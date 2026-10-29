using HumanOS.Models.Capabilities.Graph;
using HumanOS.Services;

var svc = new MathDiagramRenderService();

var spec = new MathDiagramSpec
{
    Kind = MathDiagramShapeKind.Parabola,
    A = 1,
    B = 0,
    C = 0,
    Points = new List<MathDiagramPoint>
    {
        new() { X = 0, Y = 0, Label = "vértice (0, 0)" },
        new() { X = -3, Y = 9, Label = "(-3, 9)" },
        new() { X = 3, Y = 9, Label = "(3, 9)" },
    },
    EquationLabel = "y = x^2"
};

var result = await svc.RenderAsync(spec);
await File.WriteAllBytesAsync("teaching-parabola.png", result.ImageBytes.ToArray());
Console.WriteLine("Wrote teaching-parabola.png");
