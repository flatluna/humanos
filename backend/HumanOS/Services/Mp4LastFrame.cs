namespace HumanOS.Services;

/// <summary>Extracts the last frame of a .mp4 as a JPEG (2026-08-25) — used
/// to auto-carry the visual continuity from one Structured Learning video
/// clip into the next clip's starting image, same ffmpeg-subprocess style
/// as <see cref="Mp4Trim"/>/<see cref="Mp4Concat"/>.</summary>
internal static class Mp4LastFrame
{
    public static async Task<byte[]> ExtractAsync(byte[] videoBytes, CancellationToken cancellationToken)
    {
        var workDir = Path.Combine(Path.GetTempPath(), $"mp4lastframe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        var inputPath = Path.Combine(workDir, "input.mp4");
        var outputPath = Path.Combine(workDir, "lastframe.jpg");

        try
        {
            await File.WriteAllBytesAsync(inputPath, videoBytes, cancellationToken);

            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    // -sseof -0.1 seeks 0.1s before end-of-file (2026-09-19:
                    // narrowed from -0.5, which was landing noticeably before
                    // the true last frame on ~10s clips — visibly grabbing a
                    // mid-clip frame instead of the actual ending one).
                    Arguments = $"-y -sseof -0.1 -i \"{inputPath}\" -frames:v 1 -q:v 2 \"{outputPath}\"",
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
                throw new InvalidOperationException($"ffmpeg last-frame extraction failed (exit {process.ExitCode}): {tail}");
            }

            return await File.ReadAllBytesAsync(outputPath, cancellationToken);
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}

/// <summary>Extracts the first frame of a .mp4 as a JPEG — used as the
/// starting-image candidate when regenerating a video from its own
/// original prompt (2026-08-26).</summary>
internal static class Mp4FirstFrame
{
    public static async Task<byte[]> ExtractAsync(byte[] videoBytes, CancellationToken cancellationToken)
    {
        var workDir = Path.Combine(Path.GetTempPath(), $"mp4firstframe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        var inputPath = Path.Combine(workDir, "input.mp4");
        var outputPath = Path.Combine(workDir, "firstframe.jpg");

        try
        {
            await File.WriteAllBytesAsync(inputPath, videoBytes, cancellationToken);

            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y -i \"{inputPath}\" -frames:v 1 -q:v 2 \"{outputPath}\"",
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
                throw new InvalidOperationException($"ffmpeg first-frame extraction failed (exit {process.ExitCode}): {tail}");
            }

            return await File.ReadAllBytesAsync(outputPath, cancellationToken);
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
