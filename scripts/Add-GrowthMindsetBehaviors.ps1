<#
.SYNOPSIS
    Seeds the 20 standard Growth Mindset culture principle behaviors (EN/ES) via the
    Enterprise Evolution Culture API.

.DESCRIPTION
    Looks up the CulturePrinciple with PrincipleCode = GROWTH_MINDSET and creates each
    CulturePrincipleBehavior record (ExpectedBehavior, BehaviorToAvoid, LeadershipExpectation,
    EmployeeExpectation, AITransformationImplication) through the running Azure Functions API.
    Existing behaviors with the same BehaviorCode are skipped (idempotent).

.PARAMETER ApiBaseUrl
    Base URL of the running HumanOS Functions host. Defaults to the local dev host.

.PARAMETER CreatedByPersonId
    PersonId to stamp as CreatedByPersonId on each behavior. Defaults to the Growth Mindset
    principle's OwnerPersonId when not supplied.

.EXAMPLE
    ./Add-GrowthMindsetBehaviors.ps1
#>
param(
    [string]$ApiBaseUrl = "http://localhost:7071/api",
    [string]$CreatedByPersonId
)

$ErrorActionPreference = "Stop"

function Get-GrowthMindsetPrinciple {
    param([string]$BaseUrl)

    $response = Invoke-RestMethod -Uri "$BaseUrl/enterprise-evolution/culture/principles" -Method Get
    $principle = $response.principles | Where-Object { $_.principleCode -eq "GROWTH_MINDSET" } | Select-Object -First 1

    if (-not $principle) {
        throw "No CulturePrinciple found with PrincipleCode 'GROWTH_MINDSET'. Create it first."
    }

    return $principle
}

