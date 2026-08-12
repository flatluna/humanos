using FluentAssertions;
using HumanOS.Agents.Studio;
using Xunit;

namespace HumanOS.Tests.Agents.Studio;

/// <summary>
/// Tests for <see cref="SyllabusOutlineParser"/> — the deterministic
/// (no-LLM) "Tier 0" syllabus structure detector (2026-08-03).
/// </summary>
public class SyllabusOutlineParserTests
{
    private const string MarketingDigitalSyllabus = """
        ### **Módulo 1: Introducción al Marketing Digital**

        - 1.1. ¿Qué es el marketing digital?
        - 1.2. Diferencias entre marketing tradicional y digital
        - 1.3. Evolución y tendencias actuales
        - 1.4. Ecosistema digital y canales principales
        - 1.5. Ventajas y desafíos del marketing online

        ### **Módulo 2: Estrategia y Planificación**

        - 2.1. Definición de objetivos SMART
        - 2.2. Análisis del público objetivo (Buyer Persona)
        - 2.3. Customer Journey (viaje del cliente)
        - 2.4. Análisis de la competencia
        - 2.5. Embudo de conversión (Funnel)
        - 2.6. Elaboración de un plan de marketing digital

        ### **Módulo 3: Sitios Web y Landing Pages**

        - 3.1. Importancia de tener un sitio web
        - 3.2. Diseño web centrado en el usuario (UX/UI)
        - 3.3. Creación de landing pages efectivas
        - 3.4. Optimización para dispositivos móviles
        - 3.5. Herramientas de creación web
        """;

    [Fact]
    public void Parse_MarketingDigitalSyllabus_DetectsAllModulesAndSubsectionsInOrder()
    {
        var result = SyllabusOutlineParser.Parse(MarketingDigitalSyllabus);

        result.IsConfidentStructure.Should().BeTrue(because: result.RejectionReason);
        result.Modules.Should().HaveCount(3);

        result.Modules[0].Number.Should().Be(1);
        result.Modules[0].Title.Should().Be("Introducción al Marketing Digital");
        result.Modules[0].Subsections.Should().HaveCount(5);
        result.Modules[0].Subsections[0].Number.Should().Be("1.1");
        result.Modules[0].Subsections[0].Title.Should().Be("¿Qué es el marketing digital?");
        result.Modules[0].Subsections[4].Number.Should().Be("1.5");

        result.Modules[1].Number.Should().Be(2);
        result.Modules[1].Title.Should().Be("Estrategia y Planificación");
        result.Modules[1].Subsections.Should().HaveCount(6);
        result.Modules[1].Subsections[1].Number.Should().Be("2.2");
        result.Modules[1].Subsections[1].Title.Should().Be("Análisis del público objetivo (Buyer Persona)");

        result.Modules[2].Number.Should().Be(3);
        result.Modules[2].Title.Should().Be("Sitios Web y Landing Pages");
        result.Modules[2].Subsections.Should().HaveCount(5);
    }

    [Fact]
    public void Parse_UnstructuredFreeText_IsRejected()
    {
        const string freeText = """
            El marketing digital es una disciplina en constante evolución.
            En este documento hablaremos de varios temas relacionados con
            estrategias online, redes sociales, y análisis de datos, sin
            seguir ningún índice numerado en particular.
            """;

        var result = SyllabusOutlineParser.Parse(freeText);

        result.IsConfidentStructure.Should().BeFalse();
        result.RejectionReason.Should().NotBeNullOrWhiteSpace();
        result.Modules.Should().BeEmpty();
    }

    [Fact]
    public void Parse_SingleModuleOnly_IsRejected()
    {
        const string oneModule = """
            ### Módulo 1: Introducción

            - 1.1. Primer tema
            - 1.2. Segundo tema
            """;

        var result = SyllabusOutlineParser.Parse(oneModule);

        result.IsConfidentStructure.Should().BeFalse();
        result.RejectionReason.Should().Contain("al menos 2");
    }

