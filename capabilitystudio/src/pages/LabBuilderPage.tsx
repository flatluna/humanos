import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { Sparkles, ArrowLeft, Lightbulb } from 'lucide-react';
import {
  generateLabDraft,
  LAB_ARCHETYPES,
  type GenerateLabDraftRequest,
  type GenerateLabDraftResponse,
  type SkillToEvaluate,
} from '../lib/api/labsApi';
import { getJobRoles, type JobRoleDto } from '../lib/api/jobRolesApi';
import { useAuth } from '../auth/AuthContext';
import { DEFAULT_STUDIO_TENANT_ID } from '../lib/api/pdfPipelineApi';
import SearchableCombobox, { type ComboboxOption } from '../components/SearchableCombobox';
import LabDraftPreview from '../components/LabDraftPreview';

const EMPTY_SKILL: SkillToEvaluate = { SkillName: '' };

/** Same display convention as NewCapabilityPage's job-role picker — two roles
 * can share a Title in different departments. */
function jobRoleOptionLabel(role: JobRoleDto): string {
  const withLevel = role.LevelLabel ? `${role.Title} (${role.LevelLabel})` : role.Title;
  return `${withLevel} — ${role.DepartmentName}`;
}

/** Sample text-only request (no PDF) for quickly trying out the AI Lab Builder. */
const EXAMPLE_FORM: GenerateLabDraftRequest = {
  LabName: 'Negociación y Cierre de Contratos B2B',
  TargetRole: 'Ejecutivo de Cuentas Senior (Account Executive)',
  LabObjective: 'Conducir la sesión final de negociación contractual con el Director Financiero de un cliente corporativo, defendiendo el valor de la propuesta y cerrando el acuerdo sin otorgar descuentos no autorizados.',
  ProcessName: 'Metodología de Negociación Estratégica (Harvard)',
  Archetype: 'CLIENT_NEGOTIATION',
  ProcessContent:
    'MANUAL INTERNO DE NEGOCIACIÓN COMERCIAL — MÉTODO HARVARD DE NEGOCIACIÓN BASADA EN PRINCIPIOS\n' +
    'Sección 4: Negociación de contratos B2B con cuentas corporativas\n\n' +
    'Principio rector: separar a las personas del problema y negociar en función de intereses, no de posiciones. ' +
    'El ejecutivo de cuentas nunca debe entrar en un regateo directo de precio; su labor es entender por qué el cliente ' +
    'pide lo que pide y resolver esa necesidad real con la menor concesión de valor posible.\n\n' +
    'Paso 1 — Diagnóstico de intereses: antes de responder cualquier objeción de precio, el ejecutivo debe indagar qué hay ' +
    'detrás de la posición del cliente (presión presupuestal del trimestre, comparación con un competidor, inseguridad sobre el ROI, etc.). ' +
    'Una posición ("quiero 25% de descuento") casi siempre esconde un interés distinto y más fácil de satisfacer.\n\n' +
    'Paso 2 — Criterios objetivos: todo acuerdo debe anclarse en referencias externas verificables (precios de mercado, ' +
    'SLAs estándar de la industria, benchmarks de cumplimiento normativo) en vez de en quién presiona más fuerte.\n\n' +
    'Paso 3 — Opciones de beneficio mutuo: antes de tocar el precio base, el ejecutivo debe explorar variables de valor que no cuestan margen ' +
    '(soporte técnico prioritario, capacitación adicional, tiempos de implementación) y ofrecerlas como alternativa real al descuento.\n\n' +
    'Paso 4 — Concesiones condicionadas: ninguna concesión se otorga sin obtener algo a cambio (p. ej. condicionar un beneficio extra a un pago anual anticipado). ' +
    'Las concesiones nunca son unilaterales.\n\n' +
    'Paso 5 — Cierre formal: el acuerdo se resume por escrito de forma explícita antes de solicitar la firma, dejando constancia de qué se acordó y qué no.',
  SkillsToEvaluate: [
    { SkillName: 'Negociación estratégica y concesiones condicionales', SkillType: 'TECHNICAL' },
    { SkillName: 'Manejo de objeciones bajo presión', SkillType: 'SOFT' },
  ],
  ScenarioDescription: 'El Director Financiero de un cliente corporativo exige un descuento del 25% para firmar hoy el contrato anual, amenazando con posponer la decisión al próximo trimestre si no se concede.',
  LabLanguage: 'es-MX',
  Difficulty: 'ADVANCED',
  DurationMinutes: 25,
  DialogueCount: 1,
};

