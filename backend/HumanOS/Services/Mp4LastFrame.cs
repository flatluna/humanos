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
                    // -sseof -0.5 seeks 0.5s before end-of-file (avoids a
                    // black/faded final frame some clips have), then grabs
                    // exactly 1 frame from there.
                    Arguments = $"-y -sseof -0.5 -i \"{inputPath}\" -frames:v 1 -q:v 2 \"{outputPath}\"",
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