    [Fact]
    public void Parse_NonSequentialModuleNumbers_IsRejected()
    {
        const string outOfOrder = """
            ### Módulo 1: Introducción
            - 1.1. Tema uno

            ### Módulo 1: Repetido
            - 1.1. Tema repetido
            """;

        var result = SyllabusOutlineParser.Parse(outOfOrder);

        result.IsConfidentStructure.Should().BeFalse();
        result.RejectionReason.Should().Contain("consistente");
    }

    [Fact]
    public void Parse_OnlySomeModulesHaveSubsections_IsRejected()
    {
        const string inconsistent = """
            ### Módulo 1: Introducción
            - 1.1. Tema uno
            - 1.2. Tema dos

            ### Módulo 2: Sin subsecciones detectadas

            ### Módulo 3: Otro sin subsecciones

            ### Módulo 4: Tampoco tiene
            """;

        var result = SyllabusOutlineParser.Parse(inconsistent);

        result.IsConfidentStructure.Should().BeFalse();
        result.RejectionReason.Should().Contain("consistente");
    }

    [Fact]
    public void Parse_ModulesWithNoSubsectionsAtAll_IsAcceptedAsFlatStructure()
    {
        const string flatToc = """
            ### Unidad 1: Vocales

            ### Unidad 2: Consonantes

            ### Unidad 3: Sílabas
            """;

        var result = SyllabusOutlineParser.Parse(flatToc);

        result.IsConfidentStructure.Should().BeTrue(because: result.RejectionReason);
        result.Modules.Should().HaveCount(3);
        result.Modules.Should().OnlyContain(m => m.Subsections.Count == 0);
    }

    [Fact]
    public void Parse_EnglishKeywords_AreDetected()
    {
        const string english = """
            ## Chapter 1: Getting Started

            - 1.1. What is programming?
            - 1.2. Setting up your environment

            ## Chapter 2: Variables and Types

            - 2.1. Declaring variables
            - 2.2. Primitive types
            """;

        var result = SyllabusOutlineParser.Parse(english);

        result.IsConfidentStructure.Should().BeTrue(because: result.RejectionReason);
        result.Modules.Should().HaveCount(2);
        result.Modules[0].Title.Should().Be("Getting Started");
        result.Modules[0].Subsections[0].Title.Should().Be("What is programming?");
    }

    [Fact]
    public void Parse_UnidadAndCapituloKeywords_AreDetected()
    {
        const string mixed = """
            Unidad 1 - Números Complejos

            1.1. Definición de números complejos
            1.2. Operaciones básicas

            Capítulo 2 - Polinomios

            2.1. Definición de polinomio
            2.2. Suma y resta de polinomios
            """;

        var result = SyllabusOutlineParser.Parse(mixed);

        result.IsConfidentStructure.Should().BeTrue(because: result.RejectionReason);
        result.Modules.Should().HaveCount(2);
        result.Modules[0].Title.Should().Be("Números Complejos");
        result.Modules[1].Title.Should().Be("Polinomios");
    }

    [Fact]
    public void Parse_EmptyText_IsRejected()
    {
        var result = SyllabusOutlineParser.Parse("   ");

        result.IsConfidentStructure.Should().BeFalse();
        result.RejectionReason.Should().Be("El documento está vacío.");
    }

    [Fact]
    public void Parse_StructureAppearingTooLateInDocument_IsRejected()
    {
        // A huge preamble (>30% of the document) before the first detected
        // module heading should be treated as "probably incidental", not a
        // real index.
        var preamble = string.Concat(Enumerable.Repeat(
            "Este es un párrafo de relleno que no forma parte de ningún índice real. ", 200));

        var text = preamble + """


            ### Módulo 1: Título
            - 1.1. Subtema

            ### Módulo 2: Otro título
            - 2.1. Subtema
            """;

        var result = SyllabusOutlineParser.Parse(text);

        result.IsConfidentStructure.Should().BeFalse();
        result.RejectionReason.Should().Contain("tarde");
    }

