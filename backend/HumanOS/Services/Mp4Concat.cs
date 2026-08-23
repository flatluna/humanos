using System.Text;

namespace HumanOS.Services;

/// <summary>Concatenates multiple .mp4 clips (already same codec/resolution
/// — all our Veo/Omni Flash outputs are 720p h264/aac) into one file using
/// ffmpeg's concat demuxer with stream-copy (no re-encode, fast, no
/// quality loss). Used by StructuredLearningVideoOrchestrator.MergeAsync
/// (2026-08-22) to let a reviewer combine several generated videos, in a
/// chosen order, into a single playable video.</summary>
internal static class Mp4Concat
{
    public static async Task<byte[]> ConcatenateAsync(IReadOnlyList<byte[]> orderedVideoBytes, CancellationToken cancellationToken)
    {
        if (orderedVideoBytes.Count < 2)
        {
            throw new InvalidOperationException("At least 2 videos are required to merge.");
        }

        var workDir = Path.Combine(Path.GetTempPath(), $"mp4concat-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        var outputPath = Path.Combine(workDir, "merged.mp4");
        var listPath = Path.Combine(workDir, "list.txt");

        try
        {
            var listBuilder = new StringBuilder();
            for (var i = 0; i < orderedVideoBytes.Count; i++)
            {
                var clipPath = Path.Combine(workDir, $"clip-{i:D2}.mp4");
                await File.WriteAllBytesAsync(clipPath, orderedVideoBytes[i], cancellationToken);
                // ffmpeg concat demuxer requires forward slashes / escaped
                // paths inside the list file, even on Windows.
                listBuilder.AppendLine($"file '{clipPath.Replace("\\", "/")}'");
            }
            await File.WriteAllTextAsync(listPath, listBuilder.ToString(), cancellationToken);

            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y -f concat -safe 0 -i \"{listPath}\" -c copy \"{outputPath}\"",
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
                throw new InvalidOperationException($"ffmpeg concat failed (exit {process.ExitCode}): {stderr}");
            }

            return await File.ReadAllBytesAsync(outputPath, cancellationToken);
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
