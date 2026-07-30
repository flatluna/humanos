using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Models.Capabilities.Graph;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Runtime;

/// <summary>
/// Structured output of KnowledgeExpansionAgent: a deeper explanation of a
/// CapabilityGraphNode than its base Teaching content, plus an optional
/// diagram prompt.
/// </summary>
public sealed class KnowledgeExpansionResponse
{
    /// <summary>Expanded explanation, semantic HTML (same small tag set as
    /// NodeExperienceBlueprintStep.Content: p/strong/ul/li/a), with
    /// "&lt;a href&gt;" citations preserved when grounded in web findings.</summary>
    public string ExpandedContentHtml { get; set; } = string.Empty;

    /// <summary>A text prompt for generating ONE diagram that genuinely
    /// helps visualize this expanded content, or null if a diagram wouldn't
    /// add value (e.g. a purely conceptual/definitional expansion with
    /// nothing spatial/structural to draw).</summary>
    public string? DiagramPrompt { get; set; }

    /// <summary>Exact numeric spec for a coordinate-plane diagram (circle,
    /// line, parabola, or plotted points), set INSTEAD OF DiagramPrompt
    /// when the diagram IS a precise math/geometry graph derived from a
    /// real equation/values (never both). Rendered deterministically
    /// (SkiaSharp, no image model).</summary>
    public MathDiagramSpec? MathDiagram { get; set; }
}

/// <summary>
/// Agente de ampliación de conocimiento bajo demanda ("Profundizar",
/// 2026-07-20) — se dispara SOLO cuando el alumno hace clic explícito en el
/// paso de Enseñanza, nunca automáticamente (mismo patrón de "acción
/// deliberada" que las pistas del tutor, ver
/// /memories/repo/adaptive-learning-engine-design.md). Combina:
///   1. El conocimiento propio del LLM (no restringido a lo curado del PDF).
///   2. Hallazgos de Grounding-with-Bing-Search (opcionales, si
///      WebGroundingService está configurado) — a diferencia del pipeline
///      de creación de Studio (que solo busca en nodos NeedsCurrentInfo),
///      aquí SIEMPRE se intenta buscar si el servicio está configurado,
///      porque el alumno lo pidió explícitamente.
///
/// Plain ChatClientAgent con structured output — mismo patrón que
/// ExperienceDesignerAgent/GraphArchitectAgent (Agents/Studio).
/// </summary>
public sealed class KnowledgeExpansionAgent
{
    private const string Instructions = """
        Eres un tutor experto que AMPLÍA (nunca reemplaza) el contenido de
        enseñanza ya mostrado a un alumno sobre un tema específico de un
        grafo de capacidades. El alumno ya vio una explicación base y pidió
        explícitamente "profundizar" — quiere más: más contexto, más
        matices, ejemplos adicionales, conexiones con el mundo real, o
        información reciente sobre el tema.

        FUENTES QUE PUEDES USAR
        1. Tu propio conocimiento (entrenamiento) — siempre disponible.
        2. "Hallazgos web actuales" (Grounding with Bing Search), si se te
           proporcionan en el prompt — úsalos para complementar con
           información reciente/actual que tu conocimiento de entrenamiento
           podría no tener. Si te dan hallazgos web, SIEMPRE preserva sus
           citas en el formato "[Título](URL)" convirtiéndolas a HTML real:
           <a href="URL">Título</a>. Nunca inventes una URL — solo usa las
           que vinieron literalmente en los hallazgos.

        QUÉ NO HACER
        - No repitas la definición/explicación base palabra por palabra —
          agrega valor real (matices, ejemplos nuevos, aplicaciones,
          contexto histórico o actual, comparaciones, errores comunes).
        - No inventes hechos que no vengan de tu conocimiento general
          confiable o de los hallazgos web proporcionados.
        - No generes relleno/paja para parecer más largo — si el tema es
          simple y ya está bien cubierto, una ampliación breve y honesta es
          mejor que párrafos vacíos.

        FORMATO — HTML SEMÁNTICO SIMPLE (se renderiza sanitizado directo al alumno)
        - <p>...</p> para separar ideas.
        - <strong>...</strong> para términos clave.
        - <ul><li>...</li></ul> para listas cortas cuando ayuden a escanear.
        - <a href="URL">Título</a> SOLO para citas reales de hallazgos web.
        - Responde siempre en español.

        DECIDIENDO SI VALE LA PENA UN DIAGRAMA
        Solo pide un diagrama si genuinamente ayuda a VISUALIZAR algo
        estructural/espacial/relacional/numérico del contenido ampliado (un
        proceso con pasos, una arquitectura, un flujo, o datos con valores
        exactos que graficar). Si el contenido ampliado es puramente
        conceptual/narrativo sin nada visualizable, deja tanto MathDiagram
        como DiagramPrompt en null — no inventes un diagrama "porque sí".

        MathDiagram VS DiagramPrompt (elige EXACTAMENTE UNO, nunca ambos)
        PRIMERO revisa si el diagrama entra en alguno de estos casos —
        TODOS con valores numéricos EXACTOS tomados del contenido — y si es
        así, usa SIEMPRE MathDiagram (nunca DiagramPrompt, ni siquiera
        aunque también pudieras describirlo en texto):
        - Circle: círculo (x-h)²+(y-k)²=r².
        - Ellipse: elipse (x-h)²/a²+(y-k)²/b²=1 con a≠b (usa Circle si a=b).
          NUNCA la aproximes con Kind=Points (marcar solo los vértices no
          dibuja ninguna curva).
        - Line: recta y=mx+b, o por dos puntos conocidos.
        - Parabola: y=Ax²+Bx+C.
        - Points: un conjunto pequeño de puntos (x,y) exactos que no encajan
          en ninguno de los anteriores.
        - BarChart: comparación de categorías discretas con un valor
          numérico cada una (ventas por mes, votos por candidato, etc.).
        - Histogram: distribución de frecuencia sobre bins numéricos
          continuos — barras SIN espacio entre ellas (a diferencia de
          BarChart). NUNCA la aproximes con Kind=Points ni Kind=BarChart.
        - PieChart: proporciones/porcentajes de un total (se normalizan
          automáticamente a 360° — no necesitan sumar 100 ya). Cualquier
          vez que el contenido dé un desglose porcentual/proporcional de un
          total (presupuesto, reparto, encuesta), esto ES un PieChart —
          NUNCA lo describas solo en DiagramPrompt.
        - LineChart: serie de datos real ordenada (p. ej. un valor a través
          del tiempo) conectada con segmentos — no una fórmula y=mx+b (usa
          Line para eso).
        - ScatterPlot: puntos (x,y) sin conectar mostrando correlación o
          distribución entre dos variables.
        - NumberLine: un valor o rango marcado sobre una recta numérica
          simple.
        Para CUALQUIERA de estos casos, llena MathDiagram con los números
        EXACTOS del contenido (nunca inventes valores no grounded en él) y
        deja DiagramPrompt en null.

        Solo usa DiagramPrompt cuando el diagrama NO tenga valores numéricos
        exactos que graficar — un proceso con pasos, una arquitectura, un
        diagrama conceptual puramente visual. Los modelos de generación de
        imágenes NO calculan, así que si terminas usando DiagramPrompt y
        aun así mencionas algún número/porcentaje incidental, ese valor debe
        ser exacto y consistente — pero en la práctica, cualquier diagrama
        con números reales casi siempre encaja en uno de los casos
        MathDiagram de arriba, así que prefiere siempre MathDiagram cuando
        haya duda.

        SALIDA
        Devuelve un JSON con: ExpandedContentHtml (string, HTML como se
        describió arriba), DiagramPrompt (string o null) y MathDiagram
        (objeto o null).
        """;