    // ------------------------------------------------------------------
    // Realistic / "end-to-end style" cases: full production scale, PDF
    // text-extraction noise, and known-tricky real-world document shapes.
    // These exercise the parser against inputs closer to what a real
    // uploaded PDF or pasted syllabus produces, beyond the hand-crafted
    // minimal examples above.
    // ------------------------------------------------------------------

    /// <summary>
    /// Full-scale reconstruction of the real "Marketing Digital 360" course
    /// that originally exposed the bug (13 módulos, 3-6 subsecciones cada
    /// uno, ~55 subsecciones totales) — confirms the quality gates
    /// (coverage, monotonic numbering, subsection consistency) still all
    /// pass at real production scale, not just the 3-module toy example.
    /// </summary>
    [Fact]
    public void Parse_FullScale13ModuleRealWorldSyllabus_DetectsAllModulesAndSubsections()
    {
        (int Number, string Title, string[] Subtopics)[] plan =
        [
            (1, "Introducción al Marketing Digital", ["¿Qué es el marketing digital?", "Diferencias entre marketing tradicional y digital", "Evolución y tendencias actuales", "Ecosistema digital y canales principales"]),
            (2, "Estrategia y Planificación", ["Definición de objetivos SMART", "Análisis del público objetivo (Buyer Persona)", "Customer Journey (viaje del cliente)", "Análisis de la competencia", "Embudo de conversión (Funnel)"]),
            (3, "Sitios Web y Landing Pages", ["Importancia de tener un sitio web", "Diseño web centrado en el usuario (UX/UI)", "Creación de landing pages efectivas"]),
            (4, "SEO — Posicionamiento en Buscadores", ["Fundamentos de SEO", "Investigación de palabras clave", "SEO on-page", "SEO técnico", "Link building"]),
            (5, "SEM y Google Ads", ["Fundamentos de la publicidad en buscadores", "Estructura de una campaña en Google Ads", "Estrategias de puja", "Medición de resultados"]),
            (6, "Marketing en Redes Sociales", ["Selección de plataformas según el público", "Creación de contenido para redes", "Publicidad paga en redes sociales", "Community management", "Métricas de redes sociales"]),
            (7, "Email Marketing", ["Construcción de listas de contactos", "Diseño de campañas de email", "Automatización básica de correos"]),
            (8, "Marketing de Contenidos", ["Estrategia de contenidos", "Formatos de contenido", "Calendario editorial", "Distribución y promoción"]),
            (9, "Analítica Web", ["Introducción a Google Analytics", "Definición de KPIs", "Interpretación de reportes", "Toma de decisiones basada en datos"]),
            (10, "Comercio Electrónico", ["Plataformas de ecommerce", "Optimización del checkout", "Estrategias de retención de clientes"]),
            (11, "Marketing de Influencers", ["Selección de influencers", "Tipos de colaboración", "Medición de ROI en campañas con influencers"]),
            (12, "Automatización de Marketing", ["Introducción a los CRM", "Flujos de nutrición de leads", "Segmentación avanzada", "Integración entre herramientas"]),
            (13, "Tendencias Emergentes y Cierre del Curso", ["Inteligencia artificial aplicada al marketing", "Marketing por voz y búsqueda conversacional", "Privacidad y el fin de las cookies de terceros", "Proyecto final integrador"]),
        ];

        var sb = new System.Text.StringBuilder();
        foreach (var module in plan)
        {
            sb.AppendLine($"### **Módulo {module.Number}: {module.Title}**");
            sb.AppendLine();
            for (var i = 0; i < module.Subtopics.Length; i++)
            {
                sb.AppendLine($"- {module.Number}.{i + 1}. {module.Subtopics[i]}");
            }
            sb.AppendLine();
        }

        var result = SyllabusOutlineParser.Parse(sb.ToString());

        result.IsConfidentStructure.Should().BeTrue(because: result.RejectionReason);
        result.Modules.Should().HaveCount(13);
        result.Modules.Select(m => m.Number).Should().BeInAscendingOrder();
        result.Modules.Sum(m => m.Subsections.Count).Should().Be(plan.Sum(p => p.Subtopics.Length));
        result.Modules[12].Title.Should().Be("Tendencias Emergentes y Cierre del Curso");
        result.Modules[12].Subsections.Should().HaveCount(4);
        result.Modules[12].Subsections[3].Title.Should().Be("Proyecto final integrador");
    }

