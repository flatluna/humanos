namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// The kind of coordinate-plane diagram <see cref="MathDiagramSpec"/>
/// describes. Deliberately small/closed set — covers the algebra/geometry
/// topics most likely to need PIXEL-EXACT coordinates (where a generative
/// image model like gpt-image-1.5 cannot guarantee correctness, see
/// <see cref="Services.MathDiagramRenderService"/>'s doc comment).
/// </summary>
public enum MathDiagramShapeKind
{
    /// <summary>(x - CenterX)^2 + (y - CenterY)^2 = Radius^2.</summary>
    Circle,

    /// <summary>y = Slope * x + Intercept, drawn across the full visible
    /// x-range (or through two explicit <see cref="MathDiagramSpec.Points"/>
    /// if both are given instead of Slope/Intercept).</summary>
    Line,

    /// <summary>y = A * x^2 + B * x + C. Optionally set
    /// <see cref="MathDiagramSpec.Points"/> to mark/label specific
    /// (x, f(x)) values on the curve, e.g. the point (2, 4) on y = x^2 —
    /// the axis window automatically expands to keep every given point
    /// visible and readable, never squished into a corner.</summary>
    Parabola,

    /// <summary>Just a labeled set of discrete points, no connecting curve
    /// (e.g. plotting 3-4 given (x, y) pairs).</summary>
    Points,

    /// <summary>(x - CenterX)^2 / RadiusX^2 + (y - CenterY)^2 / RadiusY^2 = 1
    /// — an axis-aligned ellipse (RadiusX != RadiusY; use Circle instead
    /// when they're equal). Do NOT approximate an ellipse with Kind=Points
    /// (marking only the vertices draws no curve at all).</summary>
    Ellipse,

    /// <summary>Vertical bars, one per <see cref="MathDiagramSpec.Categories"/>
    /// entry, with a gap between bars — use for comparing discrete
    /// categories (e.g. sales by product, GDP by country, votes by
    /// candidate). Use <see cref="Histogram"/> instead for continuous
    /// numeric bins (frequency distributions).</summary>
    BarChart,

    /// <summary>Like <see cref="BarChart"/> but bars touch with no gap —
    /// use specifically for a frequency distribution over continuous
    /// numeric bins (e.g. "10-20", "20-30", ... each with a Value =
    /// frequency/count). This is the statistics-class case.</summary>
    Histogram,

    /// <summary>A full circle divided into <see cref="MathDiagramSpec.Categories"/>
    /// proportional slices (auto-normalized to 360° from the Values given —
    /// they do NOT need to already sum to 100). Use for part-of-a-whole
    /// data: market share, budget allocation, survey results.</summary>
    PieChart,

    /// <summary>An ordered (x, y) data series from <see cref="MathDiagramSpec.Points"/>,
    /// connected by straight segments in list order, on normal coordinate
    /// axes — use for a real dataset over time/an ordered variable (e.g. a
    /// stock price or GDP over several years). NOT for a formula like
    /// y=mx+b (use <see cref="Line"/> for that).</summary>
    LineChart,

    /// <summary>Unordered (x, y) points from <see cref="MathDiagramSpec.Points"/>,
    /// drawn as dots only (no connecting line) on normal coordinate axes —
    /// use for showing correlation/distribution between two variables
    /// (statistics, machine learning: e.g. plotting height vs. weight for
    /// several people, or a regression's raw data points).</summary>
    ScatterPlot,

    /// <summary>A single horizontal number line spanning <see cref="MathDiagramSpec.RangeMin"/>
    /// to <see cref="MathDiagramSpec.RangeMax"/>, with one or more labeled
    /// values marked from <see cref="MathDiagramSpec.Points"/> (only the X
    /// of each point is used) — use for basic number-line exercises
    /// (integers, inequalities, a value's position between two others).
    /// Looks much cleaner than approximating this with Kind=Points on a
    /// full 2D grid.</summary>
    NumberLine,
}

/// <summary>One labeled (name, numeric value) entry for <see cref="MathDiagramShapeKind.BarChart"/>,
/// <see cref="MathDiagramShapeKind.Histogram"/>, or <see cref="MathDiagramShapeKind.PieChart"/> —
/// e.g. <c>{ Label = "Enero", Value = 1200 }</c> or, for a Histogram bin,
/// <c>{ Label = "10-20", Value = 7 }</c> (7 = the count/frequency in that
/// bin).</summary>
public sealed class MathDiagramCategory
{
    public string Label { get; set; } = "";

    public double Value { get; set; }
}

/// <summary>
/// One labeled point to plot/annotate on a <see cref="MathDiagramSpec"/>
/// diagram (e.g. the center of a circle, or an arbitrary point set).
/// </summary>
public sealed class MathDiagramPoint
{
    public double X { get; set; }

    public double Y { get; set; }

    /// <summary>Optional short label drawn next to the point, e.g. "(2, 1)"
    /// or "centro". Null draws the point with no label.</summary>
    public string? Label { get; set; }
}

