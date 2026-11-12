using System.Reflection;
using SkiaSharp;

namespace HumanOS.Services;

/// <summary>
/// Stamps every Static Recall card illustration (see
/// <see cref="StructuredLearningRecallEngine.EnsureStaticItemsAsync"/>) with
/// a small "Powered by EngramsIQ™" brand mark in the bottom-right corner —
/// the EngramsIQ logo (<c>Assets/engramslogo.png</c>, the same mark shown
/// elsewhere in the product, e.g. capabilitystudio's own header) plus the
/// literal caption text (2026-10-28).
///
/// Deliberately a deterministic SkiaSharp overlay, NOT part of the
/// generative-image prompt — image models are unreliable at rendering
/// exact small text, so the brand mark is composited onto the PNG
/// afterward instead, same reasoning as <see cref="MathDiagramRenderService"/>
/// preferring deterministic rendering wherever exact pixels matter.
/// </summary>
public sealed class RecallCardWatermarkService
{
    private const string EmbeddedLogoResourceName = "HumanOS.Assets.engramslogo.png";
    private const string WatermarkText = "Powered by EngramsIQ™";

    private readonly byte[]? _logoBytes;

    public RecallCardWatermarkService()
    {
        _logoBytes = LoadEmbeddedLogoBytes();
    }

    /// <summary>
    /// Returns a new PNG with the brand mark composited onto the
    /// bottom-right corner of <paramref name="imageBytes"/>. Best-effort:
    /// returns the original bytes unchanged if decoding/compositing fails
    /// for any reason (a missing watermark must never block a Recall
    /// card from being usable).
    /// </summary>
    public BinaryData ApplyWatermark(BinaryData imageBytes)
    {
        try
        {
            using var sourceBitmap = SKBitmap.Decode(imageBytes.ToArray());
            if (sourceBitmap is null)
            {
                return imageBytes;
            }

            using var surface = SKSurface.Create(new SKImageInfo(sourceBitmap.Width, sourceBitmap.Height));
            var canvas = surface.Canvas;
            canvas.DrawBitmap(sourceBitmap, 0, 0);

            var padding = Math.Max(12, sourceBitmap.Width / 60);
            var logoHeight = Math.Max(28, sourceBitmap.Height / 14);

            using var textPaint = new SKPaint
            {
                IsAntialias = true,
                Color = new SKColor(30, 30, 40, 235),
                TextSize = Math.Max(14, sourceBitmap.Height / 30f),
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
            };

            var textWidth = textPaint.MeasureText(WatermarkText);
            var logoWidth = 0f;

            if (_logoBytes is { Length: > 0 })
            {
                using var logoBitmap = SKBitmap.Decode(_logoBytes);
                if (logoBitmap is not null)
                {
                    logoWidth = logoHeight * ((float)logoBitmap.Width / logoBitmap.Height);

                    // Semi-transparent "pill" backdrop so the mark stays legible over any
                    // background scene, same idea as a photo app's watermark chip.
                    var backdropWidth = logoWidth + textWidth + padding * 3;
                    var backdropHeight = Math.Max(logoHeight, textPaint.TextSize) + padding;
                    var backdropLeft = sourceBitmap.Width - backdropWidth - padding;
                    var backdropTop = sourceBitmap.Height - backdropHeight - padding;

                    using var backdropPaint = new SKPaint { IsAntialias = true, Color = new SKColor(255, 255, 255, 215) };
                    var backdropRect = new SKRect(backdropLeft, backdropTop, backdropLeft + backdropWidth, backdropTop + backdropHeight);
                    canvas.DrawRoundRect(backdropRect, 12, 12, backdropPaint);

                    var logoTop = backdropTop + (backdropHeight - logoHeight) / 2;
                    var logoLeft = backdropLeft + padding / 2f;
                    var destRect = new SKRect(logoLeft, logoTop, logoLeft + logoWidth, logoTop + logoHeight);
                    canvas.DrawBitmap(logoBitmap, destRect);

                    var textLeft = logoLeft + logoWidth + padding / 2f;
                    var textBaseline = backdropTop + backdropHeight / 2 + textPaint.TextSize / 2.8f;
                    canvas.DrawText(WatermarkText, textLeft, textBaseline, textPaint);
                }
            }

            if (logoWidth == 0f)
            {
                // Logo failed to load/decode — still stamp the text alone rather than skip
                // the watermark entirely.
                var backdropWidth = textWidth + padding * 2;
                var backdropHeight = textPaint.TextSize + padding;
                var backdropLeft = sourceBitmap.Width - backdropWidth - padding;
                var backdropTop = sourceBitmap.Height - backdropHeight - padding;
                using var backdropPaint = new SKPaint { IsAntialias = true, Color = new SKColor(255, 255, 255, 215) };
                var backdropRect = new SKRect(backdropLeft, backdropTop, backdropLeft + backdropWidth, backdropTop + backdropHeight);
                canvas.DrawRoundRect(backdropRect, 12, 12, backdropPaint);
                var textBaseline = backdropTop + backdropHeight / 2 + textPaint.TextSize / 2.8f;
                canvas.DrawText(WatermarkText, backdropLeft + padding, textBaseline, textPaint);
            }

            using var resultImage = surface.Snapshot();
            using var encoded = resultImage.Encode(SKEncodedImageFormat.Png, 90);
            return new BinaryData(encoded.ToArray());
        }
        catch
        {
            return imageBytes;
        }
    }

    private static byte[]? LoadEmbeddedLogoBytes()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(EmbeddedLogoResourceName);
            if (stream is null)
            {
                return null;
            }

            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
        catch
        {
            return null;
        }
    }
}
