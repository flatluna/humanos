using System.Diagnostics;
using System.Text.RegularExpressions;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Contracts.ProcessDocuments;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using OpenAI.Chat;

namespace HumanOS.Services.ProcessDocuments;

/// <summary>
/// Given a real course (title/provider/URL) recommended for a skill gap, tries to extract its real
/// module index by driving a real headless browser (Microsoft.Playwright) against the course page (e.g.
/// Coursera's "#modules" syllabus section), because most course sites render their syllabus with
/// JavaScript — a plain HTTP GET only returns an empty shell or unrelated nav/footer text. If no usable
/// index can be scraped (paywalled page, unknown layout, no URL, etc.), falls back to asking the LLM to
/// draft a plausible outline from the course title/provider/skill — clearly labeled as AI-suggested, not
/// scraped — so that EngramsIQ Studio can later use either as a starting draft to auto-generate the course.
/// </summary>
public sealed class CourseOutlineService : IAsyncDisposable
{
    // Module/week/unit markers, in English and Spanish, as their own line (e.g. "Module 1", "Semana 2").
    private static readonly Regex ModuleMarkerPattern = new(
        @"^(module|módulo|modulo|week|semana|unit|unidad)\s*\d+$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DurationPattern = new(
        @"\d+\s*(minute|minutes|minuto|minutos|hour|hours|hora|horas)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Matches accordion-style section headings used by providers that don't say "Module N" (e.g. Udemy:
    // "Corporate Compliance Foundation 6 lectures • 28min"), so their real section titles can still be
    // extracted instead of falling back to an AI-invented draft.
    private static readonly Regex SectionCountPattern = new(
        @"\d+\s*(lectures?|lessons?|videos?|topics?|items?|classes?|lecciones?|clases?|videos?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Lines that are page chrome/noise, not real module titles — must never be mistaken for a title.
    private static readonly string[] NoiseLines =
    [
        "show more", "show less", "enroll for free", "enroll now", "join for free", "module details",
        "course details", "about", "outcomes", "modules", "recommendations", "testimonials", "reviews",
        "instructor", "instructors", "earn a career certificate", "earn a certificate",
    ];

    private readonly SemaphoreSlim _browserLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private const string GeneratorInstructions = """
        You are an instructional designer. You will be given a real course's title, provider, and the skill
        it should teach (and optionally the business process it supports). The course page could not be
        scraped (unknown layout, paywalled, or no URL), so you must DRAFT a plausible, realistic module/lesson
        index for it, as a helpful starting outline — NOT a verified fact.

        RULES:
        1. Produce 3 to 8 modules, each with a short title and 2 to 5 lesson bullet titles.
        2. Base the content on what a course with this exact title/provider for this skill would plausibly
           cover — keep it practical and specific to the skill, not generic filler.
        3. Do not claim this is the real published syllabus; this is a draft outline to help generate training
           content later.
        """;

    private readonly AIAgent? _generatorAgent;
    private readonly ILogger<CourseOutlineService>? _logger;

    public CourseOutlineService(IConfiguration configuration, ILogger<CourseOutlineService>? logger = null)
    {
        _logger = logger;

        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIEconomyDeploymentName"] ?? configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _generatorAgent = null;
            return;
        }

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        _generatorAgent = client
            .GetChatClient(deploymentName)
            .AsAIAgent(instructions: GeneratorInstructions, name: "CourseOutlineGeneratorAgent");
    }

    public async Task<CourseOutlineResult> GetOrGenerateOutlineAsync(CourseOutlineRequest request, CancellationToken cancellationToken = default)
    {
        var result = new CourseOutlineResult
        {
            CourseTitle = request.CourseTitle,
            Url = request.Url,
        };

        List<CourseOutlineModule>? scraped = null;
        if (!string.IsNullOrWhiteSpace(request.Url))
        {
            try
            {
                scraped = await TryScrapeOutlineAsync(request.Url, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogWarning(ex, "CourseOutlineService: failed to scrape course page {Url}", request.Url);
            }
        }

        if (scraped is { Count: >= 2 })
        {
            result.Source = "scraped";
            result.Modules = scraped;
            return result;
        }

        if (_generatorAgent is null)
        {
            result.Warnings.Add(string.IsNullOrWhiteSpace(request.Url)
                ? "No course URL was provided, and the AI outline generator is not configured."
                : "Could not scrape a module index from the course page, and the AI outline generator is not configured.");
            return result;
        }

        var aiModules = await GenerateOutlineWithAiAsync(request, cancellationToken);
        result.Source = "ai-suggested";
        result.Modules = aiModules;
        if (aiModules.Count == 0)
        {
            result.Warnings.Add("Could not scrape or generate a usable course outline.");
        }

        return result;
    }

    /// <summary>
    /// Drives a real headless Chromium instance (Microsoft.Playwright) against the course page, because most
    /// course-provider sites (Coursera, Udemy, etc.) render their syllabus/module list via JavaScript after
    /// page load — a plain HTTP GET only returns an empty shell or unrelated nav/footer text. Dismisses common
    /// cookie-consent banners, scrolls progressively to trigger lazy-loaded sections, then parses the
    /// rendered page text for real module markers ("Module 1", "Semana 2", etc.) paired with their title and
    /// duration. This never invents content — if no real module markers are found, returns null so the
    /// caller falls back to the clearly-labeled AI-suggested draft instead.
    /// </summary>
    private async Task<List<CourseOutlineModule>?> TryScrapeOutlineAsync(string url, CancellationToken cancellationToken)
    {
        var browser = await GetBrowserAsync(cancellationToken);
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1366, Height = 1800 },
        });
        var page = await context.NewPageAsync();