$behaviors = @(
    @{ BehaviorCode = "ASK_QUESTIONS"; BehaviorType = "ExpectedBehavior"; SortOrder = 1
       DescriptionEn = "Ask questions when information, expectations, instructions, or outcomes are unclear."
       DescriptionEs = "Hacer preguntas cuando la información, las expectativas, las instrucciones o los resultados no sean claros." }
    @{ BehaviorCode = "SEEK_FEEDBACK"; BehaviorType = "ExpectedBehavior"; SortOrder = 2
       DescriptionEn = "Actively request feedback from appropriate people and systems to identify opportunities for improvement."
       DescriptionEs = "Solicitar activamente retroalimentación de las personas y sistemas apropiados para identificar oportunidades de mejora." }
    @{ BehaviorCode = "APPLY_FEEDBACK"; BehaviorType = "ExpectedBehavior"; SortOrder = 3
       DescriptionEn = "Use relevant feedback to improve the next decision, action, practice attempt, or work result."
       DescriptionEs = "Utilizar la retroalimentación relevante para mejorar la siguiente decisión, acción, práctica o resultado de trabajo." }
    @{ BehaviorCode = "PRACTICE_NEW_BEHAVIORS"; BehaviorType = "ExpectedBehavior"; SortOrder = 4
       DescriptionEn = "Practice unfamiliar behaviors in approved environments until the required level of performance is demonstrated."
       DescriptionEs = "Practicar comportamientos nuevos en entornos aprobados hasta demostrar el nivel de desempeño requerido." }
    @{ BehaviorCode = "LEARN_FROM_RESULTS"; BehaviorType = "ExpectedBehavior"; SortOrder = 5
       DescriptionEn = "Review successful and unsuccessful results, identify useful lessons, and apply those lessons to future performance."
       DescriptionEs = "Revisar los resultados exitosos y no exitosos, identificar aprendizajes útiles y aplicarlos al desempeño futuro." }
    @{ BehaviorCode = "SHARE_LEARNING"; BehaviorType = "ExpectedBehavior"; SortOrder = 6
       DescriptionEn = "Share relevant lessons, effective practices, identified risks, and improvements with other people and teams."
       DescriptionEs = "Compartir aprendizajes relevantes, prácticas efectivas, riesgos identificados y mejoras con otras personas y equipos." }

    @{ BehaviorCode = "HIDE_MISTAKES"; BehaviorType = "BehaviorToAvoid"; SortOrder = 7
       DescriptionEn = "Hide mistakes, inaccurate results, capability gaps, or important uncertainties that others need to understand."
       DescriptionEs = "Ocultar errores, resultados inexactos, brechas de capacidad o incertidumbres importantes que otras personas necesitan comprender." }
    @{ BehaviorCode = "PRETEND_TO_UNDERSTAND"; BehaviorType = "BehaviorToAvoid"; SortOrder = 8
       DescriptionEn = "Act as though information or instructions are understood when clarification or additional learning is needed."
       DescriptionEs = "Actuar como si se comprendiera la información o las instrucciones cuando se requiere aclaración o aprendizaje adicional." }
    @{ BehaviorCode = "REJECT_FEEDBACK_WITHOUT_REVIEW"; BehaviorType = "BehaviorToAvoid"; SortOrder = 9
       DescriptionEn = "Reject relevant feedback without examining the evidence, reasoning, or opportunity for improvement."
       DescriptionEs = "Rechazar retroalimentación relevante sin examinar la evidencia, el razonamiento o la oportunidad de mejora." }
    @{ BehaviorCode = "TREAT_CAPABILITY_AS_FIXED"; BehaviorType = "BehaviorToAvoid"; SortOrder = 10
       DescriptionEn = "Assume that a person cannot develop a capability solely because the person has not demonstrated it yet."
       DescriptionEs = "Asumir que una persona no puede desarrollar una capacidad únicamente porque todavía no la ha demostrado." }

    @{ BehaviorCode = "PROVIDE_PRACTICE_TIME"; BehaviorType = "LeadershipExpectation"; SortOrder = 11
       DescriptionEn = "Provide reasonable time, access, tools, and opportunities for people to practice the behaviors required by a transformation."
       DescriptionEs = "Proporcionar tiempo, acceso, herramientas y oportunidades razonables para practicar los comportamientos requeridos por una transformación." }
    @{ BehaviorCode = "MODEL_CONTINUOUS_LEARNING"; BehaviorType = "LeadershipExpectation"; SortOrder = 12
       DescriptionEn = "Demonstrate curiosity, request feedback, acknowledge uncertainty, and show how learning influences decisions."
       DescriptionEs = "Demostrar curiosidad, solicitar retroalimentación, reconocer la incertidumbre y mostrar cómo el aprendizaje influye en las decisiones." }
    @{ BehaviorCode = "RESPOND_CONSTRUCTIVELY_TO_MISTAKES"; BehaviorType = "LeadershipExpectation"; SortOrder = 13
       DescriptionEn = "Distinguish responsible learning and experimentation from negligence, and respond in a way that supports correction, learning, and accountability."
       DescriptionEs = "Distinguir el aprendizaje y la experimentación responsable de la negligencia, y responder promoviendo la corrección, el aprendizaje y la responsabilidad." }
    @{ BehaviorCode = "REMOVE_LEARNING_BARRIERS"; BehaviorType = "LeadershipExpectation"; SortOrder = 14
       DescriptionEn = "Identify and remove organizational barriers such as insufficient time, access, tools, support, or clear expectations."
       DescriptionEs = "Identificar y eliminar barreras organizacionales como tiempo, acceso, herramientas, apoyo o expectativas claras insuficientes." }

    @{ BehaviorCode = "OWN_CAPABILITY_DEVELOPMENT"; BehaviorType = "EmployeeExpectation"; SortOrder = 15
       DescriptionEn = "Participate actively in relevant learning, practice, feedback, and capability-development experiences."
       DescriptionEs = "Participar activamente en experiencias relevantes de aprendizaje, práctica, retroalimentación y desarrollo de capacidades." }
    @{ BehaviorCode = "ESCALATE_IMPORTANT_UNCERTAINTY"; BehaviorType = "EmployeeExpectation"; SortOrder = 16
       DescriptionEn = "Escalate important uncertainty, unsupported information, or potential risk instead of presenting it as verified."
       DescriptionEs = "Escalar incertidumbre importante, información no sustentada o riesgos potenciales en lugar de presentarlos como verificados." }

    @{ BehaviorCode = "AI_SUPPORT_PRACTICE_AND_RETRY"; BehaviorType = "AITransformationImplication"; SortOrder = 17
       DescriptionEn = "AI tutors, coaches, and simulations should provide useful feedback and appropriate opportunities to practice and try again."
       DescriptionEs = "Los tutores, coaches y simulaciones de IA deben proporcionar retroalimentación útil y oportunidades apropiadas para practicar e intentarlo nuevamente." }
    @{ BehaviorCode = "AI_AVOID_FIXED_LABELS"; BehaviorType = "AITransformationImplication"; SortOrder = 18
       DescriptionEn = "AI must not label a person as incapable, resistant, or unwilling based on an isolated response, conversation, assessment, or early practice attempt."
       DescriptionEs = "La IA no debe etiquetar a una persona como incapaz, resistente o indispuesta basándose en una respuesta, conversación, evaluación o práctica inicial aislada." }
    @{ BehaviorCode = "AI_EXPLAIN_IMPROVEMENT_OPPORTUNITIES"; BehaviorType = "AITransformationImplication"; SortOrder = 19
       DescriptionEn = "AI feedback should identify the observed gap, explain why it matters, and provide a clear and achievable next step."
       DescriptionEs = "La retroalimentación de IA debe identificar la brecha observada, explicar por qué es importante y proporcionar un siguiente paso claro y alcanzable." }
    @{ BehaviorCode = "AI_USE_EVIDENCE_FOR_READINESS"; BehaviorType = "AITransformationImplication"; SortOrder = 20
       DescriptionEn = "Readiness decisions should use demonstrated performance and relevant evidence rather than course completion or technology usage alone."
       DescriptionEs = "Las decisiones de preparación deben utilizar desempeño demostrado y evidencia relevante, no únicamente la finalización de cursos o el uso de tecnología." }
)