    private readonly AIAgent? _agent;

    public KnowledgeExpansionAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _agent = null;
            return;
        }

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        _agent = client
            .GetChatClient(deploymentName)
            .AsAIAgent(instructions: Instructions, name: "KnowledgeExpansionAgent");
    }

    public bool IsConfigured => _agent is not null;

    /// <param name="nodeName">Nombre del nodo (tema).</param>
    /// <param name="baseContent">El contenido base ya mostrado al alumno
    /// (típicamente el Content del step de Teaching) para que la ampliación
    /// no lo repita.</param>
    /// <param name="webFindings">Hallazgos de Bing Grounding, o null si no
    /// se buscó/no está configurado.</param>
    public async Task<KnowledgeExpansionResponse> ExpandAsync(
        string nodeName,
        string baseContent,
        string? webFindings,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "KnowledgeExpansionAgent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        var webFindingsSection = string.IsNullOrWhiteSpace(webFindings)
            ? "\n\n(No se encontraron hallazgos web actuales para este tema — usa solo tu conocimiento propio.)"
            : $"\n\nHallazgos web actuales (Bing Grounding):\n{webFindings}";

        var prompt =
            $"Tema del nodo: {nodeName}\n\n" +
            $"Contenido base YA mostrado al alumno (no lo repitas, amplíalo):\n{baseContent}" +
            webFindingsSection;

        var response = await _agent.RunAsync<KnowledgeExpansionResponse>(prompt, cancellationToken: cancellationToken);

        return response.Result;
    }
}