        try
        {
            await page.GotoAsync(url, new PageGotoOptions
            {
                Timeout = 20000,
                WaitUntil = WaitUntilState.DOMContentLoaded,
            });
        }
        catch (TimeoutException)
        {
            // Keep going with whatever rendered so far — many sites finish rendering the syllabus before
            // all third-party trackers/analytics requests settle.
        }
        catch (PlaywrightException ex)
        {
            _logger?.LogWarning(ex, "CourseOutlineService: navigation failed for {Url}", url);
            return null;
        }

        await TryDismissCookieBannerAsync(page);

        // Many course sites lazy-render their syllabus section only once it's scrolled into view.
        for (var i = 0; i < 10 && !cancellationToken.IsCancellationRequested; i++)
        {
            await page.Mouse.WheelAsync(0, 1400);
            await page.WaitForTimeoutAsync(300);
        }

        var bodyText = await page.InnerTextAsync("body");
        var modules = ParseModulesFromRenderedText(bodyText);
        if (modules is { Count: >= 2 })
        {
            return modules;
        }

        // Fallback: providers like Alison render their curriculum as structured module containers
        // (a heading for the module title, plus a nested topics list of real lesson headings) instead of
        // literal "Module 1" markers or an "N lectures" suffix. Extract the real heading text straight
        // from those containers — still only what the page actually says, nothing invented.
        try
        {
            var structuredModules = await page.EvaluateAsync<AlisonModuleJson[]>(@"() => {
                const containers = Array.from(document.querySelectorAll('.l-mods__module'));
                return containers.map(m => {
                    const titleEl = m.querySelector('h3');
                    const topics = Array.from(m.querySelectorAll('.l-mods__topics h4'))
                        .map(h => (h.textContent || '').trim())
                        .filter(Boolean);
                    return { title: titleEl ? (titleEl.textContent || '').trim() : '', topics };
                }).filter(x => x.title);
            }");
            if (structuredModules is { Length: > 0 })
            {
                return structuredModules
                    .Select(m => new CourseOutlineModule { Title = m.Title, Lessons = m.Topics?.ToList() ?? [] })
                    .ToList();
            }
        }
        catch (PlaywrightException ex)
        {
            _logger?.LogWarning(ex, "CourseOutlineService: failed to read structured module containers for {Url}", url);
        }

        // Fallback: some providers (e.g. GBTA) simply describe their curriculum as a plain bullet list
        // right after a sentence like "...modules covered during the training include:". Find that list
        // and use its real item text as the (flat) module titles — still only what the page says.
        try
        {
            var plainListModules = await page.EvaluateAsync<string[]>(@"() => {
                const keyword = /(modules?|módulos?|lessons?|lecciones?|units?|unidades?)/i;
                const introPhrase = /(includ|contien|siguientes|following|cover|covers)/i;
                const candidates = Array.from(document.querySelectorAll('p, strong, div, span'));
                for (const el of candidates) {
                    const text = (el.textContent || '').trim();
                    if (text.length > 400 || !keyword.test(text) || !introPhrase.test(text)) continue;
                    let sib = el.nextElementSibling;
                    let hops = 0;
                    while (sib && !['UL', 'OL'].includes(sib.tagName) && hops < 3) {
                        sib = sib.nextElementSibling;
                        hops++;
                    }
                    if (sib && ['UL', 'OL'].includes(sib.tagName)) {
                        const items = Array.from(sib.querySelectorAll('li'))
                            .map(li => (li.textContent || '').trim())
                            .filter(Boolean);
                        if (items.length > 0) return items;
                    }
                }
                return [];
            }");
            if (plainListModules is { Length: > 0 })
            {
                return plainListModules.Select(title => new CourseOutlineModule { Title = title, Lessons = [] }).ToList();
            }
        }
        catch (PlaywrightException ex)
        {
            _logger?.LogWarning(ex, "CourseOutlineService: failed to read plain-list modules for {Url}", url);
        }