/// <summary>
/// Exact numeric parameters for ONE coordinate-plane diagram, extracted
/// (never freely "imagined") from the actual equation/values being taught —
/// e.g. for "(x-2)^2 + (y-1)^2 = 9" this is
/// <c>{ Kind = Circle, CenterX = 2, CenterY = 1, Radius = 3 }</c>. Consumed
/// exclusively by <see cref="Services.MathDiagramRenderService"/>, which
/// draws it deterministically with SkiaSharp — no LLM involved in the
/// actual pixel placement, so the rendered image is GUARANTEED to match
/// these numbers exactly (unlike a gpt-image-1.5 prompt, which can only
/// approximate a described scene).
/// </summary>
public sealed class MathDiagramSpec
{
    public MathDiagramShapeKind Kind { get; set; }

    // --- Circle ---
    // Nullable (even though these ONLY make sense for Circle/Ellipse) because the
    // LLM's structured output sometimes emits `null` for fields it considers "not
    // applicable" to the chosen Kind (e.g. Kind=Parabola with CenterX left null) —
    // a non-nullable double here throws a hard JsonException/500 on deserialization
    // in that case (real bug hit 2026-07-26). Consumers must use `?? 0`.
    public double? CenterX { get; set; }

    public double? CenterY { get; set; }

    public double? Radius { get; set; }

    // --- Ellipse: (x-CenterX)^2/RadiusX^2 + (y-CenterY)^2/RadiusY^2 = 1 (reuses CenterX/CenterY above) ---
    public double? RadiusX { get; set; }

    public double? RadiusY { get; set; }

    /// <summary>Only for Kind=Circle/Ellipse: when set, draws an exact
    /// vertical test line at this X across the whole plot (not just across
    /// the curve), for a "prueba de la recta vertical" (vertical line test)
    /// diagram — the EXACT intersection point(s) with the curve are
    /// computed geometrically in code (never guessed/eyeballed by the LLM
    /// or an image model), so the line is GUARANTEED to touch the curve at
    /// the right spot. Must satisfy |VerticalLineTestX - CenterX| &lt;=
    /// Radius (or RadiusX for Ellipse) so it actually intersects the curve
    /// — that's the whole point of this diagram (showing 2 intersection
    /// points to prove the curve fails the vertical line test). Null draws
    /// no test line.</summary>
    public double? VerticalLineTestX { get; set; }

    // --- Line (either Slope/Intercept OR two Points; Points wins if both given) ---
    public double? Slope { get; set; }

    public double? Intercept { get; set; }

    // --- Parabola: y = A*x^2 + B*x + C ---
    public double? A { get; set; }

    public double? B { get; set; }

    public double? C { get; set; }

    // --- Points (used directly for Kind=Points; also used as line endpoints for Kind=Line,
    // the ordered/unordered data series for Kind=LineChart/ScatterPlot, and the marked
    // value(s) — only .X is used — for Kind=NumberLine) ---
    public List<MathDiagramPoint>? Points { get; set; }

    // --- BarChart / Histogram / PieChart ---
    public List<MathDiagramCategory>? Categories { get; set; }

    /// <summary>Optional Y-axis unit label for BarChart/Histogram, e.g.
    /// "Frecuencia", "Millones de USD", "Unidades vendidas". Null draws no
    /// axis label. Not used by PieChart (which has no axes).</summary>
    public string? ValueAxisLabel { get; set; }

    // --- NumberLine ---
    public double? RangeMin { get; set; }

    public double? RangeMax { get; set; }

    /// <summary>Left/finite bound of a shaded solution interval/ray for
    /// Kind=NumberLine (e.g. inequality solution sets). Leave null for an
    /// unbounded ray extending to -infinity (only valid when
    /// <see cref="IntervalMax"/> is set). Leave BOTH IntervalMin and
    /// IntervalMax null to draw no shaded region at all (isolated Points
    /// only).</summary>
    public double? IntervalMin { get; set; }

    /// <summary>Right/finite bound of the shaded interval/ray. Leave null
    /// for an unbounded ray extending to +infinity (only valid when
    /// <see cref="IntervalMin"/> is set).</summary>
    public double? IntervalMax { get; set; }

    /// <summary>True if IntervalMin is an OPEN endpoint (strict &lt; or &gt;,
    /// drawn as a hollow circle) — false/null draws a filled (closed, ≤/≥)
    /// circle. Ignored if IntervalMin is null.</summary>
    public bool? IntervalMinOpen { get; set; }

    /// <summary>True if IntervalMax is an OPEN endpoint (strict &lt; or &gt;,
    /// drawn as a hollow circle) — false/null draws a filled (closed, ≤/≥)
    /// circle. Ignored if IntervalMax is null.</summary>
    public bool? IntervalMaxOpen { get; set; }

    /// <summary>Short human-readable equation shown as the diagram's title,
    /// e.g. "(x - 2)² + (y - 1)² = 9". Optional.</summary>
    public string? EquationLabel { get; set; }
}