    /// <summary>
    /// Simulates typical PDF text-extraction noise: repeated page
    /// headers/footers, double spaces from justified text, and a
    /// hyphenated line-break splitting a word across two lines. The
    /// detector should still find the real structure, ignoring the noise
    /// lines (they simply match neither regex and are skipped).
    /// </summary>
    [Fact]
    public void Parse_TextWithPdfExtractionNoise_StillDetectsStructure()
    {
        const string noisy = """
            Curso de Marketing Digital 360
            Página 1 de 24

            Módulo  1:   Introducción al Marketing Digi-
            tal

            Página 2 de 24
            Curso de Marketing Digital 360

            - 1.1.  ¿Qué es el marketing digital?
            - 1.2.  Diferencias entre marketing tradi-
            cional y digital

            Página 3 de 24

            Módulo 2: Estrategia y Planificación

            - 2.1. Definición de objetivos SMART
            - 2.2. Análisis del público objetivo

            Página 4 de 24
            """;

        var result = SyllabusOutlineParser.Parse(noisy);

        result.IsConfidentStructure.Should().BeTrue(because: result.RejectionReason);
        result.Modules.Should().HaveCount(2);
        // The hyphenated line break means the module/subsection title only
        // captures the text on its own heading line — the continuation
        // word on the next line is simply ignored (no rejoining logic).
        // This documents that known limitation rather than a joined title.
        result.Modules[0].Title.Should().StartWith("Introducción al Marketing Digi");
        result.Modules[0].Subsections.Should().HaveCount(2);
        result.Modules[0].Subsections[1].Title.Should().Be("Diferencias entre marketing tradi-");
    }

    /// <summary>
    /// Real syllabus PDFs commonly repeat every module heading twice: once
    /// in a "Tabla de contenidos" (index) page, and again as the real
    /// chapter heading later in the document — causing module numbers to
    /// restart (1, 2, 3, 1, 2, 3) when scanned linearly. Tier 0 correctly
    /// declines this shape (numbering isn't monotonic) rather than
    /// guessing which occurrence is authoritative; this is EXPECTED to
    /// fall through to the Tier 1 LLM-based fallback in the real pipeline,
    /// not something Tier 0 itself should try to resolve.
    /// </summary>
    [Fact]
    public void Parse_TableOfContentsFollowedByDuplicateChapterHeadings_IsRejected()
    {
        const string tocThenContent = """
            Tabla de contenidos

            Módulo 1: Introducción
            Módulo 2: Estrategia
            Módulo 3: Ejecución

            ---

            Módulo 1: Introducción
            - 1.1. Primer tema
            - 1.2. Segundo tema

            Módulo 2: Estrategia
            - 2.1. Primer tema
            - 2.2. Segundo tema

            Módulo 3: Ejecución
            - 3.1. Primer tema
            """;

        var result = SyllabusOutlineParser.Parse(tocThenContent);

        result.IsConfidentStructure.Should().BeFalse();
        result.RejectionReason.Should().Contain("consistente");
    }

    /// <summary>
    /// All-caps module keyword (common in PDFs with ALL-CAPS heading
    /// styles) should still be recognized since matching is
    /// case-insensitive.
    /// </summary>
    [Fact]
    public void Parse_AllCapsModuleKeyword_IsDetected()
    {
        const string allCaps = """
            MÓDULO 1: FUNDAMENTOS DE PROGRAMACIÓN
            - 1.1. Variables y tipos de datos
            - 1.2. Estructuras de control

            MÓDULO 2: FUNCIONES Y MÓDULOS
            - 2.1. Definición de funciones
            - 2.2. Ámbito de variables
            """;

        var result = SyllabusOutlineParser.Parse(allCaps);

        result.IsConfidentStructure.Should().BeTrue(because: result.RejectionReason);
        result.Modules.Should().HaveCount(2);
        result.Modules[0].Title.Should().Be("FUNDAMENTOS DE PROGRAMACIÓN");
    }