        // Fallback: some providers (e.g. The Travel Institute) label their curriculum with a short heading
        // like "Course Chapters" / "Course Content" / "Syllabus" (no sentence, no "include"/"following"
        // wording), with the real list rendered in a nearby sibling element rather than immediately after
        // the heading. Find that heading and scan forward in document order for the next UL/OL.
        try
        {
            var headingListModules = await page.EvaluateAsync<string[]>(@"() => {
                const headingKeyword = /^(course\s+)?(chapters|contents|content|syllabus|curriculum|outline|modules|lessons|units)$|^(temario|contenido\s+del\s+curso|capítulos|módulos)$/i;
                const headingEls = Array.from(document.querySelectorAll('h1, h2, h3, h4, h5, strong, b'));
                for (const heading of headingEls) {
                    const text = (heading.textContent || '').trim();
                    if (text.length > 60 || !headingKeyword.test(text)) continue;
                    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_ELEMENT);
                    walker.currentNode = heading;
                    let node;
                    let hops = 0;
                    while ((node = walker.nextNode()) && hops < 60) {
                        hops++;
                        if (node.tagName === 'UL' || node.tagName === 'OL') {
                            const items = Array.from(node.querySelectorAll('li'))
                                .map(li => (li.textContent || '').trim())
                                .filter(Boolean);
                            if (items.length > 0) return items;
                        }
                    }
                }
                return [];
            }");
            if (headingListModules is { Length: > 0 })
            {
                return headingListModules.Select(title => new CourseOutlineModule { Title = title, Lessons = [] }).ToList();
            }
        }
        catch (PlaywrightException ex)
        {
            _logger?.LogWarning(ex, "CourseOutlineService: failed to read heading-list modules for {Url}", url);
        }

        // Fallback: many providers (Udemy, LinkedIn Learning, Skillshare, etc.) render their curriculum as
        // an accordion of real heading elements like "Corporate Compliance Foundation 6 lectures • 28min"
        // instead of literal "Module 1"/"Week 1" markers. Extract real heading text (still only what the
        // page actually says — nothing invented) and parse the "N lectures/lessons" suffix as the section
        // boundary.
        try
        {
            var headingTexts = await page.Locator("h1, h2, h3, h4, h5, h6").AllTextContentsAsync();
            return ParseHeadingAccordionModules(headingTexts);
        }
        catch (PlaywrightException ex)
        {
            _logger?.LogWarning(ex, "CourseOutlineService: failed to read heading accordion for {Url}", url);
            return null;
        }
    }

    private static async Task TryDismissCookieBannerAsync(IPage page)
    {
        string[] acceptLabels = ["Accept all", "Accept All", "Accept", "I agree", "Aceptar todo", "Aceptar"];
        foreach (var label in acceptLabels)
        {
            try
            {
                await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = label, Exact = false })
                    .First
                    .ClickAsync(new LocatorClickOptions { Timeout = 1500 });
                return;
            }
            catch (TimeoutException)
            {
                // That label wasn't present/clickable — try the next one.
            }
            catch (PlaywrightException)
            {
                // Banner likely absent; nothing to dismiss.
            }
        }
    }

    /// <summary>
    /// Parses the real, rendered page text for module markers (e.g. "Module 1", "Semana 2") and pairs each
    /// with the title line immediately preceding it and the first duration mention (e.g. "12 minutes") found
    /// shortly after. Only real text found on the page is used — nothing here is generated or guessed.
    /// </summary>
    private static List<CourseOutlineModule>? ParseModulesFromRenderedText(string bodyText)
    {
        var lines = bodyText
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        var modules = new List<CourseOutlineModule>();

        for (var i = 0; i < lines.Count && modules.Count < 20; i++)
        {
            if (!ModuleMarkerPattern.IsMatch(lines[i]))
            {
                continue;
            }

            // The module title is the nearest preceding line that isn't noise/chrome.
            string? title = null;
            for (var back = i - 1; back >= 0 && back >= i - 4; back--)
            {
                var candidate = lines[back];
                if (candidate.Length is < 3 or > 160)
                {
                    continue;
                }
                if (NoiseLines.Contains(candidate.ToLowerInvariant()) || ModuleMarkerPattern.IsMatch(candidate))
                {
                    continue;
                }
                title = candidate;
                break;
            }

            if (title is null)
            {
                continue;
            }

            string? duration = null;
            for (var fwd = i + 1; fwd < lines.Count && fwd <= i + 4; fwd++)
            {
                if (DurationPattern.IsMatch(lines[fwd]))
                {
                    duration = lines[fwd];
                    break;
                }
            }

            modules.Add(new CourseOutlineModule
            {
                Title = duration is null ? title : $"{title} ({duration})",
                Lessons = [],
            });
        }

        return modules.Count > 0 ? modules : null;
    }

    /// <summary>
    /// Parses real heading text (e.g. Udemy's "Corporate Compliance Foundation 6 lectures • 28min") into
    /// modules by cutting off the "N lectures/lessons/..." count suffix that providers append to each real
    /// curriculum section title. Only headings that actually carry this count suffix are treated as
    /// curriculum sections — this avoids picking up unrelated page headings (nav, reviews, FAQ, etc.).
    /// </summary>
    private static List<CourseOutlineModule>? ParseHeadingAccordionModules(IEnumerable<string> headingTexts)
    {
        var modules = new List<CourseOutlineModule>();

        foreach (var raw in headingTexts)
        {
            if (modules.Count >= 20)
            {
                break;
            }

            var text = raw.Trim();
            if (text.Length == 0)
            {
                continue;
            }

            var countMatch = SectionCountPattern.Match(text);
            if (!countMatch.Success)
            {
                continue;
            }

            var title = text[..countMatch.Index].Trim().TrimEnd('-', '–', ':', '|', '•', '·').Trim();
            if (title.Length is < 3 or > 160 || NoiseLines.Contains(title.ToLowerInvariant()))
            {
                continue;
            }

            var durationMatch = DurationPattern.Match(text, countMatch.Index + countMatch.Length);
            modules.Add(new CourseOutlineModule
            {
                Title = durationMatch.Success ? $"{title} ({durationMatch.Value})" : title,
                Lessons = [],
            });
        }

        return modules.Count > 0 ? modules : null;
    }

    private async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
    {
        if (_browser is not null)
        {
            return _browser;
        }

        await _browserLock.WaitAsync(cancellationToken);
        try
        {
            if (_browser is not null)
            {
                return _browser;
            }

            _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            return _browser;
        }
        finally
        {
            _browserLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }
        _playwright?.Dispose();
    }

    private async Task<List<CourseOutlineModule>> GenerateOutlineWithAiAsync(CourseOutlineRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var prompt = $"""
            Course Title: {request.CourseTitle}
            Provider/Platform: {request.Provider ?? "unknown"}
            Skill to teach: {request.SkillName}
            Business process context: {request.ProcessName ?? "(not specified)"}
            """;

        try
        {
            var agentResponse = await _generatorAgent!.RunAsync<CourseOutlineAiDraft>(prompt, cancellationToken: cancellationToken);
            stopwatch.Stop();
            _logger?.LogInformation(
                "CourseOutlineService: AI-generated outline for '{CourseTitle}' in {DurationMs}ms ({ModuleCount} modules)",
                request.CourseTitle, stopwatch.ElapsedMilliseconds, agentResponse.Result?.Modules.Count ?? 0);
            return agentResponse.Result?.Modules ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "CourseOutlineService: failed to AI-generate outline for '{CourseTitle}'", request.CourseTitle);
            return [];
        }
    }

    private sealed class CourseOutlineAiDraft
    {
        public List<CourseOutlineModule> Modules { get; set; } = [];
    }

    // Deserialization target for the structured module/topic extraction (e.g. Alison's ".l-mods__module"
    // containers) run via Page.EvaluateAsync.
    private sealed class AlisonModuleJson
    {
        public string Title { get; set; } = string.Empty;
        public string[] Topics { get; set; } = [];
    }
}
