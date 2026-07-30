using HumanOS.Models.Capabilities.Graph;
using SkiaSharp;

namespace HumanOS.Services;

/// <summary>
/// Deterministically renders a <see cref="MathDiagramSpec"/> (circle, line,
/// parabola, or a labeled set of points on a coordinate plane) to a PNG
/// using SkiaSharp — no LLM/generative-image model involved anywhere in
/// this path, so the pixels are GUARANTEED to match the spec's exact
/// numbers.
///
/// Exists because a generative image model (gpt-image-1.5, wrapped by
/// <see cref="Agents.Studio.GraphIllustrationImageService"/>) cannot
/// calculate — it can only approximate a described scene, which is fine for
/// a freeform illustration ("dibuja un corazón", "dos perros jugando") but
/// unacceptable for a precise algebra/geometry diagram (e.g. a circle whose
/// drawn center/radius must exactly match "(x-2)² + (y-1)² = 9"). Real bug
/// found 2026-07-25: a circle-equation illustration rendered by gpt-image-1.5
/// showed a circle whose marked points didn't match the equation's actual
/// center/radius.
///
/// Deliberately mirrors <see cref="Agents.Studio.GraphIllustrationImageService"/>'s
/// <c>GeneratedIllustration</c> return shape (ImageBytes/ImageModel/Width/
/// Height) so callers can treat "render deterministically" and "generate
/// with AI" as interchangeable, swappable steps based purely on whether the
/// agent that proposed the illustration set a <see cref="MathDiagramSpec"/>
/// (precise) or a plain text prompt (freeform) — see each producing agent's
/// "DECIDIENDO SI GENERAR..." instructions section for the exact rule.
/// </summary>
public sealed class MathDiagramRenderService
{
    private const int CanvasSize = 1024;
    private const int Padding = 90; // Reserves room for axis number labels + the equation title.
    private const string ModelName = "deterministic-skiasharp-v1";

    public sealed class RenderedDiagram
    {
        public BinaryData ImageBytes { get; set; } = null!;

        public string ImageModel { get; set; } = ModelName;

        public int Width { get; set; }

        public int Height { get; set; }
    }

    /// <summary>Always available — no external service/API key required.</summary>
    public bool IsConfigured => true;