    /// <summary>
    /// A stray "N.M" numbered line that doesn't belong to the current
    /// module (e.g. leftover cross-reference text, or a numbered list
    /// item unrelated to the syllabus) must NOT be attributed to the
    /// current module just because it looks like a subsection — the
    /// major-number guard should silently ignore it.
    /// </summary>
    [Fact]
    public void Parse_SubsectionNumberNotMatchingCurrentModule_IsIgnored()
    {
        const string text = """
            Módulo 1: Introducción
            - 1.1. Primer tema
            - 1.2. Segundo tema

            Módulo 2: Avanzado
            Ver también la sección 1.5 del manual externo para más detalle.
            - 2.1. Primer tema avanzado
            - 2.2. Segundo tema avanzado
            """;

        var result = SyllabusOutlineParser.Parse(text);

        result.IsConfidentStructure.Should().BeTrue(because: result.RejectionReason);
        result.Modules[1].Subsections.Should().HaveCount(2);
        result.Modules[1].Subsections.Should().NotContain(s => s.Number == "1.5");
    }

    /// <summary>
    /// Subsections using lettered bullets (a), b), c)) instead of numbered
    /// "N.M" style are simply not matched by the subsection regex — since
    /// this applies uniformly across every module, the document is still
    /// accepted as a valid flat (module-only) structure rather than
    /// rejected for "inconsistent" subsections.
    /// </summary>
    [Fact]
    public void Parse_LetteredSubsectionsInsteadOfNumbered_IsAcceptedAsFlatStructure()
    {
        const string text = """
            Unidad 1: Fonética
            a) Vocales abiertas
            b) Vocales cerradas

            Unidad 2: Morfología
            a) Sustantivos
            b) Verbos

            Unidad 3: Sintaxis
            a) Oraciones simples
            """;

        var result = SyllabusOutlineParser.Parse(text);

        result.IsConfidentStructure.Should().BeTrue(because: result.RejectionReason);
        result.Modules.Should().HaveCount(3);
        result.Modules.Should().OnlyContain(m => m.Subsections.Count == 0);
    }

    /// <summary>
    /// Real-world regression (2026-08-09): a PDF-to-Markdown extraction that
    /// wraps every heading in "[text](url)" link syntax (some with an empty
    /// leading "[]()" artifact), styles one subsection heading with ONLY
    /// bold ("**3.2 ...**", no "#" at all), and word-wraps another heading
    /// across two physical lines. The bug this locks in: Tier 0 used to
    /// reject or silently drop "1.1", "3.2" and "3.3" because none of its
    /// regexes tolerated link/bold decoration — falling through to the
    /// LLM-based Tier 1, which itself dropped those same subsections.
    /// </summary>
    [Fact]
    public void Parse_HeadingsWrappedInMarkdownLinksAndBold_DetectsEveryChapterAndSubsection()
    {
        const string text = """
            # [1.0 Introduction]()

            ## [1.1 Purpose of the Policy]()

            Welcome, sweet friend—we are so happy you're here!

            ## [1.2 Importance of Volunteers in
            the Program]()

            Volunteers like you are such a blessing.

            ---

            # []()[3.0 Volunteer Role: Companion and Support Within the Group]()

            ## []()[3.1 Companionship and Support During Group
            Activities]()

            We love to think of our volunteers as kind, friendly companions.

            **3.2 General Responsibilities**

            Your role is a truly lovely one, dear.

            # []()[3.3 When a Volunteer May Lead Activities]()

            Sometimes, you might be invited to lead an activity.
            """;

        var result = SyllabusOutlineParser.Parse(text);

        result.IsConfidentStructure.Should().BeTrue(because: result.RejectionReason);
        result.Modules.Should().HaveCount(2);

        result.Modules[0].Number.Should().Be(1);
        result.Modules[0].Subsections.Select(s => s.Number).Should().Equal("1.1", "1.2");
        result.Modules[0].Subsections[0].Title.Should().Be("Purpose of the Policy");
        result.Modules[0].Subsections[1].Title.Should().Be("Importance of Volunteers in the Program");

        result.Modules[1].Number.Should().Be(3);
        result.Modules[1].Title.Should().Contain("Volunteer Role");
        result.Modules[1].Subsections.Select(s => s.Number).Should().Equal("3.1", "3.2", "3.3");
        result.Modules[1].Subsections[0].Title.Should().Be("Companionship and Support During Group Activities");
        result.Modules[1].Subsections[1].Title.Should().Be("General Responsibilities");
        result.Modules[1].Subsections[2].Title.Should().Be("When a Volunteer May Lead Activities");
    }