/** Sample text-only request for a technical/behavioral job interview use case (Forward
 * Deployed Engineer role) — shows how JOB_INTERVIEW archetype Labs should be filled: the
 * ProcessContent IS the job description + required skills, so the live interviewer agent
 * actually reads and evaluates against it (not a generic "be nice" checklist). */
const EXAMPLE_FORM_INTERVIEW: GenerateLabDraftRequest = {
  LabName: 'Technical & Behavioral Interview — Forward Deployed Engineer',
  TargetRole: 'Forward Deployed Engineer (FDE)',
  LabObjective: 'Conduct a complete technical and behavioral interview to assess whether the candidate has the technical depth, tolerance for ambiguity, and client-communication skills required for the Forward Deployed Engineer role, probing for real examples from their experience.',
  ProcessName: 'Interview Guide for Forward Deployed Engineer (FDE)',
  Archetype: 'JOB_INTERVIEW',
  ProcessContent:
    'JOB DESCRIPTION — FORWARD DEPLOYED ENGINEER (FDE)\n\n' +
    'We are looking for a Forward Deployed Engineer: a software engineer who works directly with customers to implement, ' +
    'customize, and optimize complex technical systems. Unlike a traditional backend or product engineer, the FDE operates ' +
    'at the intersection of engineering and real-world deployment — ensuring software solutions integrate seamlessly into ' +
    "the client's workflows, infrastructure, and business processes.\n\n" +
    'KEY RESPONSIBILITIES (the FDE owns the project end-to-end):\n' +
    "1. Custom solution development: analyze client requirements and design bespoke integrations (APIs, AI models, " +
    'workflow automations), optimizing for performance, scalability, and low latency.\n' +
    '2. Deployment & production scaling: transform prototypes into secure, compliant production systems; handle data ' +
    'migrations, cloud setup, and infrastructure scaling.\n' +
    '3. Real-time troubleshooting: debug live environments, resolve schema conflicts and edge cases, tune performance amid ' +
    'operational demands.\n' +
    '4. Stakeholder collaboration: lead client workshops, translate business needs into technical specifications, and loop ' +
    'in internal teams with deployment insights.\n' +
    '5. Knowledge transfer & enablement: train teams, document workflows, and provide ongoing support for sustained ' +
    'adoption.\n\n' +
    'ESSENTIAL SKILLS THE INTERVIEWER MUST EVALUATE:\n' +
    '- Technical mastery: full-stack development (Python, Java, SQL), API development and integration, distributed ' +
    'systems and microservices, cloud platforms (AWS/Azure/GCP), data engineering, AI/ML systems (LLM fine-tuning, ' +
    'vector databases, MLOps basics).\n' +
    '- Problem-solving under ambiguity: comfort working with incomplete or evolving specifications, ability to rapidly ' +
    'prototype solutions, strong debugging skills in live production systems, iterative development with continuous ' +
    'feedback loops.\n' +
    '- Communication & empathy: translating complex technical concepts into simple business language, leading customer ' +
    'workshops and technical discussions, active listening, building long-term client relationships.\n' +
    '- Business & compliance awareness: understanding customer workflows, operational constraints, ROI-driven solution ' +
    'design, regulatory frameworks (GDPR, data privacy), risk management in AI/data deployments.\n' +
    '- Ownership & adaptability: taking full responsibility for project outcomes, thriving in both startup and enterprise ' +
    'environments, managing multiple priorities, maintaining a continuous learning mindset.\n\n' +
    'GUIDANCE FOR THE INTERVIEWER: use STAR-style behavioral questions (Situation, Task, Action, Result) to probe real ' +
    'past examples, and technical system-design questions to validate depth. Never accept generic or theoretical answers ' +
    '— always push for a concrete example from a real project, with specific numbers and decisions.',
  SkillsToEvaluate: [
    { SkillName: 'System design and integration architecture (APIs, microservices, cloud)', SkillType: 'TECHNICAL' },
    { SkillName: 'Problem-solving under ambiguity and production debugging', SkillType: 'TECHNICAL' },
    { SkillName: 'Technical communication with non-technical stakeholders', SkillType: 'SOFT' },
    { SkillName: 'End-to-end ownership and accountability under ambiguity', SkillType: 'SOFT' },
  ],
  ScenarioDescription: 'The candidate enters a one-hour final-round interview for the Forward Deployed Engineer role, where a technical hiring manager will assess their real experience deploying AI/software solutions directly with enterprise clients, under ambiguous and shifting requirements.',
  LabLanguage: 'en-US',
  Difficulty: 'ADVANCED',
  DurationMinutes: 30,
  DialogueCount: 1,
};


