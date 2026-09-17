using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Contracts.ProcessDocuments;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace HumanOS.Services.ProcessDocuments;

/// <summary>
/// Extracts readable text, tables, layout elements, and structural hierarchy
/// (Chapters, Modules, Phases, Process Steps, Substeps, Objectives, Skills,
/// Deliverables, Completion Conditions, Checklists, Appendices) from a PDF process document.
/// Uses a two-tier extraction pipeline:
/// 1. Page-by-page text & layout extraction via PdfPig with fast deterministic heuristics.
/// 2. If the document is un-indexed or complex (fewer than 2 steps detected), leverages
///    Azure OpenAI to extract/infer complete Chapters, Steps, Activities, Tools, and Criteria.
/// </summary>
public sealed partial class ProcessDocumentExtractionService : IProcessDocumentExtractionService
{
    private const string AiExtractionInstructions = """
        You are an expert workforce capability and process intelligence extraction agent.
        Your goal is to analyze the extracted text of ANY business process document, operational manual, standard operating procedure (SOP), or corporate policy, and transform it into a complete, logical operational hierarchy of Chapters and Work Steps.

        CRITICAL ARCHITECTURAL RULES:
        1. PROCESS & POLICY IDENTIFICATION:
           - Extract the true title and high-level purpose/description of the process or policy.
           - Do NOT mistake introductory metadata labels (such as "Purpose:", "Objetivo:", "Scope:", "Document ID:") for a chapter or process step.
        2. CHAPTERS / PHASES (Top-level Grouping):
           - Group steps into coherent Chapters or Phases (e.g. "Phase 1: Travel Request and Authorization", "Phase 2: Booking and Transportation", or numbered chapters).
           - If the document does not use explicit chapters, create 1 to 3 logical chapters/phases that group the steps coherently.
        3. EXTRACT ALL WORK STEPS / POLICY CLAUSES:
           - Extract EVERY operational work step, policy rule, approval stage, or procedural requirement as an individual Step in reading order.
           - For numbered policies (e.g. "1. Travel Request and Business Justification", "2. Approval Matrix", "3. Air Travel Policy", "4. Hotel Policy", "5. Meals and Per Diem", "6. Alcohol Consumption Policy"...), EACH numbered section is a DISTINCT STEP! NEVER omit, compress, or merge numbered steps into one.
        4. STEP DETAILS:
           - "stepNumber": The section/step number (e.g., "1", "2", "3").
           - "stepName": Clear, professional title of the step or policy clause.
           - "objective": The operational goal or compliance intent of this specific step.
           - "requiredActivities": An exhaustive list of the specific actions, rules, responsibilities, or bullet points the employee must execute or follow in this step.
           - "requiredTools": Software, applications, communication channels, or templates mentioned (e.g., Concur, SAP, Amex Corporate, Email, Excel, ERP, etc.).
           - "deliverables": Tangible work artifacts, forms, receipts, or outputs generated.
           - "completionCondition": Conditions or compliance criteria for this step to be considered finished.
           - "sourcePage": Estimated page number where this step appears in the document.
        """;

    private readonly AIAgent? _agent;
    private readonly ILogger<ProcessDocumentExtractionService>? _logger;

    public ProcessDocumentExtractionService(
        IConfiguration? configuration = null,
        ILogger<ProcessDocumentExtractionService>? logger = null)
    {
        _logger = logger;

        if (configuration is null) return;

        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIEconomyDeploymentName"] ?? configuration["AzureOpenAIDeploymentName"];
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
            .AsAIAgent(instructions: AiExtractionInstructions, name: "ProcessDocumentExtractionAgent");
    }

    public bool IsAiConfigured => _agent is not null;

    private enum StepSubSectionState
    {
        None,
        Objective,
        Activities,
        Skills,
        Deliverables,
        CompletionCondition,
        Checklist
    }