    public Task<RenderedDiagram> RenderAsync(MathDiagramSpec spec, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var bitmap = new SKBitmap(CanvasSize, CanvasSize);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        switch (spec.Kind)
        {
            case MathDiagramShapeKind.BarChart:
            case MathDiagramShapeKind.Histogram:
                DrawBarOrHistogramChart(canvas, spec);
                break;

            case MathDiagramShapeKind.PieChart:
                DrawPieChart(canvas, spec);
                break;

            case MathDiagramShapeKind.NumberLine:
                DrawNumberLine(canvas, spec);
                break;

            default:
                // Circle / Line / Parabola / Points / Ellipse / LineChart /
                // ScatterPlot all share the same coordinate-plane (grid +
                // axes through the origin) rendering path.
                var (minX, maxX, minY, maxY) = ComputeBounds(spec);
                var plotArea = new SKRect(Padding, Padding, CanvasSize - Padding, CanvasSize - Padding);
                var toPixel = BuildProjection(minX, maxX, minY, maxY, plotArea);
                DrawGridAndAxes(canvas, plotArea, minX, maxX, minY, maxY, toPixel);
                DrawShape(canvas, spec, toPixel);
                break;
        }

        if (!string.IsNullOrWhiteSpace(spec.EquationLabel))
        {
            using var titleFont = new SKFont(SKTypeface.Default, 30);
            using var titlePaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            canvas.DrawText(spec.EquationLabel, Padding, 50, titleFont, titlePaint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return Task.FromResult(new RenderedDiagram
        {
            ImageBytes = BinaryData.FromBytes(data.ToArray()),
            ImageModel = ModelName,
            Width = CanvasSize,
            Height = CanvasSize
        });
    }

    /// <summary>Math-space bounding box (with padding + "nice" rounding) that
    /// the diagram's shape must fit inside, always including the origin so
    /// the axes are visible.</summary>
    private static (double minX, double maxX, double minY, double maxY) ComputeBounds(MathDiagramSpec spec)
    {
        var xs = new List<double> { 0 };
        var ys = new List<double> { 0 };

        switch (spec.Kind)
        {
            case MathDiagramShapeKind.Circle:
                {
                    var cx = spec.CenterX ?? 0;
                    var cy = spec.CenterY ?? 0;
                    var r = spec.Radius ?? 0;
                    xs.Add(cx - r);
                    xs.Add(cx + r);
                    ys.Add(cy - r);
                    ys.Add(cy + r);
                    break;
                }

            case MathDiagramShapeKind.Line:
                if (spec.Points is { Count: >= 2 })
                {
                    foreach (var p in spec.Points)
                    {
                        xs.Add(p.X);
                        ys.Add(p.Y);
                    }
                }
                else
                {
                    // No explicit x-range given — use a symmetric default
                    // window and derive the matching y's from the line.
                    xs.Add(-10);
                    xs.Add(10);
                    ys.Add((spec.Slope ?? 0) * -10 + (spec.Intercept ?? 0));
                    ys.Add((spec.Slope ?? 0) * 10 + (spec.Intercept ?? 0));
                }
                break;

            case MathDiagramShapeKind.Parabola:
                {
                    var a = spec.A ?? 0;
                    var b = spec.B ?? 0;
                    var c = spec.C ?? 0;
                    // Vertex x = -b / 2a; sample a symmetric window around it.
                    var vertexX = a == 0 ? 0 : -b / (2 * a);
                    xs.Add(vertexX - 8);
                    xs.Add(vertexX + 8);
                    for (var x = vertexX - 8; x <= vertexX + 8; x += 1)
                    {
                        ys.Add(a * x * x + b * x + c);
                    }
                    var vertexY = a * vertexX * vertexX + b * vertexX + c;
                    ys.Add(vertexY);
                    if (spec.Points is { Count: > 0 })
                    {
                        foreach (var p in spec.Points)
                        {
                            xs.Add(p.X);
                            ys.Add(p.Y);
                        }
                    }
                }
                break;

            case MathDiagramShapeKind.Points:
                if (spec.Points is { Count: > 0 })
                {
                    foreach (var p in spec.Points)
                    {
                        xs.Add(p.X);
                        ys.Add(p.Y);
                    }
                }
                break;

            case MathDiagramShapeKind.Ellipse:
                {
                    var cx = spec.CenterX ?? 0;
                    var cy = spec.CenterY ?? 0;
                    var rx = spec.RadiusX ?? 0;
                    var ry = spec.RadiusY ?? 0;
                    xs.Add(cx - rx);
                    xs.Add(cx + rx);
                    ys.Add(cy - ry);
                    ys.Add(cy + ry);
                    break;
                }

            case MathDiagramShapeKind.LineChart:
            case MathDiagramShapeKind.ScatterPlot:
                if (spec.Points is { Count: > 0 })
                {
                    foreach (var p in spec.Points)
                    {
                        xs.Add(p.X);
                        ys.Add(p.Y);
                    }
                }
                break;
        }

        var rawMinX = xs.Min();
        var rawMaxX = xs.Max();
        var rawMinY = ys.Min();
        var rawMaxY = ys.Max();

        var spanX = Math.Max(rawMaxX - rawMinX, 1);
        var spanY = Math.Max(rawMaxY - rawMinY, 1);
        var midX = (rawMinX + rawMaxX) / 2;
        var midY = (rawMinY + rawMaxY) / 2;

        // Circle/Ellipse MUST use the same span on both axes — otherwise the
        // curve itself is visually distorted (a circle would render as an
        // oval). Every other kind is a normal 2D plot (like a textbook graph
        // or Desmos) where X and Y independently get their own "nice"
        // padding — forcing them equal would blow up whichever axis has the
        // smaller natural range, e.g. a steep parabola like y=x^2 would get
        // an enormous, teaching-irrelevant X window just to match its large
        // Y range, squishing small values like (2, 4) into an unreadable
        // sliver near the origin.
        if (spec.Kind is MathDiagramShapeKind.Circle or MathDiagramShapeKind.Ellipse)
        {
            var span = Math.Max(spanX, spanY) * 1.4;
            return (midX - span / 2, midX + span / 2, midY - span / 2, midY + span / 2);
        }

        var paddedSpanX = spanX * 1.4;
        var paddedSpanY = spanY * 1.4;
        return (midX - paddedSpanX / 2, midX + paddedSpanX / 2, midY - paddedSpanY / 2, midY + paddedSpanY / 2);
    }

    private static Func<double, double, SKPoint> BuildProjection(
        double minX, double maxX, double minY, double maxY, SKRect plotArea)
    {
        return (x, y) =>
        {
            var px = plotArea.Left + (float)((x - minX) / (maxX - minX)) * plotArea.Width;
            // Screen Y grows downward — flip so +y goes up, matching a real graph.
            var py = plotArea.Bottom - (float)((y - minY) / (maxY - minY)) * plotArea.Height;
            return new SKPoint(px, py);
        };
    }

    /// <summary>Picks a human-friendly gridline step (1, 2, 5, 10, 20, 50...)
    /// for a given axis span, mirroring how real graphing tools choose tick
    /// spacing instead of showing ugly fractional gridlines.</summary>
    private static double NiceStep(double span)
    {
        var roughStep = span / 10;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(roughStep)));
        var normalized = roughStep / magnitude;

        double niceNormalized = normalized switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 5 => 5,
            _ => 10
        };

