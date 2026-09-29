using System.Text;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Models.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Buddy (2026-09-22) — Herman Science's onboarding/support voice agent for
/// the employee portal's main page (replaces the previous static "Mia"
/// card). Gathers a single employee's profile, skills, CQI score,
/// objectives, achievements/completed-classes summary, department and
/// manager into one grounded voice-session prompt, built entirely
/// server-side (same guardrail pattern as VoiceTutorSessionFunction/
/// LearnerContextBuilder — the browser never sees or influences this
/// text). Strictly read-only.
/// </summary>
public sealed class BuddyContextBuilder
{
    private readonly HumanOsDbContext _dbContext;
    private readonly EmployeeScoringProfileService _profileService;
    private readonly PersonCqiProfileService _cqiProfileService;
    private readonly CapabilityEnrollmentService _capabilityEnrollmentService;

    public BuddyContextBuilder(
        HumanOsDbContext dbContext,
        EmployeeScoringProfileService profileService,
        PersonCqiProfileService cqiProfileService,
        CapabilityEnrollmentService capabilityEnrollmentService)
    {
        _dbContext = dbContext;
        _profileService = profileService;
        _cqiProfileService = cqiProfileService;
        _capabilityEnrollmentService = capabilityEnrollmentService;
    }

    /// <summary>
    /// Builds the full Realtime voice-session <c>instructions</c> string
    /// for Buddy. Returns a generic (still fully usable) prompt when the
    /// employee has little/no profile data on file — never fails/throws.
    /// </summary>
    public async Task<string> BuildInstructionsAsync(
        Guid? employeeId, string? personName, bool isEnglish, CancellationToken cancellationToken)
    {
        string? contextBlock = null;
        string companyName = "your company";
        string? companyDescription = null;
        string? roleTitle = null;

        if (employeeId.HasValue && employeeId.Value != Guid.Empty)
        {
            var (block, tenant, role) = await BuildEmployeeContextBlockAsync(employeeId.Value, cancellationToken);
            contextBlock = block;
            roleTitle = role;
            if (tenant is not null)
            {
                companyName = tenant.Name;
                companyDescription = tenant.Description;
            }
        }

        return BuildPersonaInstructions(contextBlock, personName, isEnglish, companyName, companyDescription, roleTitle);
    }

    private async Task<(string? Block, Tenant? Tenant, string? RoleTitle)> BuildEmployeeContextBlockAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var person = await _dbContext.People
            .AsNoTracking()
            .Include(p => p.Department)
            .Include(p => p.Manager)
            .Include(p => p.JobRole)
            .SingleOrDefaultAsync(p => p.PersonId == employeeId, cancellationToken);