$principle = Get-GrowthMindsetPrinciple -BaseUrl $ApiBaseUrl
$principleId = $principle.culturePrincipleId

if (-not $CreatedByPersonId) {
    $CreatedByPersonId = $principle.ownerPersonId
}

Write-Host "Growth Mindset CulturePrincipleId: $principleId" -ForegroundColor Cyan

$existing = Invoke-RestMethod -Uri "$ApiBaseUrl/enterprise-evolution/culture/principles/$principleId/behaviors" -Method Get
$existingCodes = @($existing.behaviors | ForEach-Object { $_.behaviorCode })

$created = 0
$skipped = 0

foreach ($b in $behaviors) {
    if ($existingCodes -contains $b.BehaviorCode) {
        Write-Host "SKIP  $($b.BehaviorCode) (already exists)" -ForegroundColor Yellow
        $skipped++
        continue
    }

    $body = @{
        behaviorCode      = $b.BehaviorCode
        behaviorType      = $b.BehaviorType
        descriptionEn     = $b.DescriptionEn
        descriptionEs     = $b.DescriptionEs
        sortOrder         = $b.SortOrder
        isActive          = $true
        createdByPersonId = $CreatedByPersonId
    } | ConvertTo-Json

    try {
        Invoke-RestMethod -Uri "$ApiBaseUrl/enterprise-evolution/culture/principles/$principleId/behaviors" -Method Post -Body $body -ContentType "application/json" | Out-Null
        Write-Host "OK    $($b.BehaviorCode)" -ForegroundColor Green
        $created++
    } catch {
        Write-Host "ERROR $($b.BehaviorCode): $_" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Done. Created: $created, Skipped (already existed): $skipped, Total requested: $($behaviors.Count)" -ForegroundColor Cyan