        return niceNormalized * magnitude;
    }

    private static void DrawGridAndAxes(
        SKCanvas canvas, SKRect plotArea, double minX, double maxX, double minY, double maxY,
        Func<double, double, SKPoint> toPixel)
    {
        using var gridPaint = new SKPaint { Color = new SKColor(220, 220, 220), StrokeWidth = 1, IsAntialias = true };
        using var axisPaint = new SKPaint { Color = SKColors.Black, StrokeWidth = 2.5f, IsAntialias = true };
        using var labelFont = new SKFont(SKTypeface.Default, 20);
        using var labelPaint = new SKPaint { Color = new SKColor(90, 90, 90), IsAntialias = true };

        var stepX = NiceStep(maxX - minX);
        var stepY = NiceStep(maxY - minY);

        for (var x = Math.Ceiling(minX / stepX) * stepX; x <= maxX; x += stepX)
        {
            var top = toPixel(x, maxY);
            var bottom = toPixel(x, minY);
            canvas.DrawLine(top, bottom, gridPaint);
            if (Math.Abs(x) > stepX / 1000.0)
            {
                var labelPos = toPixel(x, 0);
                canvas.DrawText(FormatTick(x), labelPos.X - 8, Math.Min(labelPos.Y + 22, plotArea.Bottom + 30), labelFont, labelPaint);
            }
        }

        for (var y = Math.Ceiling(minY / stepY) * stepY; y <= maxY; y += stepY)
        {
            var left = toPixel(minX, y);
            var right = toPixel(maxX, y);
            canvas.DrawLine(left, right, gridPaint);
            if (Math.Abs(y) > stepY / 1000.0)
            {
                var labelPos = toPixel(0, y);
                canvas.DrawText(FormatTick(y), Math.Max(labelPos.X - 45, plotArea.Left - 55), labelPos.Y + 7, labelFont, labelPaint);
            }
        }

        // X and Y axes themselves (through the origin), drawn on top of the grid.
        canvas.DrawLine(toPixel(minX, 0), toPixel(maxX, 0), axisPaint);
        canvas.DrawLine(toPixel(0, minY), toPixel(0, maxY), axisPaint);
    }

    private static string FormatTick(double value)
    {
        return value % 1 == 0 ? ((int)value).ToString() : value.ToString("0.##");
    }

    private static void DrawShape(SKCanvas canvas, MathDiagramSpec spec, Func<double, double, SKPoint> toPixel)
    {
        using var shapePaint = new SKPaint { Color = new SKColor(37, 99, 235), StrokeWidth = 4, IsAntialias = true, Style = SKPaintStyle.Stroke };
        using var pointPaint = new SKPaint { Color = new SKColor(220, 38, 38), IsAntialias = true, Style = SKPaintStyle.Fill };
        using var pointLabelFont = new SKFont(SKTypeface.Default, 22);
        using var pointLabelPaint = new SKPaint { Color = new SKColor(220, 38, 38), IsAntialias = true };

        switch (spec.Kind)
        {
            case MathDiagramShapeKind.Circle:
                {
                    var cx = spec.CenterX ?? 0;
                    var cy = spec.CenterY ?? 0;
                    var r = spec.Radius ?? 0;
                    var center = toPixel(cx, cy);
                    var edge = toPixel(cx + r, cy);
                    var pixelRadius = edge.X - center.X;
                    canvas.DrawCircle(center, pixelRadius, shapePaint);
                    canvas.DrawCircle(center, 6, pointPaint);
                    canvas.DrawText($"({FormatTick(cx)}, {FormatTick(cy)})", center.X + 10, center.Y - 10, pointLabelFont, pointLabelPaint);
                    DrawVerticalLineTest(canvas, spec, toPixel, cx, cy, r, r);
                    break;
                }

            case MathDiagramShapeKind.Line:
                {
                    if (spec.Points is { Count: >= 2 })
                    {
                        var p1 = toPixel(spec.Points[0].X, spec.Points[0].Y);
                        var p2 = toPixel(spec.Points[1].X, spec.Points[1].Y);
                        canvas.DrawLine(p1, p2, shapePaint);
                        DrawLabeledPoints(canvas, spec.Points, toPixel, pointPaint, pointLabelFont, pointLabelPaint);
                    }
                    else
                    {
                        var slope = spec.Slope ?? 0;
                        var intercept = spec.Intercept ?? 0;
                        var p1 = toPixel(-1000, slope * -1000 + intercept);
                        var p2 = toPixel(1000, slope * 1000 + intercept);
                        canvas.Save();
                        canvas.ClipRect(new SKRect(0, 0, CanvasSize, CanvasSize));
                        canvas.DrawLine(p1, p2, shapePaint);
                        canvas.Restore();
                    }
                    break;
                }

            case MathDiagramShapeKind.Parabola:
                {
                    var a = spec.A ?? 0;
                    var b = spec.B ?? 0;
                    var c = spec.C ?? 0;
                    using var path = new SKPath();
                    var first = true;
                    for (var x = -1000d; x <= 1000d; x += 0.25)
                    {
                        var y = a * x * x + b * x + c;
                        var pt = toPixel(x, y);
                        if (pt.Y < -CanvasSize || pt.Y > CanvasSize * 2) continue;
                        if (first)
                        {
                            path.MoveTo(pt);
                            first = false;
                        }
                        else
                        {
                            path.LineTo(pt);
                        }
                    }
                    canvas.DrawPath(path, shapePaint);
                    if (spec.Points is { Count: > 0 })
                    {
                        DrawLabeledPoints(canvas, spec.Points, toPixel, pointPaint, pointLabelFont, pointLabelPaint);
                    }
                    break;
                }

            case MathDiagramShapeKind.Points:
                if (spec.Points is { Count: > 0 })
                {
                    DrawLabeledPoints(canvas, spec.Points, toPixel, pointPaint, pointLabelFont, pointLabelPaint);
                }
                break;

            case MathDiagramShapeKind.Ellipse:
                {
                    var cx = spec.CenterX ?? 0;
                    var cy = spec.CenterY ?? 0;
                    var rx = spec.RadiusX ?? 0;
                    var ry = spec.RadiusY ?? 0;
                    var center = toPixel(cx, cy);
                    var edgeX = toPixel(cx + rx, cy);
                    var edgeY = toPixel(cx, cy + ry);
                    var pixelRadiusX = edgeX.X - center.X;
                    var pixelRadiusY = center.Y - edgeY.Y; // Screen Y grows downward, so subtract in this order.
                    var rect = new SKRect(center.X - pixelRadiusX, center.Y - pixelRadiusY, center.X + pixelRadiusX, center.Y + pixelRadiusY);
                    canvas.DrawOval(rect, shapePaint);
                    canvas.DrawCircle(center, 6, pointPaint);
                    canvas.DrawText($"({FormatTick(cx)}, {FormatTick(cy)})", center.X + 10, center.Y - 10, pointLabelFont, pointLabelPaint);
                    if (spec.Points is { Count: > 0 })
                    {
                        DrawLabeledPoints(canvas, spec.Points, toPixel, pointPaint, pointLabelFont, pointLabelPaint);
                    }
                    DrawVerticalLineTest(canvas, spec, toPixel, cx, cy, rx, ry);
                    break;
                }

            case MathDiagramShapeKind.LineChart:
                {
                    if (spec.Points is { Count: >= 2 })
                    {
                        using var path = new SKPath();
                        var first = true;
                        foreach (var p in spec.Points)
                        {
                            var pt = toPixel(p.X, p.Y);
                            if (first)
                            {
                                path.MoveTo(pt);
                                first = false;
                            }
                            else
                            {
                                path.LineTo(pt);
                            }
                        }
                        canvas.DrawPath(path, shapePaint);
                    }
                    if (spec.Points is { Count: > 0 })
                    {
                        DrawLabeledPoints(canvas, spec.Points, toPixel, pointPaint, pointLabelFont, pointLabelPaint);
                    }
                    break;
                }

            case MathDiagramShapeKind.ScatterPlot:
                if (spec.Points is { Count: > 0 })
                {
                    DrawLabeledPoints(canvas, spec.Points, toPixel, pointPaint, pointLabelFont, pointLabelPaint);
                }
                break;
        }
    }

    private static void DrawLabeledPoints(
        SKCanvas canvas, List<MathDiagramPoint> points, Func<double, double, SKPoint> toPixel,
        SKPaint pointPaint, SKFont labelFont, SKPaint labelPaint)
    {
        foreach (var p in points)
        {
            var pixel = toPixel(p.X, p.Y);
            canvas.DrawCircle(pixel, 6, pointPaint);
            var label = p.Label ?? $"({FormatTick(p.X)}, {FormatTick(p.Y)})";
            canvas.DrawText(label, pixel.X + 10, pixel.Y - 10, labelFont, labelPaint);
        }
    }

    /// <summary>Draws an exact "prueba de la recta vertical" (vertical line
    /// test) overlay on top of a Circle/Ellipse: a vertical line at
    /// spec.VerticalLineTestX spanning the whole canvas, plus the curve's
    /// intersection point(s) with that line, computed geometrically (never
    /// guessed) so the line is GUARANTEED to touch the curve exactly where
    /// drawn. No-op if VerticalLineTestX is null or doesn't actually
    /// intersect the curve.</summary>
    private static void DrawVerticalLineTest(
        SKCanvas canvas, MathDiagramSpec spec, Func<double, double, SKPoint> toPixel,
        double centerX, double centerY, double radiusX, double radiusY)
    {
        if (spec.VerticalLineTestX is not { } testX || radiusX <= 0)
        {
            return;
        }

        var normalizedDx = (testX - centerX) / radiusX;
        if (Math.Abs(normalizedDx) > 1)
        {
            return; // The line doesn't actually cross the curve at this X.
        }

        using var testLinePaint = new SKPaint { Color = new SKColor(220, 38, 38), StrokeWidth = 4, IsAntialias = true, Style = SKPaintStyle.Stroke };
        using var testPointPaint = new SKPaint { Color = new SKColor(220, 38, 38), IsAntialias = true, Style = SKPaintStyle.Fill };
        using var testLabelFont = new SKFont(SKTypeface.Default, 22);
        using var testLabelPaint = new SKPaint { Color = new SKColor(220, 38, 38), IsAntialias = true };

        // Draw the test line across the whole visible canvas, not just the curve.
        var top = toPixel(testX, 1000);
        var bottom = toPixel(testX, -1000);
        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, CanvasSize, CanvasSize));
        canvas.DrawLine(top, bottom, testLinePaint);
        canvas.Restore();

        var halfHeight = radiusY * Math.Sqrt(1 - normalizedDx * normalizedDx);
        var upperY = centerY + halfHeight;
        var lowerY = centerY - halfHeight;

        var upperPoint = toPixel(testX, upperY);
        canvas.DrawCircle(upperPoint, 7, testPointPaint);
        canvas.DrawText($"({FormatTick(testX)}, {FormatTick(upperY)})", upperPoint.X + 12, upperPoint.Y - 10, testLabelFont, testLabelPaint);

        if (halfHeight > 1e-9)
        {
            var lowerPoint = toPixel(testX, lowerY);
            canvas.DrawCircle(lowerPoint, 7, testPointPaint);
            canvas.DrawText($"({FormatTick(testX)}, {FormatTick(lowerY)})", lowerPoint.X + 12, lowerPoint.Y + 26, testLabelFont, testLabelPaint);
        }
    }

    private static readonly SKColor[] ChartPalette =
    [
        new SKColor(37, 99, 235),   // blue
        new SKColor(220, 38, 38),   // red
        new SKColor(22, 163, 74),   // green
        new SKColor(234, 179, 8),   // yellow
        new SKColor(147, 51, 234),  // purple
        new SKColor(249, 115, 22),  // orange
        new SKColor(20, 184, 166),  // teal
        new SKColor(219, 39, 119),  // pink
    ];

    /// <summary>Renders <see cref="MathDiagramShapeKind.BarChart"/> (bars with
    /// a gap, discrete categories) and <see cref="MathDiagramShapeKind.Histogram"/>
    /// (bars touching, continuous numeric bins) — same layout, only the gap
    /// ratio differs.</summary>
    private static void DrawBarOrHistogramChart(SKCanvas canvas, MathDiagramSpec spec)
    {
        var categories = spec.Categories;
        if (categories is not { Count: > 0 })
        {
            return;
        }

        var plotArea = new SKRect(Padding, Padding, CanvasSize - Padding, CanvasSize - Padding - 40);
        var maxValue = Math.Max(categories.Max(c => c.Value), 0.0001);
        var stepY = NiceStep(maxValue);
        var niceMax = Math.Ceiling(maxValue / stepY) * stepY;

        using var axisPaint = new SKPaint { Color = SKColors.Black, StrokeWidth = 2.5f, IsAntialias = true };
        using var gridPaint = new SKPaint { Color = new SKColor(220, 220, 220), StrokeWidth = 1, IsAntialias = true };
        using var tickFont = new SKFont(SKTypeface.Default, 20);
        using var tickPaint = new SKPaint { Color = new SKColor(90, 90, 90), IsAntialias = true };
        using var catLabelFont = new SKFont(SKTypeface.Default, 20);
        using var catLabelPaint = new SKPaint { Color = new SKColor(60, 60, 60), IsAntialias = true };
        using var barPaint = new SKPaint { Color = new SKColor(37, 99, 235), IsAntialias = true, Style = SKPaintStyle.Fill };
        using var valueFont = new SKFont(SKTypeface.Default, 20);
        using var valuePaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };

        // Horizontal gridlines + Y-axis value labels (0..niceMax).
        for (var y = 0.0; y <= niceMax + stepY / 1000.0; y += stepY)
        {
            var py = plotArea.Bottom - (float)(y / niceMax) * plotArea.Height;
            canvas.DrawLine(new SKPoint(plotArea.Left, py), new SKPoint(plotArea.Right, py), gridPaint);
            var text = FormatTick(y);
            canvas.DrawText(text, plotArea.Left - 20 - tickFont.MeasureText(text), py + 7, tickFont, tickPaint);
        }

        // Axes.
        canvas.DrawLine(new SKPoint(plotArea.Left, plotArea.Top), new SKPoint(plotArea.Left, plotArea.Bottom), axisPaint);
        canvas.DrawLine(new SKPoint(plotArea.Left, plotArea.Bottom), new SKPoint(plotArea.Right, plotArea.Bottom), axisPaint);

        var slotWidth = plotArea.Width / categories.Count;
        var barWidthRatio = spec.Kind == MathDiagramShapeKind.Histogram ? 1.0f : 0.6f;

        for (var i = 0; i < categories.Count; i++)
        {
            var category = categories[i];
            var slotLeft = plotArea.Left + i * slotWidth;
            var barWidth = slotWidth * barWidthRatio;
            var barLeft = slotLeft + (slotWidth - barWidth) / 2;
            var barHeightRatio = (float)(category.Value / niceMax);
            var barTop = plotArea.Bottom - barHeightRatio * plotArea.Height;
            canvas.DrawRect(new SKRect(barLeft, barTop, barLeft + barWidth, plotArea.Bottom), barPaint);

            var valueText = FormatTick(category.Value);
            var valueTextWidth = valueFont.MeasureText(valueText);
            canvas.DrawText(valueText, barLeft + barWidth / 2 - valueTextWidth / 2, barTop - 10, valueFont, valuePaint);

            var catTextWidth = catLabelFont.MeasureText(category.Label);
            canvas.DrawText(category.Label, slotLeft + slotWidth / 2 - catTextWidth / 2, plotArea.Bottom + 30, catLabelFont, catLabelPaint);
        }

        if (!string.IsNullOrWhiteSpace(spec.ValueAxisLabel))
        {
            using var axisLabelFont = new SKFont(SKTypeface.Default, 20);
            using var axisLabelPaint = new SKPaint { Color = new SKColor(90, 90, 90), IsAntialias = true };
            canvas.Save();
            canvas.RotateDegrees(-90, 30, CanvasSize / 2f);
            var textWidth = axisLabelFont.MeasureText(spec.ValueAxisLabel);
            canvas.DrawText(spec.ValueAxisLabel, 30 - textWidth / 2, CanvasSize / 2f, axisLabelFont, axisLabelPaint);
            canvas.Restore();
        }
    }

    /// <summary>Renders <see cref="MathDiagramShapeKind.PieChart"/> — slices are
    /// auto-normalized from each Category's raw Value (they don't need to
    /// already sum to 100/360).</summary>
    private static void DrawPieChart(SKCanvas canvas, MathDiagramSpec spec)
    {
        var categories = spec.Categories;
        if (categories is not { Count: > 0 })
        {
            return;
        }

        var total = categories.Sum(c => c.Value);
        if (total <= 0)
        {
            return;
        }

        var center = new SKPoint(CanvasSize / 2f, CanvasSize / 2f - 60);
        var radius = CanvasSize / 2f - Padding - 40;
        var rect = new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius);

        using var pctFont = new SKFont(SKTypeface.Default, 22);
        using var pctPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var legendFont = new SKFont(SKTypeface.Default, 20);
        using var legendPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        using var strokePaint = new SKPaint { Color = SKColors.White, StrokeWidth = 3, IsAntialias = true, Style = SKPaintStyle.Stroke };

        var startAngle = -90f;
        for (var i = 0; i < categories.Count; i++)
        {
            var category = categories[i];
            var sweep = (float)(category.Value / total * 360.0);
            var color = ChartPalette[i % ChartPalette.Length];
            using var slicePaint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };

            using var path = new SKPath();
            path.MoveTo(center);
            path.ArcTo(rect, startAngle, sweep, false);
            path.Close();
            canvas.DrawPath(path, slicePaint);
            canvas.DrawPath(path, strokePaint);

            var midAngleRad = (startAngle + sweep / 2) * Math.PI / 180.0;
            var labelRadius = radius * 0.65f;
            var labelX = center.X + (float)(Math.Cos(midAngleRad) * labelRadius);
            var labelY = center.Y + (float)(Math.Sin(midAngleRad) * labelRadius);
            var pctText = $"{category.Value / total * 100.0:0.#}%";
            var pctTextWidth = pctFont.MeasureText(pctText);
            canvas.DrawText(pctText, labelX - pctTextWidth / 2, labelY + 8, pctFont, pctPaint);

            startAngle += sweep;
        }

        // Legend, wrapped across lines below the pie.
        var legendX = (float)Padding;
        var legendY = center.Y + radius + 60;
        for (var i = 0; i < categories.Count; i++)
        {
            var color = ChartPalette[i % ChartPalette.Length];
            using var swatchPaint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
            canvas.DrawRect(new SKRect(legendX, legendY, legendX + 24, legendY + 24), swatchPaint);
            canvas.DrawText(categories[i].Label, legendX + 32, legendY + 20, legendFont, legendPaint);

            legendX += 32 + legendFont.MeasureText(categories[i].Label) + 40;
            if (legendX > CanvasSize - Padding - 100)
            {
                legendX = Padding;
                legendY += 36;
            }
        }
    }

    /// <summary>Renders <see cref="MathDiagramShapeKind.NumberLine"/> — a single
    /// horizontal line from RangeMin to RangeMax with tick marks, plus any
    /// Points marked as labeled dots (only .X is used), and an optional
    /// shaded interval/ray (IntervalMin/IntervalMax, each open or closed)
    /// for representing inequality solution sets.</summary>
    private static void DrawNumberLine(SKCanvas canvas, MathDiagramSpec spec)
    {
        var rangeMin = spec.RangeMin ?? -10;
        var rangeMax = spec.RangeMax ?? 10;
        if (rangeMax <= rangeMin)
        {
            rangeMax = rangeMin + 1;
        }

        var y = CanvasSize / 2f;
        var left = (float)Padding;
        var right = CanvasSize - (float)Padding;

        using var axisPaint = new SKPaint { Color = SKColors.Black, StrokeWidth = 3, IsAntialias = true };
        using var tickFont = new SKFont(SKTypeface.Default, 22);
        using var tickPaint = new SKPaint { Color = new SKColor(90, 90, 90), IsAntialias = true };
        using var pointPaint = new SKPaint { Color = new SKColor(220, 38, 38), IsAntialias = true, Style = SKPaintStyle.Fill };
        using var pointLabelFont = new SKFont(SKTypeface.Default, 24);
        using var pointLabelPaint = new SKPaint { Color = new SKColor(220, 38, 38), IsAntialias = true };

        float ToX(double v) => left + (float)((v - rangeMin) / (rangeMax - rangeMin)) * (right - left);

        // Main line with simple arrowheads on both ends.
        canvas.DrawLine(new SKPoint(left - 15, y), new SKPoint(right + 15, y), axisPaint);
        canvas.DrawLine(new SKPoint(right + 15, y), new SKPoint(right, y - 8), axisPaint);
        canvas.DrawLine(new SKPoint(right + 15, y), new SKPoint(right, y + 8), axisPaint);
        canvas.DrawLine(new SKPoint(left - 15, y), new SKPoint(left, y - 8), axisPaint);
        canvas.DrawLine(new SKPoint(left - 15, y), new SKPoint(left, y + 8), axisPaint);

        var step = NiceStep(rangeMax - rangeMin);
        for (var v = Math.Ceiling(rangeMin / step) * step; v <= rangeMax; v += step)
        {
            var x = ToX(v);
            canvas.DrawLine(new SKPoint(x, y - 10), new SKPoint(x, y + 10), axisPaint);
            var text = FormatTick(v);
            var textWidth = tickFont.MeasureText(text);
            canvas.DrawText(text, x - textWidth / 2, y + 40, tickFont, tickPaint);
        }

        if (spec.IntervalMin is not null || spec.IntervalMax is not null)
        {
            using var shadePaint = new SKPaint { Color = new SKColor(220, 38, 38), StrokeWidth = 8, IsAntialias = true };
            using var endpointFillPaint = new SKPaint { Color = new SKColor(220, 38, 38), IsAntialias = true, Style = SKPaintStyle.Fill };
            using var endpointOpenPaint = new SKPaint { Color = SKColors.White, IsAntialias = true, Style = SKPaintStyle.Fill };
            using var endpointStrokePaint = new SKPaint { Color = new SKColor(220, 38, 38), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 };

            var shadeStartX = spec.IntervalMin is { } minV ? ToX(Math.Max(minV, rangeMin)) : left - 15;
            var shadeEndX = spec.IntervalMax is { } maxV ? ToX(Math.Min(maxV, rangeMax)) : right + 15;
            canvas.DrawLine(new SKPoint(shadeStartX, y), new SKPoint(shadeEndX, y), shadePaint);

            void DrawEndpoint(double value, bool? open)
            {
                var x = ToX(value);
                if (open == true)
                {
                    canvas.DrawCircle(new SKPoint(x, y), 9, endpointOpenPaint);
                    canvas.DrawCircle(new SKPoint(x, y), 9, endpointStrokePaint);
                }
                else
                {
                    canvas.DrawCircle(new SKPoint(x, y), 9, endpointFillPaint);
                }
            }

            if (spec.IntervalMin is { } finiteMin)
            {
                DrawEndpoint(finiteMin, spec.IntervalMinOpen);
            }

            if (spec.IntervalMax is { } finiteMax)
            {
                DrawEndpoint(finiteMax, spec.IntervalMaxOpen);
            }
        }

        if (spec.Points is { Count: > 0 })
        {
            foreach (var p in spec.Points)
            {
                var x = ToX(p.X);
                canvas.DrawCircle(new SKPoint(x, y), 9, pointPaint);
                var label = p.Label ?? FormatTick(p.X);
                var labelWidth = pointLabelFont.MeasureText(label);
                canvas.DrawText(label, x - labelWidth / 2, y - 25, pointLabelFont, pointLabelPaint);
            }
        }
    }
}