    /// <summary>
    /// Real-world regression (2026-08-09): the SAME document as above, but
    /// now preceded by its own Word-exported "Content" (Table of Contents)
    /// section — each entry a Markdown link whose text bakes in the page
    /// number too (e.g. "[1.0 Introduction. 1](#_Toc237186034)"). The bug
    /// this locks in: the coverage gate compared against the DEDUPED
    /// (later-wins) offset of the body's own "1.0" heading — which sits
    /// AFTER this whole TOC block — so on a short document the TOC alone
    /// could push that occurrence past the 70% "way too late" threshold
    /// and reject Tier 0 entirely, even though the very first sighting
    /// (the TOC's own "1.0" entry) legitimately appears right at the top.
    /// Falling through to Tier 1 (LLM) then dropped the source document's
    /// own "1.0"/"1.1"/etc. numbering from every returned title, since the
    /// LLM only returns prose titles, not the exact numbering scheme.
    /// </summary>
    [Fact]
    public void Parse_PrecededByWordExportedTableOfContents_StillDetectsStructureWithNumbering()
    {
        const string text = """
            Content

            [1.0 Introduction. 1](#_Toc237186034)

            [1.1 Purpose of the Policy. 1](#_Toc237186035)

            [1.2 Importance of Volunteers in the Program... 1](#_Toc237186036)

            [3.0 Volunteer Role: Companion and Support Within the
            Group. 1](#_Toc237186037)

            [3.1 Companionship and Support During Group Activities. 1](#_Toc237186038)

            [3.2 General Responsibilities. 2](#_Toc237186039)

            [3.3 When a Volunteer May Lead Activities. 2](#_Toc237186040)

            # [1.0 Introduction]()

            ## [1.1 Purpose of the Policy]()

            Welcome, sweet friend—we are so happy you're here!

            ## [1.2 Importance of Volunteers in
            the Program]()

            Volunteers like you are such a blessing.

            ---

            # []()[3.0 Volunteer Role: Companion and Support Within the Group]()

            ## []()[3.1 Companionship and Support During Group
            Activities]()

            We love to think of our volunteers as kind, friendly companions.

            **3.2 General Responsibilities**

            Your role is a truly lovely one, dear.

            # []()[3.3 When a Volunteer May Lead Activities]()

            Sometimes, you might be invited to lead an activity.
            """;

        var result = SyllabusOutlineParser.Parse(text);

        result.IsConfidentStructure.Should().BeTrue(because: result.RejectionReason);
        result.Modules.Should().HaveCount(2);

        result.Modules[0].Title.Should().Be("1.0 Introduction");
        result.Modules[0].Subsections.Select(s => s.Number).Should().Equal("1.1", "1.2");
        result.Modules[0].Subsections[0].Title.Should().Be("Purpose of the Policy");
        result.Modules[0].Subsections[1].Title.Should().Be("Importance of Volunteers in the Program");

        result.Modules[1].Title.Should().Be("3.0 Volunteer Role: Companion and Support Within the Group");
        result.Modules[1].Subsections.Select(s => s.Number).Should().Equal("3.1", "3.2", "3.3");
        result.Modules[1].Subsections[0].Title.Should().Be("Companionship and Support During Group Activities");
        result.Modules[1].Subsections[1].Title.Should().Be("General Responsibilities");
        result.Modules[1].Subsections[2].Title.Should().Be("When a Volunteer May Lead Activities");
    }