    [GeneratedRegex(@"^(cap[ií]tulo|chapter)\s+(\d+|[ivxlcdm]+)[\s:\.\-]*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex ChapterRegex();

    [GeneratedRegex(@"^(m[oó]dulo|module)\s+(\d+|[ivxlcdm]+)[\s:\.\-]*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex ModuleRegex();

    [GeneratedRegex(@"^(fase|phase)\s+(\d+|[ivxlcdm]+)[\s:\.\-]*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex PhaseRegex();

    [GeneratedRegex(@"^(\d+[\.\-\)]\s*)?(paso|step)\s+(\d+(\.\d+)*)[\s:\.\-]*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex ExplicitStepRegex();

    [GeneratedRegex(@"^(objetivo(\s+del\s+paso)?|objective(\s+of\s+the\s+step)?|goal|prop[oó]sito|meta|purpose|alcance|scope|descripci[oó]n(\s+del\s+paso)?)[\s:\-]*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex ObjectiveHeaderRegex();

    [GeneratedRegex(@"^(actividades(\s+requeridas)?|required\s+activities|activities|tareas(\s+requeridas)?|tasks|qu[eé]\s+debe\s+hacer(\s+el\s+[a-z\s]+)?|what\s+the\s+[a-z\s]+\s+must\s+do|what\s+to\s+do|acciones(\s+a\s+realizar)?|actions|responsabilidades|responsibilities|procedimiento|procedure|instrucciones|instructions|pasos\s+a\s+seguir|antes\s+de\s+la\s+reuni[oó]n|before\s+the\s+meeting|durante\s+la\s+reuni[oó]n|during\s+the\s+meeting|despu[eé]s\s+de\s+la\s+reuni[oó]n|after\s+the\s+meeting)[\s:\-]*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex ActivitiesHeaderRegex();

    [GeneratedRegex(@"^(habilidades(\s+requeridas)?|required\s+skills|skills|competencias(\s+requeridas)?|capacidades|capabilities|knowledge\s+and\s+skills)[\s:\-]*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex SkillsHeaderRegex();

    [GeneratedRegex(@"^(entregables(\s+esperados)?|deliverables|expected\s+deliverables|outputs|resultados(\s+esperados)?|artefactos|artifacts|productos)[\s:\-]*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex DeliverablesHeaderRegex();

    [GeneratedRegex(@"^(condici[oó]n\s+de\s+finalizaci[oó]n|criterio(s)?\s+de\s+[eé]xito|completion\s+condition|completion\s+criteria|success\s+criteria|criterio(s)?\s+de\s+aceptaci[oó]n|acceptance\s+criteria|definition\s+of\s+done|criterio(s)?\s+de\s+terminaci[oó]n|completado\s+cuando|complete\s+when)[\s:\-]*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex CompletionConditionHeaderRegex();

    [GeneratedRegex(@"^(\d+)\.(\d+)[\s:\.\-]+(.*)$")]
    private static partial Regex NumberedSubSectionRegex();

    [GeneratedRegex(@"^(\d+)\.[\s:\-]*(.*)$")]
    private static partial Regex TopLevelNumberedRegex();

    [GeneratedRegex(@"^(anexo|ap[eé]ndice|appendix)\s*([a-z0-9]*)[\s:\.\-]*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex AppendixRegex();

    [GeneratedRegex(@"^\[([ xX✓✔])\]\s*(.*)$")]
    private static partial Regex CheckboxBracketRegex();

    [GeneratedRegex(@"^[☐☑✓✔]\s*(.*)$")]
    private static partial Regex CheckboxSymbolRegex();

    [GeneratedRegex(@"^[\-\*•]\s*(.*)$")]
    private static partial Regex BulletListRegex();

    public async Task<(RawDocumentExtractionResult Raw, ProcessDocumentStructure Structured)> ExtractAndStructureAsync(
        byte[] pdfBytes,
        string fileName,
        string documentId,
        string? providedProcessName,
        string? processDescription,
        string sourceStoragePath,
        CancellationToken cancellationToken = default)
    {
        if (pdfBytes == null || pdfBytes.Length == 0)
        {
            throw new ArgumentException("PDF content cannot be null or empty.", nameof(pdfBytes));
        }

        using var memoryStream = new MemoryStream(pdfBytes);
        using var pdfDocument = PdfDocument.Open(memoryStream);

        var rawResult = ExtractRawDocument(pdfDocument, documentId);
        var structured = TransformToProcessStructure(
            rawResult,
            pdfDocument,
            fileName,
            documentId,
            providedProcessName,
            processDescription,
            sourceStoragePath);

        // If deterministic regex found fewer than 2 steps and AI extraction agent is available,
        // use the AI model to intelligently extract the full process/policy/manual structure!
        var totalSteps = structured.Chapters.Sum(c => c.Steps.Count);
        if (totalSteps < 2 && _agent is not null)
        {
            _logger?.LogInformation(
                "Document {DocumentId} produced only {Steps} steps with regex; running AI Process Structure Extractor...",
                documentId,
                totalSteps);

            var aiStructured = await ExtractStructureWithAiAsync(
                rawResult,
                documentId,
                providedProcessName,
                processDescription,
                fileName,
                sourceStoragePath,
                cancellationToken);

            if (aiStructured != null && aiStructured.Chapters.Sum(c => c.Steps.Count) > 0)
            {
                // Preserve tables and appendices extracted from the PDF
                aiStructured.Tables = structured.Tables;
                aiStructured.Appendices = structured.Appendices;
                return (rawResult, aiStructured);
            }
        }

        return (rawResult, structured);
    }

    private async Task<ProcessDocumentStructure?> ExtractStructureWithAiAsync(
        RawDocumentExtractionResult rawResult,
        string documentId,
        string? providedProcessName,
        string? processDescription,
        string fileName,
        string sourceStoragePath,
        CancellationToken cancellationToken)
    {
        if (_agent is null) return null;

        var sb = new StringBuilder();
        sb.AppendLine("Analyze the extracted text of the following business process/policy document and extract its complete structure (Chapters, Work Steps, Activities, Tools, Deliverables, Conditions):");
        sb.AppendLine($"File Name: {fileName}");
        if (!string.IsNullOrWhiteSpace(providedProcessName)) sb.AppendLine($"Suggested Process Name: {providedProcessName}");
        if (!string.IsNullOrWhiteSpace(processDescription)) sb.AppendLine($"Suggested Description: {processDescription}");
        sb.AppendLine("\n=== EXTRACTED DOCUMENT CONTENT (PAGE BY PAGE) ===");

        foreach (var p in rawResult.Paragraphs)
        {
            if (!string.IsNullOrWhiteSpace(p.Content))
            {
                sb.AppendLine($"[Page {p.PageNumber}] {p.Content}");
            }
        }
        sb.AppendLine("=== END OF CONTENT ===");

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = await _agent.RunAsync<ProcessStructureAiResponse>(
                sb.ToString(),
                cancellationToken: cancellationToken);

            stopwatch.Stop();
            var aiResult = response.Result;
            if (aiResult == null || aiResult.Chapters.Count == 0) return null;

            var chapters = new List<ProcessChapter>();
            int stepCounter = 1;

            foreach (var chAi in aiResult.Chapters)
            {
                var ch = new ProcessChapter
                {
                    ChapterId = $"ch-{chapters.Count + 1}",
                    ChapterNumber = string.IsNullOrWhiteSpace(chAi.ChapterNumber) ? (chapters.Count + 1).ToString() : chAi.ChapterNumber,
                    Title = !string.IsNullOrWhiteSpace(chAi.Title) ? chAi.Title : $"Fase {chapters.Count + 1}",
                    Description = chAi.Description ?? string.Empty,
                    Steps = []
                };

                foreach (var stAi in chAi.Steps)
                {
                    var step = new ProcessStep
                    {
                        StepId = $"step-{stepCounter}",
                        StepNumber = !string.IsNullOrWhiteSpace(stAi.StepNumber) ? stAi.StepNumber : stepCounter.ToString(),
                        StepName = stAi.StepName,
                        Objective = stAi.Objective,
                        RequiredActivities = stAi.RequiredActivities ?? [],
                        Deliverables = stAi.Deliverables ?? [],
                        CompletionCondition = stAi.CompletionCondition ?? string.Empty,
                        SourcePages = stAi.SourcePage > 0 ? [stAi.SourcePage] : [1]
                    };
                    stepCounter++;
                    ch.Steps.Add(step);
                }

                chapters.Add(ch);
            }

            var procName = !string.IsNullOrWhiteSpace(providedProcessName) ? providedProcessName
                         : (!string.IsNullOrWhiteSpace(aiResult.ProcessName) ? aiResult.ProcessName : Path.GetFileNameWithoutExtension(fileName));

            _logger?.LogInformation(
                "AI Process Extractor successfully derived {Chapters} chapters and {Steps} steps for document {DocumentId} in {Ms}ms",
                chapters.Count,
                stepCounter - 1,
                documentId,
                stopwatch.ElapsedMilliseconds);

            return new ProcessDocumentStructure
            {
                DocumentId = documentId,
                ProcessName = procName,
                DocumentTitle = procName,
                SourceFile = new SourceFileInfo
                {
                    FileName = fileName,
                    StoragePath = sourceStoragePath,
                    ContentType = "application/pdf"
                },
                Chapters = chapters,
                Tables = [],
                Appendices = [],
                Warnings = [],
                ProcessingStatus = "completed"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to extract process structure with AI agent for document {DocumentId}", documentId);
            return null;
        }
    }

    private static RawDocumentExtractionResult ExtractRawDocument(PdfDocument pdfDocument, string documentId)
    {
        var raw = new RawDocumentExtractionResult
        {
            DocumentId = documentId,
            PageCount = pdfDocument.NumberOfPages
        };

        var fullContentBuilder = new StringBuilder();

        foreach (var page in pdfDocument.GetPages())
        {
            var pageNum = page.Number;
            var pageText = page.Text ?? string.Empty;

            // Extract lines: prefer splitting page.Text by newlines; if absent/single-line, group words by Y coordinate
            List<string> lines;
            var rawLines = pageText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (rawLines.Length > 1)
            {
                lines = rawLines.ToList();
            }
            else
            {
                var words = page.GetWords().ToList();
                if (words.Count > 0)
                {
                    lines = words
                        .GroupBy(w => Math.Round(w.BoundingBox.Bottom / 4.0) * 4.0)
                        .OrderByDescending(g => g.Key)
                        .Select(g => string.Join(" ", g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)).Trim())
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToList();
                }
                else
                {
                    lines = rawLines.ToList();
                }
            }

            fullContentBuilder.AppendLine($"--- Page {pageNum} ---");
            foreach (var l in lines)
            {
                fullContentBuilder.AppendLine(l);
            }

            var inTableBlock = false;
            var currentTableLines = new List<string>();

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                // Detect table rows (e.g. pipe delimited or tab/multiple column formatted)
                if (IsTableRow(line))
                {
                    inTableBlock = true;
                    currentTableLines.Add(line);
                    continue;
                }
                else if (inTableBlock)
                {
                    // End of table block
                    if (currentTableLines.Count >= 2)
                    {
                        var rawTable = ParseRawTable(currentTableLines, pageNum);
                        if (rawTable != null)
                        {
                            raw.Tables.Add(rawTable);
                        }
                    }
                    inTableBlock = false;
                    currentTableLines.Clear();
                }

                // Check for selection marks (checkboxes)
                var boxMatch = CheckboxBracketRegex().Match(line);
                if (boxMatch.Success)
                {
                    var isChecked = !string.IsNullOrWhiteSpace(boxMatch.Groups[1].Value);
                    raw.SelectionMarks.Add(new RawSelectionMark
                    {
                        State = isChecked ? "selected" : "unselected",
                        Label = boxMatch.Groups[2].Value.Trim(),
                        PageNumber = pageNum
                    });
                }
                else
                {
                    var symbolMatch = CheckboxSymbolRegex().Match(line);
                    if (symbolMatch.Success)
                    {
                        var isChecked = line.StartsWith('☑') || line.StartsWith('✓') || line.StartsWith('✔');
                        raw.SelectionMarks.Add(new RawSelectionMark
                        {
                            State = isChecked ? "selected" : "unselected",
                            Label = symbolMatch.Groups[1].Value.Trim(),
                            PageNumber = pageNum
                        });
                    }
                }

                // Check for bullet lists
                var bulletMatch = BulletListRegex().Match(line);
                if (bulletMatch.Success)
                {
                    raw.Lists.Add(new RawList
                    {
                        Items = [bulletMatch.Groups[1].Value.Trim()],
                        PageNumber = pageNum
                    });
                }

                // Paragraph role detection (heading vs regular paragraph)
                var isHeading = IsHeadingCandidate(line);
                var role = isHeading ? (i == 0 && pageNum == 1 ? "title" : "sectionHeading") : null;

                if (isHeading)
                {
                    raw.Headings.Add(new RawHeading
                    {
                        Level = DetermineHeadingLevel(line),
                        Text = line,
                        PageNumber = pageNum
                    });
                }

                raw.Paragraphs.Add(new RawParagraph
                {
                    Content = line,
                    Role = role,
                    PageNumber = pageNum
                });
            }

            if (inTableBlock && currentTableLines.Count >= 2)
            {
                var rawTable = ParseRawTable(currentTableLines, pageNum);
                if (rawTable != null)
                {
                    raw.Tables.Add(rawTable);
                }
            }
        }

        raw.Content = fullContentBuilder.ToString();
        return raw;
    }

    private static bool IsTableRow(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;

        // Pipe table format: "| Col1 | Col2 |"
        if (line.Contains('|') && line.Count(c => c == '|') >= 2)
        {
            // Ignore separator lines like "|---|---|"
            var withoutPipes = line.Replace("|", "").Replace("-", "").Replace(":", "").Trim();
            return withoutPipes.Length > 0 || line.Contains("---");
        }

        // Tab delimited or double/triple space separated tabular layout with at least 2 columns
        var parts = Regex.Split(line, @"\t+|\s{3,}");
        return parts.Length >= 2 && !line.StartsWith('#') && !line.StartsWith('*');
    }

    private static RawTable? ParseRawTable(List<string> lines, int pageNum)
    {
        var cleanedLines = lines.Where(l => !Regex.IsMatch(l, @"^[\s\|\-:]+$")).ToList();
        if (cleanedLines.Count == 0) return null;

        var rows = new List<List<string>>();
        foreach (var line in cleanedLines)
        {
            List<string> cells;
            if (line.Contains('|'))
            {
                cells = line.Split('|', StringSplitOptions.TrimEntries)
                    .Where((_, idx) => idx > 0 || !line.StartsWith('|'))
                    .Where(c => !string.IsNullOrEmpty(c))
                    .ToList();
            }
            else
            {
                cells = Regex.Split(line, @"\t+|\s{3,}")
                    .Select(c => c.Trim())
                    .Where(c => !string.IsNullOrEmpty(c))
                    .ToList();
            }

            if (cells.Count > 0)
            {
                rows.Add(cells);
            }
        }

        if (rows.Count == 0) return null;

        var headers = rows[0];
        var dataRows = rows.Skip(1).ToList();

        return new RawTable
        {
            PageNumber = pageNum,
            RowCount = rows.Count,
            ColumnCount = headers.Count,
            Headers = headers,
            Rows = dataRows
        };
    }

    private static bool IsHeadingCandidate(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length > 150) return false;
        var clean = StripMarkdownAndDecorations(line);

        if (ChapterRegex().IsMatch(clean) ||
            ModuleRegex().IsMatch(clean) ||
            PhaseRegex().IsMatch(clean) ||
            ExplicitStepRegex().IsMatch(clean) ||
            AppendixRegex().IsMatch(clean) ||
            ObjectiveHeaderRegex().IsMatch(clean) ||
            ActivitiesHeaderRegex().IsMatch(clean) ||
            SkillsHeaderRegex().IsMatch(clean) ||
            DeliverablesHeaderRegex().IsMatch(clean) ||
            CompletionConditionHeaderRegex().IsMatch(clean))
        {
            return true;
        }

        if (line.StartsWith('#')) return true;

        // ALL CAPS heading candidate of reasonable length
        if (clean.Length >= 4 && clean == clean.ToUpperInvariant() && Regex.IsMatch(clean, @"[A-Z]"))
        {
            return true;
        }

        // Title prefix keywords
        if (Regex.IsMatch(clean, @"^(proceso|procedimiento|manual|gu[ií]a|instrucci[oó]n|politica|pol[ií]tica)[\s:\-]", RegexOptions.IgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static int DetermineHeadingLevel(string line)
    {
        if (line.StartsWith("###")) return 3;
        if (line.StartsWith("##")) return 2;
        if (line.StartsWith('#')) return 1;

        if (ChapterRegex().IsMatch(line) || ModuleRegex().IsMatch(line)) return 1;
        if (PhaseRegex().IsMatch(line)) return 2;
        if (ExplicitStepRegex().IsMatch(line)) return 3;
        return 2;
    }

    private static ProcessDocumentStructure TransformToProcessStructure(
        RawDocumentExtractionResult raw,
        PdfDocument pdfDocument,
        string fileName,
        string documentId,
        string? providedProcessName,
        string? processDescription,
        string sourceStoragePath)
    {
        var warnings = new List<string>();

        // 1. Identify document title
        var detectedTitle = ExtractDocumentTitle(raw, pdfDocument);
        var documentTitle = !string.IsNullOrWhiteSpace(detectedTitle) ? detectedTitle : "Documento de Proceso";

        // Do not use the PDF filename as process name when document provides a clear title.
        string processName;
        if (!string.IsNullOrWhiteSpace(providedProcessName))
        {
            processName = providedProcessName.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(detectedTitle))
        {
            processName = detectedTitle;
        }
        else
        {
            processName = Path.GetFileNameWithoutExtension(fileName);
            warnings.Add("No clear document title was detected in the document; used file name as process name fallback.");
        }

        // 2. Extract Tables
        var structuredTables = new List<ProcessTable>();
        var tableIdx = 1;
        foreach (var t in raw.Tables)
        {
            structuredTables.Add(new ProcessTable
            {
                TableId = $"tbl-{tableIdx++}",
                Title = t.Headers.Count > 0 ? string.Join(" | ", t.Headers.Take(2)) : $"Tabla p.{t.PageNumber}",
                Headers = t.Headers,
                Rows = t.Rows,
                SourcePage = t.PageNumber
            });
        }

        // 3. Process lines to identify Chapters, Modules, Phases, Steps, Substeps, Appendices
        var chapters = new List<ProcessChapter>();
        var appendices = new List<ProcessAppendix>();

        ProcessChapter? currentChapter = null;
        ProcessModule? currentModule = null;
        ProcessPhase? currentPhase = null;
        ProcessStep? currentStep = null;

        var allLinesWithPages = new List<(string Line, int Page)>();
        foreach (var p in raw.Paragraphs)
        {
            if (!string.IsNullOrWhiteSpace(p.Content))
            {
                allLinesWithPages.Add((p.Content, p.PageNumber));
            }
        }

        var totalStepsFound = 0;
        var hasClearProcessStructure = false;
        var currentSubSection = StepSubSectionState.None;
        string? pendingActivityNumber = null;
        string? pendingSkillName = null;

        for (var i = 0; i < allLinesWithPages.Count; i++)
        {
            var (rawLine, pageNum) = allLinesWithPages[i];
            if (string.IsNullOrWhiteSpace(rawLine)) continue;

            var cleanLine = StripMarkdownAndDecorations(rawLine);
            if (string.IsNullOrWhiteSpace(cleanLine)) continue;

            // Check Appendix
            var appendixMatch = AppendixRegex().Match(cleanLine);
            if (appendixMatch.Success)
            {
                currentSubSection = StepSubSectionState.None;
                currentStep = null;
                pendingActivityNumber = null;
                pendingSkillName = null;

                var appTitle = cleanLine.Trim();
                var appContentBuilder = new StringBuilder();
                var appPages = new HashSet<int> { pageNum };

                // Collect following lines until another top-level section
                while (i + 1 < allLinesWithPages.Count)
                {
                    var nextClean = StripMarkdownAndDecorations(allLinesWithPages[i + 1].Line);
                    if (ChapterRegex().IsMatch(nextClean) ||
                        ExplicitStepRegex().IsMatch(nextClean) ||
                        AppendixRegex().IsMatch(nextClean))
                    {
                        break;
                    }
                    i++;
                    appContentBuilder.AppendLine(allLinesWithPages[i].Line);
                    appPages.Add(allLinesWithPages[i].Page);
                }

                appendices.Add(new ProcessAppendix
                {
                    AppendixId = $"app-{appendices.Count + 1}",
                    Title = appTitle,
                    Content = appContentBuilder.ToString().Trim(),
                    SourcePages = appPages.OrderBy(p => p).ToList()
                });
                continue;
            }

            // Check Chapter
            var chapterMatch = ChapterRegex().Match(cleanLine);
            if (chapterMatch.Success)
            {
                hasClearProcessStructure = true;
                currentSubSection = StepSubSectionState.None;
                currentStep = null;
                pendingActivityNumber = null;
                pendingSkillName = null;

                currentChapter = new ProcessChapter
                {
                    ChapterId = $"ch-{chapters.Count + 1}",
                    ChapterNumber = chapterMatch.Groups[2].Value,
                    Title = string.IsNullOrWhiteSpace(chapterMatch.Groups[3].Value) ? cleanLine : chapterMatch.Groups[3].Value.Trim(),
                    Description = string.Empty
                };
                chapters.Add(currentChapter);
                currentModule = null;
                currentPhase = null;
                continue;
            }

            // Check Module
            var moduleMatch = ModuleRegex().Match(cleanLine);
            if (moduleMatch.Success)
            {
                hasClearProcessStructure = true;
                currentSubSection = StepSubSectionState.None;
                currentStep = null;
                pendingActivityNumber = null;
                pendingSkillName = null;

                if (currentChapter == null)
                {
                    currentChapter = CreateDefaultChapter(chapters.Count + 1);
                    chapters.Add(currentChapter);
                }

                currentModule = new ProcessModule
                {
                    ModuleId = $"mod-{currentChapter.Modules.Count + 1}",
                    ModuleNumber = moduleMatch.Groups[2].Value,
                    Title = string.IsNullOrWhiteSpace(moduleMatch.Groups[3].Value) ? cleanLine : moduleMatch.Groups[3].Value.Trim()
                };
                currentChapter.Modules.Add(currentModule);
                currentPhase = null;
                continue;
            }

            // Check Phase
            var phaseMatch = PhaseRegex().Match(cleanLine);
            if (phaseMatch.Success)
            {
                hasClearProcessStructure = true;
                currentSubSection = StepSubSectionState.None;
                currentStep = null;
                pendingActivityNumber = null;
                pendingSkillName = null;

                if (currentChapter == null)
                {
                    currentChapter = CreateDefaultChapter(chapters.Count + 1);
                    chapters.Add(currentChapter);
                }

                currentPhase = new ProcessPhase
                {
                    PhaseId = $"ph-{((currentModule?.Phases.Count ?? 0) + 1)}",
                    PhaseNumber = phaseMatch.Groups[2].Value,
                    Name = string.IsNullOrWhiteSpace(phaseMatch.Groups[3].Value) ? cleanLine : phaseMatch.Groups[3].Value.Trim()
                };

                if (currentModule != null)
                {
                    currentModule.Phases.Add(currentPhase);
                }
                continue;
            }

            // Check Step
            // Rule: "Do not interpret a numbered list as a process automatically unless the surrounding context supports it."
            var isExplicitStep = ExplicitStepRegex().Match(cleanLine);
            var isNumberedStepCandidate = isExplicitStep.Success
                ? false
                : (currentSubSection == StepSubSectionState.None && IsContextualProcessStep(cleanLine, allLinesWithPages, i));

            if (isExplicitStep.Success || isNumberedStepCandidate)
            {
                hasClearProcessStructure = true;
                currentSubSection = StepSubSectionState.None;
                pendingActivityNumber = null;
                pendingSkillName = null;

                if (currentChapter == null)
                {
                    currentChapter = CreateDefaultChapter(chapters.Count + 1);
                    chapters.Add(currentChapter);
                }

                totalStepsFound++;
                var stepNum = isExplicitStep.Success
                    ? isExplicitStep.Groups[3].Value
                    : (TopLevelNumberedRegex().Match(cleanLine).Groups[1].Value);
                var stepName = isExplicitStep.Success
                    ? (string.IsNullOrWhiteSpace(isExplicitStep.Groups[5].Value) ? cleanLine : isExplicitStep.Groups[5].Value.Trim())
                    : ExtractStepNameFromNumberedLine(cleanLine);

                currentStep = new ProcessStep
                {
                    StepId = $"step-{totalStepsFound}",
                    StepNumber = string.IsNullOrWhiteSpace(stepNum) ? totalStepsFound.ToString() : stepNum,
                    StepName = stepName,
                    SourcePages = [pageNum]
                };
                currentChapter.Steps.Add(currentStep);
                continue;
            }

            // If we are inside an active step, extract its substeps, objectives, activities, skills, deliverables, completion conditions, checklists
            if (currentStep != null)
            {
                if (!currentStep.SourcePages.Contains(pageNum))
                {
                    currentStep.SourcePages.Add(pageNum);
                }

                // Check for Objectives
                var objMatch = ObjectiveHeaderRegex().Match(cleanLine);
                if (objMatch.Success)
                {
                    currentSubSection = StepSubSectionState.Objective;
                    pendingActivityNumber = null;
                    pendingSkillName = null;
                    var remainder = objMatch.Groups[objMatch.Groups.Count - 1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(remainder))
                    {
                        currentStep.Objective = remainder;
                    }
                    continue;
                }

                // Check for Required Activities
                var actMatch = ActivitiesHeaderRegex().Match(cleanLine);
                if (actMatch.Success)
                {
                    currentSubSection = StepSubSectionState.Activities;
                    pendingActivityNumber = null;
                    pendingSkillName = null;
                    var remainder = actMatch.Groups[actMatch.Groups.Count - 1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(remainder))
                    {
                        currentStep.RequiredActivities.Add(remainder);
                    }
                    continue;
                }

                // Check for Required Skills
                var skillMatch = SkillsHeaderRegex().Match(cleanLine);
                if (skillMatch.Success)
                {
                    currentSubSection = StepSubSectionState.Skills;
                    pendingActivityNumber = null;
                    pendingSkillName = null;
                    var remainder = skillMatch.Groups[skillMatch.Groups.Count - 1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(remainder))
                    {
                        var skills = remainder.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        currentStep.RequiredSkills.AddRange(skills);
                    }
                    continue;
                }

                // Check for Deliverables
                var delivMatch = DeliverablesHeaderRegex().Match(cleanLine);
                if (delivMatch.Success)
                {
                    currentSubSection = StepSubSectionState.Deliverables;
                    pendingActivityNumber = null;
                    pendingSkillName = null;
                    var remainder = delivMatch.Groups[delivMatch.Groups.Count - 1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(remainder))
                    {
                        var delivs = remainder.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        currentStep.Deliverables.AddRange(delivs);
                    }
                    continue;
                }

                // Check for Completion Condition
                var compMatch = CompletionConditionHeaderRegex().Match(cleanLine);
                if (compMatch.Success)
                {
                    currentSubSection = StepSubSectionState.CompletionCondition;
                    pendingActivityNumber = null;
                    pendingSkillName = null;
                    var remainder = compMatch.Groups[compMatch.Groups.Count - 1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(remainder))
                    {
                        currentStep.CompletionCondition = remainder;
                    }
                    continue;
                }

                // Check for Checklists: checkboxes [ ], [x], ☐, ☑
                var checkMatch = CheckboxBracketRegex().Match(cleanLine);
                if (checkMatch.Success)
                {
                    currentStep.Checklists.Add(checkMatch.Groups[2].Value.Trim());
                    continue;
                }
                var symCheckMatch = CheckboxSymbolRegex().Match(cleanLine);
                if (symCheckMatch.Success)
                {
                    currentStep.Checklists.Add(symCheckMatch.Groups[1].Value.Trim());
                    continue;
                }

                // Handle lines according to current sub-section state
                if (currentSubSection == StepSubSectionState.Objective)
                {
                    if (!IsHeadingCandidate(cleanLine))
                    {
                        if (string.IsNullOrWhiteSpace(currentStep.Objective))
                        {
                            currentStep.Objective = cleanLine;
                        }
                        else
                        {
                            currentStep.Objective += " " + cleanLine;
                        }
                        continue;
                    }
                }
                else if (currentSubSection == StepSubSectionState.Activities)
                {
                    var numItemMatch = TopLevelNumberedRegex().Match(cleanLine);
                    if (numItemMatch.Success)
                    {
                        var actText = numItemMatch.Groups[1].Value.Trim();
                        // In TopLevelNumberedRegex: group 1 is digits, group 2 is remainder text
                        var restText = numItemMatch.Groups.Count > 1 ? numItemMatch.Groups[numItemMatch.Groups.Count - 1].Value.Trim() : string.Empty;
                        if (!string.IsNullOrWhiteSpace(restText) && restText != actText)
                        {
                            currentStep.RequiredActivities.Add(restText);
                            pendingActivityNumber = null;
                        }
                        else
                        {
                            pendingActivityNumber = actText;
                        }
                        continue;
                    }

                    if (pendingActivityNumber != null)
                    {
                        currentStep.RequiredActivities.Add(cleanLine);
                        pendingActivityNumber = null;
                        continue;
                    }

                    var bullet = BulletListRegex().Match(cleanLine);
                    if (bullet.Success)
                    {
                        currentStep.RequiredActivities.Add(bullet.Groups[1].Value.Trim());
                        continue;
                    }

                    var subMatch = NumberedSubSectionRegex().Match(cleanLine);
                    if (subMatch.Success)
                    {
                        currentStep.Substeps.Add(new ProcessSubstep
                        {
                            SubstepId = $"{currentStep.StepId}-{currentStep.Substeps.Count + 1}",
                            SubstepNumber = $"{subMatch.Groups[1].Value}.{subMatch.Groups[2].Value}",
                            Name = subMatch.Groups[3].Value.Trim(),
                            Description = string.Empty
                        });
                        continue;
                    }

                    // Continuation of previous activity or plain text activity
                    if (!IsHeadingCandidate(cleanLine))
                    {
                        if (currentStep.RequiredActivities.Count > 0 && !cleanLine.EndsWith('.'))
                        {
                            currentStep.RequiredActivities[^1] += " " + cleanLine;
                        }
                        else
                        {
                            currentStep.RequiredActivities.Add(cleanLine);
                        }
                        continue;
                    }
                }
                else if (currentSubSection == StepSubSectionState.Skills)
                {
                    // Ignore common table header titles
                    if (Regex.IsMatch(cleanLine, @"^(skill|what\s+the\s+[a-z\s]+\s+must\s+be\s+able\s+to\s+do|habilidad|competencia|descripci[oó]n)$", RegexOptions.IgnoreCase))
                    {
                        continue;
                    }

                    var bullet = BulletListRegex().Match(cleanLine);
                    var skillText = bullet.Success ? bullet.Groups[1].Value.Trim() : cleanLine;

                    var colonIdx = skillText.IndexOf(':');
                    var dashIdx = skillText.IndexOf('-');
                    var sepIdx = colonIdx > 0 ? colonIdx : dashIdx;

                    if (sepIdx > 0 && sepIdx < 45 && sepIdx < skillText.Length - 1)
                    {
                        currentStep.RequiredSkills.Add(skillText);
                        pendingSkillName = null;
                        continue;
                    }

                    if (pendingSkillName == null)
                    {
                        if (skillText.Length < 50 && !skillText.EndsWith('.'))
                        {
                            pendingSkillName = skillText;
                            continue;
                        }
                        else
                        {
                            currentStep.RequiredSkills.Add(skillText);
                            continue;
                        }
                    }
                    else
                    {
                        currentStep.RequiredSkills.Add($"{pendingSkillName}: {skillText}");
                        pendingSkillName = null;
                        continue;
                    }
                }
                else if (currentSubSection == StepSubSectionState.Deliverables)
                {
                    var bullet = BulletListRegex().Match(cleanLine);
                    var item = bullet.Success ? bullet.Groups[1].Value.Trim() : cleanLine;
                    if (!IsHeadingCandidate(cleanLine))
                    {
                        currentStep.Deliverables.Add(item);
                        continue;
                    }
                }
                else if (currentSubSection == StepSubSectionState.CompletionCondition)
                {
                    if (!IsHeadingCandidate(cleanLine))
                    {
                        if (string.IsNullOrWhiteSpace(currentStep.CompletionCondition))
                        {
                            currentStep.CompletionCondition = cleanLine;
                        }
                        else
                        {
                            currentStep.CompletionCondition += " " + cleanLine;
                        }
                        continue;
                    }
                }

                // If not in an active sub-section: Check for Substeps: e.g. "1.1", "Paso 1.a", "a)"
                var subMatchFallback = NumberedSubSectionRegex().Match(cleanLine);
                if (subMatchFallback.Success)
                {
                    currentStep.Substeps.Add(new ProcessSubstep
                    {
                        SubstepId = $"{currentStep.StepId}-{currentStep.Substeps.Count + 1}",
                        SubstepNumber = $"{subMatchFallback.Groups[1].Value}.{subMatchFallback.Groups[2].Value}",
                        Name = subMatchFallback.Groups[3].Value.Trim(),
                        Description = string.Empty
                    });
                    continue;
                }

                // Check for bullet items under step
                var bulletFallback = BulletListRegex().Match(cleanLine);
                if (bulletFallback.Success)
                {
                    var item = bulletFallback.Groups[1].Value.Trim();
                    if (currentStep.RequiredActivities.Count == 0 && currentStep.Substeps.Count == 0)
                    {
                        currentStep.RequiredActivities.Add(item);
                    }
                    else
                    {
                        currentStep.Substeps.Add(new ProcessSubstep
                        {
                            SubstepId = $"{currentStep.StepId}-{currentStep.Substeps.Count + 1}",
                            SubstepNumber = $"{currentStep.Substeps.Count + 1}",
                            Name = item,
                            Description = string.Empty
                        });
                    }
                    continue;
                }

                // General descriptive text under step: if no objective set yet, populate it
                if (string.IsNullOrWhiteSpace(currentStep.Objective) && cleanLine.Length > 20 && !IsHeadingCandidate(cleanLine))
                {
                    currentStep.Objective = cleanLine;
                    continue;
                }
            }
            else if (currentChapter != null && string.IsNullOrWhiteSpace(currentChapter.Description) && !IsHeadingCandidate(cleanLine))
            {
                currentChapter.Description = cleanLine;
            }
        }

        if (pendingSkillName != null && currentStep != null && !currentStep.RequiredSkills.Contains(pendingSkillName))
        {
            currentStep.RequiredSkills.Add(pendingSkillName);
        }

        // Attach relevant table content (skills, activities, deliverables) to steps on the same page
        foreach (var chapter in chapters)
        {
            foreach (var step in chapter.Steps)
            {
                var matchingTables = structuredTables
                    .Where(t => step.SourcePages.Contains(t.SourcePage))
                    .ToList();

                foreach (var table in matchingTables)
                {
                    var hasSkillHeader = table.Headers.Any(h => Regex.IsMatch(h, @"(skill|habilidad|competencia|capacidad)", RegexOptions.IgnoreCase));
                    if (hasSkillHeader)
                    {
                        foreach (var row in table.Rows)
                        {
                            if (row.Count >= 2 && !string.IsNullOrWhiteSpace(row[0]))
                            {
                                var skillText = $"{row[0].Trim()}: {row[1].Trim()}";
                                if (!step.RequiredSkills.Contains(skillText))
                                {
                                    step.RequiredSkills.Add(skillText);
                                }
                            }
                            else if (row.Count == 1 && !string.IsNullOrWhiteSpace(row[0]))
                            {
                                var skillText = row[0].Trim();
                                if (!step.RequiredSkills.Contains(skillText))
                                {
                                    step.RequiredSkills.Add(skillText);
                                }
                            }
                        }
                    }

                    var hasActivityHeader = table.Headers.Any(h => Regex.IsMatch(h, @"(activit|activid|task|tarea|acci[oó]n|action)", RegexOptions.IgnoreCase));
                    if (hasActivityHeader)
                    {
                        foreach (var row in table.Rows)
                        {
                            var actText = string.Join(" - ", row.Where(c => !string.IsNullOrWhiteSpace(c))).Trim();
                            if (!string.IsNullOrWhiteSpace(actText) && !step.RequiredActivities.Contains(actText))
                            {
                                step.RequiredActivities.Add(actText);
                            }
                        }
                    }

                    var hasDeliverableHeader = table.Headers.Any(h => Regex.IsMatch(h, @"(deliverable|entregable|resultado|producto|output|artefacto)", RegexOptions.IgnoreCase));
                    if (hasDeliverableHeader)
                    {
                        foreach (var row in table.Rows)
                        {
                            var delivText = string.Join(" - ", row.Where(c => !string.IsNullOrWhiteSpace(c))).Trim();
                            if (!string.IsNullOrWhiteSpace(delivText) && !step.Deliverables.Contains(delivText))
                            {
                                step.Deliverables.Add(delivText);
                            }
                        }
                    }
                }
            }
        }

        // Check if no clear process structure was found
        if (!hasClearProcessStructure || totalStepsFound == 0)
        {
            warnings.Add("El documento no presenta una estructura secuencial de proceso explícita (pasos/fases). Se extrajo el contenido como estructura general narrativa.");
            if (chapters.Count == 0)
            {
                var generalChapter = new ProcessChapter
                {
                    ChapterId = "ch-1",
                    ChapterNumber = "1",
                    Title = documentTitle,
                    Description = !string.IsNullOrWhiteSpace(processDescription) ? processDescription : "Contenido general del documento",
                    Steps = []
                };
                chapters.Add(generalChapter);
            }
        }

        return new ProcessDocumentStructure
        {
            DocumentId = documentId,
            ProcessName = processName,
            DocumentTitle = documentTitle,
            SourceFile = new SourceFileInfo
            {
                FileName = fileName,
                ContentType = "application/pdf",
                StoragePath = sourceStoragePath
            },
            Chapters = chapters,
            Tables = structuredTables,
            Appendices = appendices,
            Warnings = warnings,
            ProcessingStatus = "completed"
        };
    }

    private static string ExtractDocumentTitle(RawDocumentExtractionResult raw, PdfDocument pdfDocument)
    {
        // 1. Check first page for keywords like PROCESO DE ..., PROCEDIMIENTO PARA ..., MANUAL ..., POLICY ...
        var firstPageText = raw.Paragraphs
            .Where(p => p.PageNumber == 1)
            .Select(p => p.Content)
            .ToList();

        foreach (var line in firstPageText)
        {
            var clean = StripMarkdownAndDecorations(line);
            if (clean.StartsWith("Purpose:", StringComparison.OrdinalIgnoreCase) ||
                clean.StartsWith("Objetivo:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Regex.IsMatch(clean, @"^(proceso|procedimiento|manual|gu[ií]a|instrucci[oó]n|norma|pol[ií]tica|global\s+business|travel\s+policy|policy)\b", RegexOptions.IgnoreCase))
            {
                return CleanTitle(clean);
            }
        }

        // 2. Check first page paragraphs with title role (excluding "Purpose:" / "Objetivo:")
        var titleParagraph = raw.Paragraphs
            .FirstOrDefault(p => p.PageNumber == 1 && (p.Role == "title" || p.Role == "sectionHeading") && p.Content.Length > 3 && !p.Content.Trim().StartsWith("Purpose:", StringComparison.OrdinalIgnoreCase));

        if (titleParagraph != null && !titleParagraph.Content.StartsWith("---"))
        {
            return CleanTitle(titleParagraph.Content);
        }

        // 3. Fallback to first non-empty heading (excluding Purpose)
        var firstHeading = raw.Headings.FirstOrDefault(h => h.PageNumber == 1 && h.Text.Length > 3 && !h.Text.Trim().StartsWith("Purpose:", StringComparison.OrdinalIgnoreCase));
        if (firstHeading != null)
        {
            return CleanTitle(firstHeading.Text);
        }

        // 4. Try largest font words on first page using PdfPig
        try
        {
            var firstPage = pdfDocument.GetPage(1);
            var words = firstPage.GetWords().ToList();
            if (words.Count > 0)
            {
                var maxFontSize = words.Max(w => w.Letters.Count > 0 ? w.Letters.Max(l => l.PointSize) : w.BoundingBox.Height);
                var largeWords = words
                    .Where(w => {
                        var size = w.Letters.Count > 0 ? w.Letters.Max(l => l.PointSize) : w.BoundingBox.Height;
                        return Math.Abs(size - maxFontSize) < 1.0;
                    })
                    .OrderBy(w => w.BoundingBox.Left)
                    .Select(w => w.Text);

                var titleCandidate = string.Join(" ", largeWords).Trim();
                if (titleCandidate.Length >= 4 && titleCandidate.Length <= 120)
                {
                    return CleanTitle(titleCandidate);
                }
            }
        }
        catch
        {
            // Ignore PdfPig geometry errors and fall through
        }

        return string.Empty;
    }

    private static string StripMarkdownAndDecorations(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var cleaned = text.Trim();
        cleaned = Regex.Replace(cleaned, @"^#{1,6}\s*", "");
        cleaned = Regex.Replace(cleaned, @"^(\*\*|\*|__|_)\s*", "");
        cleaned = Regex.Replace(cleaned, @"\s*(\*\*|\*|__|_)$", "");
        return cleaned.Trim();
    }

    private static string CleanTitle(string rawTitle)
    {
        var cleaned = StripMarkdownAndDecorations(rawTitle).Trim('#', ' ', '\t', '\r', '\n');
        cleaned = Regex.Replace(cleaned, @"^(t[ií]tulo|title|asunto|subject)[\s:\-]+", "", RegexOptions.IgnoreCase);
        return cleaned.Trim();
    }

    private static bool IsContextualProcessStep(string line, List<(string Line, int Page)> allLines, int currentIndex)
    {
        var clean = StripMarkdownAndDecorations(line);
        var match = TopLevelNumberedRegex().Match(clean);
        if (!match.Success) return false;

        var textAfterNumber = match.Groups.Count > 1 ? match.Groups[match.Groups.Count - 1].Value.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(textAfterNumber)) return false;

        // Check if explicit step keyword is present
        if (Regex.IsMatch(textAfterNumber, @"^(paso|step|fase|etapa|tarea|task|secci[oó]n|section|pol[ií]tica|policy)\s*\d*", RegexOptions.IgnoreCase))
        {
            return true;
        }

        // Top-level numbered items (1. Title, 2. Title, 3. Title) in policy, SOP, and procedure manuals
        // represent the work steps/policies of the process if they are followed by bullets or descriptive text
        if (int.TryParse(match.Groups[1].Value, out var stepNumInt) && stepNumInt >= 1 && stepNumInt <= 50)
        {
            // Check next lines to see if it has content (bullets, descriptions) or if next numbered items follow
            if (currentIndex + 1 < allLines.Count)
            {
                var nextLine = StripMarkdownAndDecorations(allLines[currentIndex + 1].Line);
                if (BulletListRegex().IsMatch(nextLine) ||
                    CheckboxSymbolRegex().IsMatch(nextLine) ||
                    CheckboxBracketRegex().IsMatch(nextLine) ||
                    (nextLine.Length > 10 && !TopLevelNumberedRegex().IsMatch(nextLine)))
                {
                    return true;
                }
            }

            // Or if there is a sequence: e.g. 1. ... followed later by 2. ...
            for (var nextIdx = currentIndex + 1; nextIdx < Math.Min(allLines.Count, currentIndex + 15); nextIdx++)
            {
                var nextCandidate = StripMarkdownAndDecorations(allLines[nextIdx].Line);
                var nextMatch = TopLevelNumberedRegex().Match(nextCandidate);
                if (nextMatch.Success && int.TryParse(nextMatch.Groups[1].Value, out var nextInt) && nextInt == stepNumInt + 1)
                {
                    return true;
                }
            }
        }

        // Check surrounding context: was the previous heading/line a process section?
        // E.g., "PASOS DEL PROCESO", "FLUJO DE TRABAJO", "PROCEDIMIENTO", "ETAPAS", "POLICY", "MANUAL"
        for (var k = Math.Max(0, currentIndex - 5); k < currentIndex; k++)
        {
            var prevLine = StripMarkdownAndDecorations(allLines[k].Line);
            if (Regex.IsMatch(prevLine, @"(pasos|proceso|etapas|procedimiento|flujo|instrucciones|workflow|policy|policies|pol[ií]tica|manual|guide|guideline)", RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        // If line contains action verb in infinitive or imperative (Spanish/English) typical of process steps
        if (Regex.IsMatch(textAfterNumber, @"^(ingresar|validar|solicitar|revisar|aprobar|ejecutar|configurar|generar|enviar|guardar|notificar|crear|actualizar|enter|validate|submit|review|approve|execute|configure|generate|send|save|notify|create|update)\b", RegexOptions.IgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string ExtractStepNameFromNumberedLine(string line)
    {
        var clean = StripMarkdownAndDecorations(line);
        var match = TopLevelNumberedRegex().Match(clean);
        if (match.Success && match.Groups.Count > 1 && !string.IsNullOrWhiteSpace(match.Groups[match.Groups.Count - 1].Value))
        {
            return match.Groups[match.Groups.Count - 1].Value.Trim();
        }
        return clean;
    }

    private static ProcessChapter CreateDefaultChapter(int index)
    {
        return new ProcessChapter
        {
            ChapterId = $"ch-{index}",
            ChapterNumber = index.ToString(),
            Title = $"Capítulo {index}",
            Description = string.Empty
        };
    }
}

public sealed class ProcessStructureAiResponse
{
    [JsonPropertyName("processName")]
    public string ProcessName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("chapters")]
    public List<ProcessChapterAiDto> Chapters { get; set; } = [];
}

public sealed class ProcessChapterAiDto
{
    [JsonPropertyName("chapterNumber")]
    public string ChapterNumber { get; set; } = "1";

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("steps")]
    public List<ProcessStepAiDto> Steps { get; set; } = [];
}

public sealed class ProcessStepAiDto
{
    [JsonPropertyName("stepNumber")]
    public string StepNumber { get; set; } = string.Empty;

    [JsonPropertyName("stepName")]
    public string StepName { get; set; } = string.Empty;

    [JsonPropertyName("objective")]
    public string Objective { get; set; } = string.Empty;

    [JsonPropertyName("requiredActivities")]
    public List<string> RequiredActivities { get; set; } = [];

    [JsonPropertyName("requiredTools")]
    public List<string> RequiredTools { get; set; } = [];

    [JsonPropertyName("deliverables")]
    public List<string> Deliverables { get; set; } = [];

    [JsonPropertyName("completionCondition")]
    public string CompletionCondition { get; set; } = string.Empty;

    [JsonPropertyName("sourcePage")]
    public int SourcePage { get; set; } = 1;
}