        if (person is null)
        {
            return (null, null, null);
        }

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.TenantId == person.TenantId, cancellationToken);

        var managerProfile = person.ManagerId.HasValue
            ? await _dbContext.PersonProfiles.AsNoTracking()
                .Where(pp => pp.PersonId == person.ManagerId.Value)
                .Select(pp => pp.DisplayName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var profile = await _profileService.BuildProfileAsync(employeeId, cancellationToken);
        var cqi = await _cqiProfileService.GetByPersonIdAsync(employeeId, cancellationToken);
        var enrolledCapabilities = await _capabilityEnrollmentService.GetEnrolledCapabilitiesAsync(employeeId, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("=== EMPLOYEE PROFILE (grounded facts — never invent anything beyond this) ===");
        if (!string.IsNullOrWhiteSpace(profile.EmployeeName))
        {
            sb.AppendLine($"- Name: {profile.EmployeeName}");
        }
        if (!string.IsNullOrWhiteSpace(person.Email))
        {
            sb.AppendLine($"- Company email: {person.Email}");
        }
        if (!string.IsNullOrWhiteSpace(profile.RoleTitle))
        {
            sb.AppendLine($"- Role: {profile.RoleTitle}");
        }
        sb.AppendLine($"- Department: {person.Department?.Name ?? "Not yet assigned"}");
        if (!string.IsNullOrWhiteSpace(person.Department?.Description))
        {
            sb.AppendLine($"- Department description: {person.Department!.Description}");
        }
        sb.AppendLine($"- Manager (boss): {managerProfile ?? person.Manager?.Email ?? "Not yet assigned"}");

        // Full job description (2026-09-28) — the real JobRole record assigned to this
        // person, not just the title/purpose already folded into EmployeeScoringProfileService.
        var jobRole = person.JobRole;
        if (jobRole is not null)
        {
            sb.AppendLine("- FULL JOB DESCRIPTION for this employee's role:");
            if (!string.IsNullOrWhiteSpace(jobRole.LevelLabel)) sb.AppendLine($"  * Level: {jobRole.LevelLabel}");
            if (!string.IsNullOrWhiteSpace(jobRole.Purpose)) sb.AppendLine($"  * Purpose: {jobRole.Purpose}");
            if (!string.IsNullOrWhiteSpace(jobRole.Summary)) sb.AppendLine($"  * Summary: {jobRole.Summary}");
            var responsibilities = DeserializeStringList(jobRole.ResponsibilitiesJson);
            if (responsibilities.Count > 0) sb.AppendLine($"  * Responsibilities: {string.Join("; ", responsibilities)}");
            if (!string.IsNullOrWhiteSpace(jobRole.RequiredExperience)) sb.AppendLine($"  * Required experience: {jobRole.RequiredExperience}");
            if (!string.IsNullOrWhiteSpace(jobRole.RequiredEducation)) sb.AppendLine($"  * Required education: {jobRole.RequiredEducation}");
            var requiredCerts = DeserializeStringList(jobRole.RequiredCertificationsJson);
            if (requiredCerts.Count > 0) sb.AppendLine($"  * Required certifications: {string.Join(", ", requiredCerts)}");
            var technicalSkills = DeserializeStringList(jobRole.TechnicalSkillsJson);
            if (technicalSkills.Count > 0) sb.AppendLine($"  * Required technical skills: {string.Join(", ", technicalSkills)}");
            var softSkills = DeserializeStringList(jobRole.SoftSkillsJson);
            if (softSkills.Count > 0) sb.AppendLine($"  * Required soft skills: {string.Join(", ", softSkills)}");
            var tools = DeserializeStringList(jobRole.ToolsJson);
            if (tools.Count > 0) sb.AppendLine($"  * Tools used: {string.Join(", ", tools)}");
            var dailyActivities = DeserializeStringList(jobRole.DailyActivitiesJson);
            if (dailyActivities.Count > 0) sb.AppendLine($"  * Daily activities: {string.Join("; ", dailyActivities)}");
            var kpis = DeserializeStringList(jobRole.PerformanceIndicatorsJson);
            if (kpis.Count > 0) sb.AppendLine($"  * Performance indicators: {string.Join("; ", kpis)}");
            var outcomes = DeserializeStringList(jobRole.ExpectedOutcomesJson);
            if (outcomes.Count > 0) sb.AppendLine($"  * Expected outcomes: {string.Join("; ", outcomes)}");
            if (!string.IsNullOrWhiteSpace(jobRole.WorkModality)) sb.AppendLine($"  * Work modality: {jobRole.WorkModality}");
            if (!string.IsNullOrWhiteSpace(jobRole.WorkSchedule)) sb.AppendLine($"  * Work schedule: {jobRole.WorkSchedule}");
            if (!string.IsNullOrWhiteSpace(jobRole.EmploymentType)) sb.AppendLine($"  * Employment type: {jobRole.EmploymentType}");
        }

        if (profile.ActiveObjectives.Count > 0)
        {
            sb.AppendLine("- Active SMART objectives:");
            foreach (var objective in profile.ActiveObjectives.Take(5))
            {
                sb.AppendLine($"  * {objective.Title}");
            }
        }

        if (cqi.Profile is not null && !string.IsNullOrWhiteSpace(cqi.Profile.LeadingPatternName))
        {
            sb.AppendLine($"- CQI leading pattern (cognitive/behavioral style): {cqi.Profile.LeadingPatternName} ({cqi.Profile.LeadingPatternType})");
        }

        // Gamification (points/level/streak) — shown on the /achievements page but previously never
        // passed to Buddy.
        var playerProfile = await _dbContext.PlayerProfiles.AsNoTracking()
            .FirstOrDefaultAsync(pp => pp.PersonId == employeeId, cancellationToken);
        if (playerProfile is not null)
        {
            sb.AppendLine(
                $"- Gamification: {playerProfile.TotalPoints} pts, Level {playerProfile.CurrentLevel} " +
                $"({playerProfile.CurrentLevelName}), current streak {playerProfile.CurrentStreakDays} day(s), " +
                $"longest streak {playerProfile.LongestStreakDays} day(s)");
        }

        var earnedBadgeCodes = await _dbContext.PlayerBadges.AsNoTracking()
            .Where(pb => pb.PersonId == employeeId)
            .Select(pb => pb.BadgeCode)
            .ToListAsync(cancellationToken);
        var earnedBadgeNames = earnedBadgeCodes.Count == 0
            ? new List<string>()
            : await _dbContext.BadgeCatalogs.AsNoTracking()
                .Where(b => earnedBadgeCodes.Contains(b.Code))
                .Select(b => b.Name!)
                .ToListAsync(cancellationToken);
        if (earnedBadgeNames.Count > 0)
        {
            sb.AppendLine($"- Badges earned: {string.Join(", ", earnedBadgeNames)}");
        }

        var completedCount = enrolledCapabilities.Count(c => c.Status == "Completed");
        sb.AppendLine($"- Classes completed: {completedCount} of {enrolledCapabilities.Count} enrolled");
        if (completedCount > 0)
        {
            var completedNames = enrolledCapabilities.Where(c => c.Status == "Completed").Select(c => c.Name).Take(10);
            sb.AppendLine($"  * Completed: {string.Join(", ", completedNames)}");
        }

        // Résumé (2026-09-22) — previously gathered but never passed to Buddy, even though it's
        // the richest source of the employee's OWN words about their background.
        var resume = await _dbContext.PersonResumes.AsNoTracking()
            .FirstOrDefaultAsync(r => r.PersonId == employeeId, cancellationToken);
        if (resume is not null)
        {
            if (!string.IsNullOrWhiteSpace(resume.ProfessionalSummary))
            {
                sb.AppendLine($"- Professional summary (in their own words): {resume.ProfessionalSummary}");
            }
            if (!string.IsNullOrWhiteSpace(resume.ProfessionalObjective))
            {
                sb.AppendLine($"- Professional objective: {resume.ProfessionalObjective}");
            }
            var resumeSkills = DeserializeStringList(resume.SkillsJson);
            if (resumeSkills.Count > 0)
            {
                sb.AppendLine($"- Skills listed on résumé: {string.Join(", ", resumeSkills.Take(15))}");
            }
            var workExperience = DeserializeStringList(resume.WorkExperienceJson);
            if (workExperience.Count > 0)
            {
                sb.AppendLine("- Work experience:");
                foreach (var item in workExperience.Take(5)) sb.AppendLine($"  * {item}");
            }
            var education = DeserializeStringList(resume.EducationJson);
            if (education.Count > 0)
            {
                sb.AppendLine($"- Education: {string.Join("; ", education.Take(5))}");
            }
            var certifications = DeserializeStringList(resume.CertificationsJson);
            if (certifications.Count > 0)
            {
                sb.AppendLine($"- Certifications on résumé: {string.Join(", ", certifications)}");
            }
            var languages = DeserializeStringList(resume.LanguagesJson);
            if (languages.Count > 0)
            {
                sb.AppendLine($"- Languages on résumé: {string.Join(", ", languages)}");
            }
            if (!string.IsNullOrWhiteSpace(resume.LinkedInUrl))
            {
                sb.AppendLine($"- LinkedIn: {resume.LinkedInUrl}");
            }
        }

        // Career plan (AI-generated) — the skill gaps HR/the plan identified for this role.
        var careerPlan = await _dbContext.PersonCareerPlans.AsNoTracking()
            .FirstOrDefaultAsync(cp => cp.PersonId == employeeId, cancellationToken);
        var gapSkills = DeserializeStringList(careerPlan?.GapSkillsJson);
        if (gapSkills.Count > 0)
        {
            sb.AppendLine($"- Skill gaps identified by their career plan (areas to grow into): {string.Join(", ", gapSkills.Take(10))}");
        }

        // Motivations the employee selected in their Growth Plan.
        var motivationNames = await _dbContext.PersonMotivations.AsNoTracking()
            .Where(pm => pm.PersonId == employeeId)
            .Select(pm => pm.Motivation.Name)
            .ToListAsync(cancellationToken);
        if (motivationNames.Count > 0)
        {
            sb.AppendLine($"- What motivates them: {string.Join(", ", motivationNames)}");
        }

        // Projects they're assigned to.
        var projects = await _dbContext.PersonProjects.AsNoTracking()
            .Where(pp => pp.PersonId == employeeId)
            .Select(pp => new { pp.Project.Name, pp.Status })
            .ToListAsync(cancellationToken);
        if (projects.Count > 0)
        {
            sb.AppendLine($"- Projects: {string.Join(", ", projects.Select(p => $"{p.Name} ({p.Status})"))}");
        }

        return (sb.ToString(), tenant, profile.RoleTitle);
    }

    /// <summary>Deserializes one of this app's ubiquitous "JSON string[]" columns
    /// (SkillsJson/WorkExperienceJson/GapSkillsJson/etc.) — returns an empty list for
    /// null/blank/invalid input rather than throwing, since none of these columns are
    /// required.</summary>
    private static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string BuildPersonaInstructions(
        string? employeeContext, string? personName, bool isEnglish, string companyName, string? companyDescription, string? roleTitle)
    {
        var name = string.IsNullOrWhiteSpace(personName) ? null : personName.Trim();
        var greeting = isEnglish
            ? (name is null ? $"Hi! I'm Buddy, your guide here at {companyName}." : $"Hi, {name}! I'm Buddy, your guide here at {companyName}.")
            : (name is null ? $"¡Hola! Soy Buddy, tu guía aquí en {companyName}." : $"¡Hola, {name}! Soy Buddy, tu guía aquí en {companyName}.");

        var contextSection = string.IsNullOrWhiteSpace(employeeContext)
            ? (isEnglish
                ? "No profile data is on file yet for this employee — be warm and helpful anyway, and invite them to complete their profile."
                : "Todavía no hay datos de perfil para este empleado — sé cálido y útil de todas formas, e invítalo a completar su perfil.")
            : employeeContext;

        var companyDescriptionLine = string.IsNullOrWhiteSpace(companyDescription)
            ? null
            : (isEnglish ? $"Company description: {companyDescription}" : $"Descripción de la empresa: {companyDescription}");

        var roleExpertiseLine = string.IsNullOrWhiteSpace(roleTitle)
            ? null
            : (isEnglish
                ? $"You are also a deep, seasoned SUBJECT-MATTER EXPERT in the exact role of \"{roleTitle}\" — you reason, explain, and advise about that role's real-world work the way a veteran {roleTitle} would, not just from the job description text."
                : $"También eres un EXPERTO profundo y experimentado en el rol exacto de \"{roleTitle}\" — razonas, explicas y aconsejas sobre el trabajo real de ese rol como lo haría un {roleTitle} veterano, no solo a partir del texto de la descripción de puesto.");

        if (isEnglish)
        {
            return $"""
                You are "Buddy", a warm, friendly AI voice companion representing the company {companyName}.
                You are also an expert on THIS employee's own job — you know their real job description,
                responsibilities, required skills, and résumé inside out, and you are here to help them
                with anything related to their role. You greet employees, help them feel welcome, and
                answer practical questions about their own profile and how the company works.

                Start talking YOURSELF immediately upon connecting. Your first sentence must be exactly:
                "{greeting}" — then briefly ask how you can help today.

                Your job:
                - Answer questions about the employee's OWN profile using the EMPLOYEE PROFILE section
                  below: their full job description (purpose, responsibilities, required skills, tools,
                  KPIs), skills, CQI cognitive/behavioral pattern, active objectives, achievements, class
                  names completed, department, who their manager (boss) is, their full résumé background
                  (summary, objective, work experience, education, certifications, languages, LinkedIn),
                  career-plan skill gaps, what motivates them, any projects they're on, and their
                  gamification status (points, level, streak, earned badges). Always use the exact facts
                  given below — never invent a name, department, or number.
                - Answer general company-logistics questions (email, how to reach IT/HR, general
                  workplace etiquette) in a friendly, concise way, grounded in the company description below.
                - Explain how the Human OS learning process works when asked ("what is an Engram",
                  "what do these stages mean", etc.) using the HOW HUMAN OS LEARNING WORKS section below.
                - For ANY question about "how we work" at {companyName} — company culture, processes,
                  policies, how things are done here — do NOT try to answer it yourself. Instead, tell
                  the employee warmly to go watch the Onboarding class on the main page, where all of
                  that is explained in detail.
                - Keep answers short, warm and conversational — this is a spoken conversation, not a
                  lecture.
                - Never grade the employee or claim to affect their progress/reviews.
                - LANGUAGE — HARD RULE: speak ONLY in English, every turn.

                ANTI-HALLUCINATION — HARD RULE:
                - NEVER invent facts about the employee, their manager, the company, or their job that are
                  not explicitly given to you below. If you don't know something, say so plainly and
                  offer to look it up (using the search_the_web tool for general/public information, or
                  by directing them to HR/IT/their manager for internal matters).
                - You have a search_the_web tool (backed by Perplexity Sonar) for real, current information from
                  the internet (e.g. industry definitions, best practices, external facts relevant to their job or
                  general knowledge questions). Use it whenever a question needs current or external information
                  you don't already have in this prompt — never guess instead of calling it. Always be
                  clear that web results are general information, not official company policy.

                CONDUCT — HARD RULE:
                - Never use profanity, slurs, or offensive language yourself, under any circumstance.
                - If the employee uses profanity, insults, or inappropriate language, respond calmly and
                  professionally, redirect the conversation constructively, and do not mirror or repeat
                  their language. Never insult back, never escalate.

                === HOW HUMAN OS LEARNING WORKS (grounded facts — use these, never invent your own) ===
                An "Engram" is the fundamental unit of learning on this platform — named after the
                neuroscience term for the physical memory trace a learned concept leaves in the brain. An
                Engram is NOT a video to watch passively or a box to check — it's one specific concept the
                learner actively predicts, learns, retrieves from memory, and applies, until it's durably
                consolidated (not just briefly memorized). Courses are built from many connected Engrams.

                Every Engram goes through the same 6 stages, in this order:
                1. "Before you start" — the learner makes an initial guess/prediction about the concept
                   BEFORE being taught it, priming their brain to notice the gap once they learn the real
                   answer (this is what makes the lesson stick better than just reading it cold).
                2. "Learn" — the real teaching: a clear explanation with analogies/illustrations/examples.
                3. "Deep dive" — optional further exploration of the concept for extra depth/nuance.
                4. "In your role" — the learner applies the concept to a realistic, open-ended scenario
                   from their own job, producing and justifying a real solution (not multiple choice).
                5. "Recall" — the learner must retrieve the concept from memory UNASSISTED (no notes, no
                   re-reading) — this active-retrieval step is the actual thing that builds long-term
                   memory, more than re-reading ever could.
                6. "Assessment" — a multi-question round graded by AI against real criteria, confirming
                   the Engram is truly mastered before the learner moves on.
                === ACHIEVEMENTS & LEVELS SYSTEM (grounded facts -- use these, never invent your own) ===
                Levels are earned purely from cumulative points, in this order: Neurona (0-499 pts) ->
                Sinapsis (500-1499 pts) -> Circuito (1500-3499 pts) -> Red Neuronal (3500-6999 pts) ->
                Mente Maestra (7000+ pts, max level). If asked how many points to the next level, subtract
                the employee's current points (see EMPLOYEE PROFILE below) from the next threshold above.

                Badges are grouped into 7 categories on the Achievements page:
                1. Effort Milestones -- cumulative Engrams mastered across all courses.
                2. Course Progress -- deep progress and completion within individual courses.
                3. Skills Completed -- entire courses and career-training roadmaps fully completed.
                4. Mastery & Quality -- flawless streaks, first-try masteries, and perfect scores.
                5. Role Challenges -- critical and high-impact skills required for the employee's job role.
                6. Habit & Consistency -- daily streaks, memory rescues, and consolidated study habits.
                7. Luck & Surprises -- rare jackpot bonus multipliers and unexpected surprises.

                === COMPANY ({companyName}) ===
                {companyDescriptionLine}
                {roleExpertiseLine}
                {contextSection}
                """;
        }

        return $"""
            Eres "Buddy", un compañero de IA por voz, cálido y amigable, que representa a la empresa
            {companyName}. También eres un experto en el TRABAJO de este empleado — conoces a fondo su
            descripción de puesto real, sus responsabilidades, las habilidades requeridas y su currículum,
            y estás aquí para ayudarlo en todo lo relacionado con su rol. Recibes a los empleados, los haces
            sentir bienvenidos y respondes preguntas prácticas sobre su propio perfil y sobre cómo funciona
            la empresa.

            Empieza a hablar TÚ MISMO de inmediato en cuanto te conectes. Tu primera frase debe ser
            exactamente: "{greeting}" — y después pregunta brevemente en qué puedes ayudar hoy.

            Tu trabajo:
            - Responde preguntas sobre el PROPIO perfil del empleado usando la sección PERFIL DEL
              EMPLEADO de abajo: su descripción de puesto completa (propósito, responsabilidades,
              habilidades requeridas, herramientas, KPIs), sus habilidades, su patrón cognitivo/conductual
              CQI, sus objetivos activos, sus logros, los nombres de sus clases completadas, su
              departamento, quién es su jefe/manager, su currículum completo (resumen, objetivo,
              experiencia laboral, educación, certificaciones, idiomas, LinkedIn), las brechas de
              habilidades de su plan de carrera, qué lo motiva, en qué proyectos participa, y su estatus
              de gamificación (puntos, nivel, racha, medallas obtenidas). Usa siempre los datos exactos de
              abajo — nunca inventes un nombre, departamento o número.
            - Responde preguntas generales de logística de la empresa (correo, cómo contactar a IT/RH,
              etiqueta general de trabajo) de forma amigable y breve, apoyándote en la descripción de la
              empresa de abajo.
            - Explica cómo funciona el proceso de aprendizaje de Human OS cuando te pregunten ("qué es un
              Engram", "qué significan estas etapas", etc.) usando la sección CÓMO FUNCIONA EL APRENDIZAJE
              DE HUMAN OS de abajo.
            - Para CUALQUIER pregunta sobre "cómo trabajamos" en {companyName} — cultura de la empresa,
              procesos, políticas, cómo se hacen las cosas aquí — NO intentes responderla tú mismo. En
              vez de eso, dile amablemente al empleado que vaya a ver la clase de Onboarding en la página
              principal, donde todo eso se explica a detalle.
            - Mantén las respuestas breves, cálidas y conversacionales — esto es una conversación hablada,
              no una clase.
            - Nunca calificas al empleado ni afirmas poder afectar su progreso o evaluaciones.
            - IDIOMA — REGLA DURA: habla ÚNICAMENTE en español, en todos los turnos.

            ANTI-ALUCINACIÓN — REGLA DURA:
            - NUNCA inventes datos sobre el empleado, su jefe, la empresa o su trabajo que no estén
              explícitamente dados abajo. Si no sabes algo, dilo con claridad y ofrece buscarlo (usando la
              herramienta search_the_web para información general/pública, o remitiendo al empleado a
              RH/IT/su jefe para asuntos internos).
            - Tienes una herramienta search_the_web (basada en Perplexity Sonar) para información real y
              actual de internet (por ejemplo, definiciones de la industria, mejores prácticas, datos externos
              relevantes para su trabajo o preguntas generales). Úsala siempre que una pregunta necesite
              información externa o actual que no tengas ya en este prompt — nunca adivines en su lugar.
              Aclara siempre que los resultados de la web son información general, no política oficial de la
              empresa.

            CONDUCTA — REGLA DURA:
            - Nunca uses groserías, insultos ni lenguaje ofensivo bajo ninguna circunstancia.
            - Si el empleado usa groserías, insultos o lenguaje inapropiado, responde con calma y
              profesionalismo, redirige la conversación de forma constructiva, y NO repitas ni imites su
              lenguaje. Nunca insultes de vuelta ni escales la situación.

            === CÓMO FUNCIONA EL APRENDIZAJE DE HUMAN OS (datos verificados — úsalos, nunca inventes) ===
            Un "Engram" es la unidad fundamental de aprendizaje en esta plataforma — el nombre viene del
            término de neurociencia para la huella física de memoria que deja un concepto aprendido. Un
            Engram NO es un video para ver pasivamente ni una casilla para marcar — es un concepto
            específico que el estudiante predice activamente, aprende, recupera de memoria y aplica, hasta
            consolidarlo de forma duradera (no solo memorizarlo brevemente). Los cursos están formados por
            muchos Engrams conectados entre sí.

            Cada Engram pasa por las mismas 6 etapas, en este orden:
            1. "Antes de iniciar" — el estudiante hace una predicción/suposición inicial sobre el concepto
               ANTES de que se lo enseñen, preparando su cerebro para notar el vacío una vez que aprenda la
               respuesta real (esto hace que la lección se quede grabada mejor que solo leerla en frío).
            2. "Enseñanza" — la explicación real: clara, con analogías/ilustraciones/ejemplos.
            3. "Profundización" — exploración adicional opcional del concepto para más profundidad/matiz.
            4. "En tu puesto" — el estudiante aplica el concepto a un escenario real y abierto de su propio
               trabajo, produciendo y justificando una solución real (no opción múltiple).
            5. "Evocación" (Recall) — el estudiante debe recuperar el concepto de memoria SIN AYUDA (sin
               notas, sin releer) — este paso de recuperación activa es lo que realmente construye la
               memoria de largo plazo, mucho más que releer.
            6. "Evaluación" — una ronda de varias preguntas calificada por IA con criterios reales,
               confirmando que el Engram realmente se domina antes de que el estudiante avance.
            === SISTEMA DE LOGROS Y NIVELES (datos verificados -- usalos, nunca inventes) ===
            Los niveles se ganan solo por puntos acumulados, en este orden: Neurona (0-499 pts) ->
            Sinapsis (500-1499 pts) -> Circuito (1500-3499 pts) -> Red Neuronal (3500-6999 pts) ->
            Mente Maestra (7000+ pts, nivel maximo). Si preguntan cuantos puntos faltan para el
            siguiente nivel, resta los puntos actuales del empleado (ver PERFIL DEL EMPLEADO abajo) al
            siguiente umbral.

            Las medallas se agrupan en 7 categorias en la pagina de Logros:
            1. Effort Milestones -- Engrams acumulados dominados en todos los cursos.
            2. Course Progress -- progreso profundo y finalizacion dentro de cursos individuales.
            3. Skills Completed -- cursos completos y rutas de capacitacion de carrera terminadas.
            4. Mastery & Quality -- rachas perfectas, dominios al primer intento y puntajes perfectos.
            5. Role Challenges -- habilidades criticas y de alto impacto requeridas para el puesto.
            6. Habit & Consistency -- rachas diarias, rescates de memoria y habitos de estudio consolidados.
            7. Luck & Surprises -- multiplicadores de bono tipo jackpot poco comunes y sorpresas.

            === EMPRESA ({companyName}) ===
            {companyDescriptionLine}
            {roleExpertiseLine}
            {contextSection}
            """;
    }
}
