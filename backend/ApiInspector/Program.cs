using System.Reflection;

var asm = typeof(Microsoft.Agents.AI.Workflows.WorkflowBuilder).Assembly;
var type = typeof(Microsoft.Agents.AI.Workflows.WorkflowBuilder);

string Describe(MethodInfo m)
{
    var generics = m.IsGenericMethodDefinition
        ? "<" + string.Join(",", m.GetGenericArguments().Select(a => a.Name)) + ">"
        : "";
    var ps = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType} {p.Name}"));
    return $"{m.Name}{generics}({ps}) : {m.ReturnType}";
}

Console.WriteLine("--- FanOut/FanIn methods on WorkflowBuilder ---");
foreach (var m in type.GetMethods().Where(m => m.Name.Contains("FanOut") || m.Name.Contains("FanIn")))
{
    Console.WriteLine(Describe(m));
}

Console.WriteLine("--- AddEdge overloads ---");
foreach (var m in type.GetMethods().Where(m => m.Name == "AddEdge"))
{
    Console.WriteLine(Describe(m));
}

Console.WriteLine("--- Executor-related types ---");
foreach (var t in asm.GetTypes().Where(t => t.Name.Contains("FanOut") || t.Name.Contains("FanIn") || t.Name.Contains("EdgeCondition")))
{
    Console.WriteLine(t.FullName);
}