/** AI Lab Builder (AGE-LAB-001) form — generates a reusable Lab draft for
 * human review. Nothing is persisted by this call; see
 * HumanOS.SimulationLabs.Api Features/AiLabBuilder. */
export default function LabBuilderPage() {
  const { t, i18n } = useTranslation();
  const { user } = useAuth();
  const effectiveTenantId = user?.tenantId ?? DEFAULT_STUDIO_TENANT_ID;
  const [form, setForm] = useState<GenerateLabDraftRequest>({
    LabName: '',
    TargetRole: '',
    LabObjective: '',
    ProcessName: '',
    Archetype: 'CLIENT_NEGOTIATION',
    ProcessContent: '',
    SkillsToEvaluate: [{ ...EMPTY_SKILL }],
    ScenarioDescription: '',
    LabLanguage: 'en-US',
    Difficulty: 'INTERMEDIATE',
    DurationMinutes: 30,
    DialogueCount: 1,
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<GenerateLabDraftResponse | null>(null);
  const [pdfFileName, setPdfFileName] = useState<string | null>(null);
  const [extractingPdf, setExtractingPdf] = useState(false);
  const [jobRoles, setJobRoles] = useState<JobRoleDto[]>([]);
  const [jobRoleSearch, setJobRoleSearch] = useState('');

  useEffect(() => {
    let cancelled = false;
    getJobRoles(effectiveTenantId)
      .then((roles) => {
        if (!cancelled) setJobRoles(roles);
      })
      .catch(() => {
        if (!cancelled) setJobRoles([]);
      });
    return () => {
      cancelled = true;
    };
  }, [effectiveTenantId]);

  const jobRoleOptions: ComboboxOption[] = jobRoles.map((role) => ({ id: role.JobRoleId, label: jobRoleOptionLabel(role) }));
  const selectedJobRole = jobRoles.find((role) => jobRoleOptionLabel(role) === jobRoleSearch) ?? null;
  const [checkedSkills, setCheckedSkills] = useState<Record<string, boolean>>({});

  // Reset the checklist whenever the selected job role changes, and auto-fill Target Role
  // right away — there's no separate "Target role" text field anymore (it was a duplicate
  // of this picker), so this is the only place TargetRole gets set.
  useEffect(() => {
    setCheckedSkills({});
    if (selectedJobRole) {
      setForm((prev) => ({ ...prev, TargetRole: jobRoleOptionLabel(selectedJobRole) }));
    }
  }, [selectedJobRole]);

  function toggleSkillChecked(skillName: string) {
    setCheckedSkills((prev) => ({ ...prev, [skillName]: !prev[skillName] }));
  }

  /** Adds only the checked skills from the selected job role's Technical/Soft lists —
   * replaces auto-dumping every skill from the role (the admin now picks which ones matter
   * for this specific Lab). */
  function addCheckedSkills() {
    if (!selectedJobRole) return;
    const technical = (selectedJobRole.TechnicalSkills || []).filter((name) => checkedSkills[name]);
    const soft = (selectedJobRole.SoftSkills || []).filter((name) => checkedSkills[name]);
    const toAdd: SkillToEvaluate[] = [
      ...technical.map((name) => ({ SkillName: name, SkillType: 'TECHNICAL' as const })),
      ...soft.map((name) => ({ SkillName: name, SkillType: 'SOFT' as const })),
    ];
    if (toAdd.length === 0) return;
    setForm((prev) => ({
      ...prev,
      SkillsToEvaluate: [...prev.SkillsToEvaluate.filter((s) => s.SkillName.trim() !== ''), ...toAdd],
    }));
    setCheckedSkills({});
  }

  function updateSkill(index: number, field: keyof SkillToEvaluate, value: string) {
    setForm((prev) => ({
      ...prev,
      SkillsToEvaluate: prev.SkillsToEvaluate.map((s, i) => (i === index ? { ...s, [field]: value } : s)),
    }));
  }

  function addSkill() {
    setForm((prev) => ({
      ...prev,
      SkillsToEvaluate: [...prev.SkillsToEvaluate, { SkillName: '', SkillType: 'TECHNICAL' }],
    }));
  }

  function removeSkill(index: number) {
    setForm((prev) => ({ ...prev, SkillsToEvaluate: prev.SkillsToEvaluate.filter((_, i) => i !== index) }));
  }

  async function handlePdfChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    setExtractingPdf(true);
    setError(null);
    try {
      const contentBase64 = await fileToBase64(file);
      setForm((prev) => ({ ...prev, ProcessFileName: file.name, ProcessContentBase64: contentBase64, ProcessContent: '' }));
      setPdfFileName(file.name);
    } catch {
      setError(t('labs.builder.pdfReadError'));
    } finally {
      setExtractingPdf(false);
      e.target.value = '';
    }
  }

  function removePdf() {
    setPdfFileName(null);
    setForm((prev) => ({ ...prev, ProcessFileName: undefined, ProcessContentBase64: undefined }));
  }

  function loadExample() {
    setPdfFileName(null);
    setForm({ ...EXAMPLE_FORM, SkillsToEvaluate: EXAMPLE_FORM.SkillsToEvaluate.map((s) => ({ ...s })) });
    setError(null);
    setResult(null);
  }

  function loadExampleInterview() {
    setPdfFileName(null);
    setForm({ ...EXAMPLE_FORM_INTERVIEW, SkillsToEvaluate: EXAMPLE_FORM_INTERVIEW.SkillsToEvaluate.map((s) => ({ ...s })) });
    setError(null);
    setResult(null);
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (result) {
      const confirmMessage = i18n.language.startsWith('es')
        ? '¿Estás seguro que quieres generar el draft de nuevo? Se perderá el borrador actual (a menos que ya lo hayas guardado).'
        : 'Are you sure you want to generate the draft again? The current draft will be lost (unless you already saved it).';
      if (!window.confirm(confirmMessage)) return;
    }
    setLoading(true);
    setError(null);
    setResult(null);
    try {
      const response = await generateLabDraft(form);
      setResult(response);
      setTimeout(() => {
        document.getElementById('lab-builder-results')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }, 100);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error generando el borrador.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="mx-auto max-w-4xl px-4 py-8 sm:px-6">
      <div className="mb-6 flex items-center gap-3">
        <Sparkles className="h-7 w-7 text-brand-400" />
        <div>
          <h1 className="text-xl font-semibold text-white">{t('labs.builder.title')}</h1>
          <p className="text-sm text-slate-400">{t('labs.builder.subtitle')}</p>
          <p className="mt-1 text-xs text-slate-500">
            <span className="text-red-400">*</span> {t('labs.builder.requiredFieldsNote')}
          </p>
        </div>
      </div>

      <Link to="/labs" className="mb-4 inline-flex items-center gap-1 text-sm text-slate-400 hover:text-white">
        <ArrowLeft className="h-4 w-4" /> {t('nav.labs')}
      </Link>

      <div className="mb-5 flex flex-col items-start justify-between gap-3 rounded-xl border border-brand-500/30 bg-brand-500/10 p-4 sm:flex-row sm:flex-wrap sm:items-center">
        <div className="flex items-start gap-3">
          <div className="rounded-lg bg-brand-500/20 p-2 text-brand-300">
            <Lightbulb className="h-5 w-5" />
          </div>
          <div>
            <h3 className="text-sm font-semibold text-brand-200">{t('labs.builder.exampleBannerTitle')}</h3>
            <p className="text-xs text-slate-300">{t('labs.builder.exampleBannerDesc')}</p>
          </div>
        </div>
        <button
          type="button"
          onClick={loadExample}
          className="inline-flex shrink-0 items-center gap-1.5 rounded-lg border border-brand-400/40 bg-brand-500/20 px-3.5 py-2 text-xs font-semibold text-brand-100 shadow-sm transition hover:bg-brand-500/30 hover:text-white"
        >
          {t('labs.builder.loadExampleButton')}
        </button>
        <button
          type="button"
          onClick={loadExampleInterview}
          className="inline-flex shrink-0 items-center gap-1.5 rounded-lg border border-brand-400/40 bg-brand-500/20 px-3.5 py-2 text-xs font-semibold text-brand-100 shadow-sm transition hover:bg-brand-500/30 hover:text-white"
        >
          ⚡ Fill with example (FDE Interview)
        </button>
      </div>

      <form onSubmit={handleSubmit} className="space-y-4 rounded-2xl border border-white/10 bg-slate-900/60 p-4">
        <div className="rounded-xl border-2 border-brand-500/40 bg-brand-500/5 p-4">
          <p className="mb-2 text-sm font-semibold text-brand-300">{t('labs.builder.step1Label')}</p>
          <Field label={t('labs.builder.processContentLabel')} required>
            <p className="mb-2 text-xs text-slate-500">{t('labs.builder.processContentHint')}</p>
            {pdfFileName ? (
              <div className="flex items-center justify-between rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2">
                <span className="text-sm text-slate-300">{t('labs.builder.uploadedFileLabel')}: {pdfFileName}</span>
                <button type="button" onClick={removePdf} className="text-xs text-red-400 hover:text-red-300">
                  {t('labs.builder.removeFileButton')}
                </button>
              </div>
            ) : (
              <div className="space-y-2">
                <label className="inline-flex cursor-pointer items-center gap-2 rounded-lg border border-brand-400/40 bg-brand-500/10 px-4 py-3 text-sm font-medium text-brand-200 hover:bg-brand-500/20">
                  {extractingPdf ? t('labs.builder.extractingPdf') : t('labs.builder.uploadPdfButton')}
                  <input type="file" accept="application/pdf" className="hidden" onChange={handlePdfChange} disabled={extractingPdf} />
                </label>
                <p className="text-xs text-slate-500">{t('labs.builder.orTypeText')}</p>
                <textarea
                  className="input"
                  rows={6}
                  value={form.ProcessContent}
                  onChange={(e) => setForm({ ...form, ProcessContent: e.target.value })}
                />
              </div>
            )}
          </Field>
        </div>

        <div className="grid grid-cols-1 gap-4">
          <Field label="Lab name (opcional)">
            <p className="mb-1 text-xs text-slate-500">Déjalo vacío para que el agente proponga un nombre a partir del proceso/rol.</p>
            <input className="input" value={form.LabName} onChange={(e) => setForm({ ...form, LabName: e.target.value })} />
          </Field>
        </div>

        <Field label="Lab objective" required>
          <textarea className="input" rows={2} value={form.LabObjective} onChange={(e) => setForm({ ...form, LabObjective: e.target.value })} required />
        </Field>

        <Field label={t('labs.builder.archetypeLabel')} required>
          <p className="mb-1 text-xs text-slate-500">{t('labs.builder.archetypeHint')}</p>
          <select className="input" value={form.Archetype} onChange={(e) => setForm({ ...form, Archetype: e.target.value })}>
            {LAB_ARCHETYPES.map((a) => (
              <option key={a.Code} value={a.Code}>
                {i18n.language.startsWith('es') ? a.LabelEs : a.LabelEn}
              </option>
            ))}
          </select>
          <p className="mt-1 text-xs text-slate-500">
            {i18n.language.startsWith('es')
              ? LAB_ARCHETYPES.find((a) => a.Code === form.Archetype)?.DescriptionEs
              : LAB_ARCHETYPES.find((a) => a.Code === form.Archetype)?.DescriptionEn}
          </p>
        </Field>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Field label="Process name" required>
            <p className="mb-1 text-xs text-slate-500">{t('labs.builder.processNameHint')}</p>
            <input className="input" value={form.ProcessName} onChange={(e) => setForm({ ...form, ProcessName: e.target.value })} required />
          </Field>
          <Field label="Difficulty" required>
            <select className="input" value={form.Difficulty} onChange={(e) => setForm({ ...form, Difficulty: e.target.value as GenerateLabDraftRequest['Difficulty'] })}>
              <option value="BEGINNER">BEGINNER</option>
              <option value="INTERMEDIATE">INTERMEDIATE</option>
              <option value="ADVANCED">ADVANCED</option>
            </select>
          </Field>
        </div>

        <Field label="Scenario description (opcional)">
          <p className="mb-1 text-xs text-slate-500">Déjalo vacío para que el agente proponga un escenario a partir del proceso. Este texto se guarda literal, nunca lo reescribe el agente.</p>
          <textarea className="input" rows={3} value={form.ScenarioDescription} onChange={(e) => setForm({ ...form, ScenarioDescription: e.target.value })} />
        </Field>

        <Field label="Company context (opcional)">
          <p className="mb-1 text-xs text-slate-500">Contexto de la empresa/organización detrás del escenario (industria, tamaño, estructura, etc.). Se guarda literal y el agente lo usa como referencia, sin contradecirlo.</p>
          <textarea className="input" rows={2} value={form.CompanyContext ?? ''} onChange={(e) => setForm({ ...form, CompanyContext: e.target.value })} />
        </Field>

        <div className="rounded-xl border border-white/10 bg-white/[0.02] p-3">
          <p className="mb-2 text-sm font-medium text-slate-300">{t('labs.builder.jobRoleLabel')}</p>
          <p className="mb-2 text-xs text-slate-500">{t('labs.builder.jobRoleHint')}</p>
          <SearchableCombobox
            value={jobRoleSearch}
            onChange={setJobRoleSearch}
            options={jobRoleOptions}
            placeholder={t('labs.builder.jobRolePlaceholder')}
            emptyOptionsText={t('labs.builder.jobRoleNoOptions')}
          />
          {selectedJobRole && (
            <div className="mt-3 space-y-3">
              {(selectedJobRole.TechnicalSkills?.length ?? 0) > 0 && (
                <div>
                  <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-400">🛠️ Técnicas</p>
                  <div className="space-y-1">
                    {selectedJobRole.TechnicalSkills!.map((name) => (
                      <label key={name} className="flex items-center gap-2 text-sm text-slate-300">
                        <input type="checkbox" checked={Boolean(checkedSkills[name])} onChange={() => toggleSkillChecked(name)} />
                        {name}
                      </label>
                    ))}
                  </div>
                </div>
              )}
              {(selectedJobRole.SoftSkills?.length ?? 0) > 0 && (
                <div>
                  <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-400">🧠 Soft skills</p>
                  <div className="space-y-1">
                    {selectedJobRole.SoftSkills!.map((name) => (
                      <label key={name} className="flex items-center gap-2 text-sm text-slate-300">
                        <input type="checkbox" checked={Boolean(checkedSkills[name])} onChange={() => toggleSkillChecked(name)} />
                        {name}
                      </label>
                    ))}
                  </div>
                </div>
              )}
              <button
                type="button"
                onClick={addCheckedSkills}
                disabled={!Object.values(checkedSkills).some(Boolean)}
                className="whitespace-nowrap rounded-lg bg-brand-500/20 px-3 py-2 text-sm font-medium text-brand-200 hover:bg-brand-500/30 disabled:opacity-40"
              >
                + Agregar skills seleccionados
              </button>
            </div>
          )}
        </div>

        <div>
          <div className="mb-2 flex items-center justify-between">
            <label className="text-sm font-medium text-slate-300">
              {t('labs.builder.skillsToEvaluateLabel')}
              <span className="ml-0.5 text-red-400"> *</span>
            </label>
            <button type="button" onClick={addSkill} className="text-xs text-brand-400 hover:text-brand-300">
              + Add skill
            </button>
          </div>
          <p className="mb-2 text-xs text-slate-500">{t('labs.builder.skillsToEvaluateHint')}</p>
          <div className="space-y-2">
            {form.SkillsToEvaluate.map((skill, index) => (
              <div key={index} className="grid grid-cols-1 gap-2 sm:grid-cols-[140px_1fr_auto]">
                <select
                  className="input text-xs"
                  value={skill.SkillType ?? 'TECHNICAL'}
                  onChange={(e) => updateSkill(index, 'SkillType', e.target.value)}
                >
                  <option value="TECHNICAL">🛠️ Técnica</option>
                  <option value="SOFT">🧠 Soft Skill</option>
                </select>
                <input
                  className="input"
                  placeholder="Nombre de la habilidad a evaluar"
                  value={skill.SkillName}
                  onChange={(e) => updateSkill(index, 'SkillName', e.target.value)}
                  required
                />
                <button type="button" onClick={() => removeSkill(index)} className="text-xs text-red-400 hover:text-red-300">
                  Remove
                </button>
              </div>
            ))}
          </div>
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
          <Field label="Language">
            <input className="input" value={form.LabLanguage} onChange={(e) => setForm({ ...form, LabLanguage: e.target.value })} />
          </Field>
          <Field label="Duration (minutes)">
            <input
              type="number"
              className="input"
              value={form.DurationMinutes}
              onChange={(e) => setForm({ ...form, DurationMinutes: Number(e.target.value) })}
            />
          </Field>
          <Field label="Dialogue count">
            <p className="mb-1 text-xs text-slate-500">Fijo en 1: solo la línea de apertura del actor. El resto de la reunión es libre, guiada por el escenario/skills/rubrica.</p>
            <input
              type="number"
              className="input"
              value={1}
              disabled
            />
          </Field>
        </div>

        <button
          type="submit"
          disabled={loading}
          className="rounded-lg bg-brand-500 px-4 py-2 text-sm font-medium text-white hover:bg-brand-400 disabled:opacity-50"
        >
          {loading ? t('labs.builder.generating') : t('labs.builder.generateDraftButton')}
        </button>
      </form>

      {error && <p className="mt-4 text-sm text-red-400">{error}</p>}

      {result && <LabDraftPreview response={result} requestContext={form} />}
    </div>
  );
}

function Field({ label, required, children }: { label: string; required?: boolean; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1 block text-sm font-medium text-slate-300">
        {label}
        {required && <span className="ml-0.5 text-red-400"> *</span>}
      </span>
      {children}
    </label>
  );
}

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = reader.result as string;
      resolve(result.substring(result.indexOf(',') + 1));
    };
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(file);
  });
}
