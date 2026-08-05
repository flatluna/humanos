using System.Text.Json;
using HumanOS.Agents.Studio;
using Microsoft.Extensions.Configuration;

var settingsPath = Path.Combine("..", "HumanOS", "local.settings.json");
var json = File.ReadAllText(settingsPath);
using var doc = JsonDocument.Parse(json);
var values = doc.RootElement.GetProperty("Values");
var inMemory = new Dictionary<string, string?>();
foreach (var prop in values.EnumerateObject())
{
    inMemory[prop.Name] = prop.Value.GetString();
}

var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

var graphArchitect = new GraphArchitectAgent(configuration);
if (!graphArchitect.IsConfigured)
{
    Console.WriteLine("GraphArchitectAgent is NOT configured — check AzureOpenAIEndpoint/DeploymentName.");
    return;
}

// Exact chapter/subsection structure of the real Haven document, as
// produced by the new deterministic bare-numeric Tier 0 parser.
var documentChapterOrder = string.Join("\n", new[]
{
    "1. 1.1 Purpose of the Policy",
    "2. 1.2 Importance of Volunteers in the Program",
    "3. 2.0 Volunteer Requirements Before Serving",
    "4. 3.1 Companionship and Support During Group Activities",
    "5. 3.2 General Responsibilities",
    "6. 3.3 When a Volunteer May Lead Activities",
});

var corpus = new CuratedCorpus
{
    Summary = "The Haven Volunteer Orientation and Safeguarding Policy — foundational orientation, volunteer requirements, and companion/support role expectations.",
    Chunks =
    [
        new CuratedChunk
        {
            Tag = "Cap.1 1.1 Purpose of the Policy",
            Content = "Purpose: The Haven program is built on warmth, dignity, respect, and safety. Volunteers must keep gentle, loving boundaries so every person feels safe and cared for. This guide exists to help look after participants, volunteers, staff, families, and the heart of what The Haven stands for."
        },
        new CuratedChunk
        {
            Tag = "Cap.1 1.2 Importance of Volunteers in the Program",
            Content = "Volunteers are a blessing and a big part of what makes the community feel warm and welcoming for the friends served."
        },
        new CuratedChunk
        {
            Tag = "Cap.2 2.0 Volunteer Requirements Before Serving",
            Content = "Before serving, every volunteer must fill out a volunteer application, share information needed for a background check (including a Social Security number when required), attend orientation, read this policy, and sign the agreement at the end."
        },
        new CuratedChunk
        {
            Tag = "Cap.3 3.1 Companionship and Support During Group Activities",
            Content = "Volunteers act as kind, friendly companions who walk beside participants during group activities, helping one-on-one within the group — cheering a participant on, helping them stay engaged, gently supporting them."
        },
        new CuratedChunk
        {
            Tag = "Cap.3 3.2 General Responsibilities",
            Content = "The volunteer's role is to encourage, guide, gently rephrase, or softly clarify the instructions given by the teacher or leader of whatever activity is happening. Lean on program leadership rather than taking over."
        },
        new CuratedChunk
        {
            Tag = "Cap.3 3.3 When a Volunteer May Lead Activities",
            Content = "A volunteer may lead an activity, teach a project, or guide a class only when it has been talked through and approved ahead of time with program leadership, carrying The Haven's spirit of safety, encouragement, inclusion, and joyful participation."
        },
    ]
};

Console.WriteLine("Calling GraphArchitectAgent.ExtractGraphAsync with StrictSyllabusMode = true ...");
var result = await graphArchitect.ExtractGraphAsync(
    capabilityName: "The Haven Volunteer Orientation and Safeguarding Policy",
    curatedCorpus: corpus,
    documentChapterOrder: documentChapterOrder,
    programContext: null,
    languageCode: "en",
    strictSyllabusMode: true);

Console.WriteLine();
Console.WriteLine($"Graph Name: {result.Graph.Name}");
Console.WriteLine($"Node count: {result.Graph.Nodes.Count}");
Console.WriteLine();
foreach (var node in result.Graph.Nodes)
{
    Console.WriteLine($"- {node.Name}");
}
