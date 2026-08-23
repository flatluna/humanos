using System.Linq;

namespace HumanOS.Services;

/// <summary>Trims a .mp4 to [startSeconds, endSeconds) using ffmpeg
/// (2026-08-22) — re-encodes (libx264/aac) rather than stream-copying,
/// because stream-copy trims can only cut on existing keyframes, which is
/// imprecise for short 8s clips. Re-encoding gives frame-accurate cuts, at
/// the cost of a few seconds of CPU time — worth it for getting clean
/// sync when the clip is later merged with others via <see cref="Mp4Concat"/>.
/// </summary>
internal static class Mp4Trim
{
    public static async Task<byte[]> TrimAsync(byte[] videoBytes, double startSeconds, double? endSeconds, CancellationToken cancellationToken)
    {
        if (startSeconds < 0)
        {
            throw new InvalidOperationException("startSeconds must be >= 0.");
        }
        if (endSeconds is not null && endSeconds <= startSeconds)
        {
            throw new InvalidOperationException("endSeconds must be greater than startSeconds.");
        }

        var workDir = Path.Combine(Path.GetTempPath(), $"mp4trim-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        var inputPath = Path.Combine(workDir, "input.mp4");
        var outputPath = Path.Combine(workDir, "trimmed.mp4");

        try
        {
            await File.WriteAllBytesAsync(inputPath, videoBytes, cancellationToken);

            // NOTE: this is "-t" (duration), NOT "-to" (absolute end
            // timestamp) — using "-to" here was a real bug: with -ss/-to
            // both as OUTPUT options (after -i), "-to" means an absolute
            // input timestamp, not relative to -ss. Passing it the
            // (end-start) DURATION meant e.g. "-ss 10 -to 5" (end before
            // start) for any clip where start > duration — ffmpeg
            // immediately fails with EINVAL (exit -22) for that case.
            var durationArg = endSeconds is not null
                ? $"-t {(endSeconds - startSeconds).Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                : string.Empty;

            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    // -ss AFTER -i (not before) trades some seek speed for
                    // frame-accurate output — fine for 8s clips.
                    FileName = "ffmpeg",
                    Arguments = $"-y -i \"{inputPath}\" -ss {startSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} {durationArg} " +
                                $"-c:v libx264 -c:a aac -movflags +faststart \"{outputPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                var tail = string.Join('\n', stderr.Split('\n').TakeLast(15));
                throw new InvalidOperationException($"ffmpeg trim failed (exit {process.ExitCode}): {tail}");
            }

            return await File.ReadAllBytesAsync(outputPath, cancellationToken);
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