    /// <summary>
    /// Real-world regression (2026-08-09), confirmed via live diagnostic
    /// logging against an actual uploaded PDF: the SAME document/numbering
    /// as the two tests above, but this is what the real text-extraction
    /// pipeline actually produced — EVERY line break collapsed, so the
    /// document's own dot-leader Table of Contents ("1.1 Purpose of the
    /// Policy .......... 1") and the body content that follows it are one
    /// single continuous block of prose with no newlines anywhere. Neither
    /// <see cref="SyllabusOutlineParser"/>'s keyword nor bare-numeric
    /// line-based conventions can ever match this (both require a heading
    /// to start its own line), so Tier 0 used to report 0 modules detected
    /// and fall through to Tier 1 (LLM), which dropped the source
    /// document's own numbering from every title.
    /// </summary>
    [Fact]
    public void Parse_FlattenedNoLineBreaksWithDotLeaderToc_StillDetectsStructureWithNumbering()
    {
        const string text =
            "The Haven Program - Volunteer Orientation and Safeguarding Policy v.1.0 " +
            "The Haven Program - Volunteer Orientation and Safeguarding Policy Version 1.0 | Last Updated: August 2026 " +
            "Page:1 Content 1.0 Introduction ................................................... 1 " +
            "1.1 Purpose of the Policy .......................................................... 1 " +
            "1.2 Importance of Volunteers in the Program ...................................... 1 " +
            "3.0 Volunteer Role: Companion and Support Within the Group ...................... 1 " +
            "3.1 Companionship and Support During Group Activities ........................... 1 " +
            "3.2 General Responsibilities ..................................................... 2 " +
            "3.3 When a Volunteer May Lead Activities ......................................... 2 " +
            "1.0 Introduction 1.1 Purpose of the Policy Welcome, sweet friend-we are so happy you're here! " +
            "The Haven is a place built on warmth, dignity, respect, and safety. " +
            "1.2 Importance of Volunteers in the Program Volunteers like you are such a blessing. " +
            "3.0 Volunteer Role: Companion and Support Within the Group Your role matters so much. " +
            "3.1 Companionship and Support During Group Activities We love to think of our volunteers as kind, friendly companions. " +
            "3.2 General Responsibilities Your role is a truly lovely one, dear. " +
            "3.3 When a Volunteer May Lead Activities Sometimes, you might be invited to lead an activity.";

        var result = SyllabusOutlineParser.Parse(text);

        result.IsConfidentStructure.Should().BeTrue(because: result.RejectionReason);
        result.Modules.Should().HaveCount(2);

        result.Modules[0].Number.Should().Be(1);
        result.Modules[0].Subsections.Select(s => s.Number).Should().Equal("1.1", "1.2");
        result.Modules[0].Subsections[0].Title.Should().Be("Purpose of the Policy");
        result.Modules[0].Subsections[1].Title.Should().Be("Importance of Volunteers in the Program");

        result.Modules[1].Number.Should().Be(3);
        result.Modules[1].Title.Should().Contain("Volunteer Role");
        result.Modules[1].Subsections.Select(s => s.Number).Should().Equal("3.1", "3.2", "3.3");
        result.Modules[1].Subsections[0].Title.Should().Be("Companionship and Support During Group Activities");
        result.Modules[1].Subsections[1].Title.Should().Be("General Responsibilities");
        result.Modules[1].Subsections[2].Title.Should().Be("When a Volunteer May Lead Activities");
    }
}
