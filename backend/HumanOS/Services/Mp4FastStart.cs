namespace HumanOS.Services;

/// <summary>Shared by both Google video-generation services (Veo and Omni
/// Flash) — their .mp4 outputs are NOT "faststart" (the 'moov' metadata
/// atom, which lists the audio track, sits at the very END of the file).
/// Confirmed via ffprobe/ffmpeg (2026-08-22) that the audio track IS real
/// (audible, non-silent AAC), yet Chrome's &lt;video&gt; element reliably
/// fails to recognize/decode it when streamed over HTTP with the atom in
/// that order — even with proper Content-Length/Range support on our
/// serving endpoint. Re-muxing with ffmpeg's "-movflags +faststart"
/// (stream-copy, no re-encode) moves 'moov' to the front and fixes this.
/// Best-effort: falls back to the original bytes if ffmpeg isn't on PATH
/// (e.g. some hosting environments) rather than failing the whole
/// generation.</summary>
internal static class Mp4FastStart
{
    public static async Task<byte[]> TryApplyAsync(byte[] videoBytes, CancellationToken cancellationToken)
    {
        var inputPath = Path.Combine(Path.GetTempPath(), $"video-in-{Guid.NewGuid():N}.mp4");
        var outputPath = Path.Combine(Path.GetTempPath(), $"video-out-{Guid.NewGuid():N}.mp4");
        try
        {
            await File.WriteAllBytesAsync(inputPath, videoBytes, cancellationToken);

            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y -i \"{inputPath}\" -c copy -movflags +faststart \"{outputPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && File.Exists(outputPath))
            {
                return await File.ReadAllBytesAsync(outputPath, cancellationToken);
            }

            Console.WriteLine($"[Mp4FastStart] ffmpeg remux failed (exit {process.ExitCode}) — serving original bytes.");
            return videoBytes;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Mp4FastStart] ffmpeg not available for faststart remux ({ex.Message}) — serving original bytes.");
            return videoBytes;
        }
        finally
        {
            try { File.Delete(inputPath); } catch { /* best-effort cleanup */ }
            try { File.Delete(outputPath); } catch { /* best-effort cleanup */ }
        }
    }
}
