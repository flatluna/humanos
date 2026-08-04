import { useEffect, useMemo, useRef, useState, type FormEvent, type ReactNode } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useParams } from 'react-router';
import { ArrowLeft, Briefcase, Sparkles } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import {
  createJobRole,
  generateJobRoleDescription,
  getDepartments,
  getJobRole,
  getJobRoles,
  updateJobRole,
  type GenerateJobRoleDescriptionRequest,
  type GenerateJobRoleDescriptionResponse,
  type SaveJobRoleRequest,
} from '@/api/orgStructureApi';

function linesToArray(value: string): string[] {
  return value
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean);
}

function arrayToLines(value: string[] | undefined): string {
  return (value ?? []).join('\n');
}

type RoleFormState = typeof emptyForm;
type RoleFormKey = keyof RoleFormState;

/// One row of the "Generar con IA" before/after review panel — `applyValue` is already
/// shaped exactly like the form field it targets (a joined multi-line string for list
/// fields, a plain string for text fields, a boolean for requiresTravel) so applying an
/// accepted row is a single assignment, never a re-parse.
interface DescriptionDiffRow {
  key: RoleFormKey;
  label: string;
  before: string;
  after: string;
  applyValue: string | boolean;
}

/// Compares the AI proposal against the CURRENT form snapshot (taken at the moment the
/// proposal arrived — never re-diffed against a form the user keeps editing) and returns
/// only the fields that genuinely changed, for the before/after review UI.
function buildDescriptionDiffRows(
  formSnapshot: RoleFormState,
  proposal: GenerateJobRoleDescriptionResponse,
): DescriptionDiffRow[] {
  const rows: DescriptionDiffRow[] = [];

  function addText(key: RoleFormKey, label: string, current: string, proposed: string | null | undefined) {
    const proposedTrimmed = (proposed ?? '').trim();
    if (!proposedTrimmed || proposedTrimmed === current.trim()) return;
    rows.push({ key, label, before: current.trim() || '(vacío)', after: proposedTrimmed, applyValue: proposedTrimmed });
  }

  function addList(key: RoleFormKey, label: string, current: string, proposed: string[]) {
    const proposedLines = arrayToLines(proposed).trim();
    if (!proposedLines || proposedLines === current.trim()) return;
    rows.push({ key, label, before: current.trim() || '(vacío)', after: proposedLines, applyValue: proposedLines });
  }

  addText('levelLabel', 'Nivel / Seniority', formSnapshot.levelLabel, proposal.levelLabel);
  addText('purpose', 'Propósito', formSnapshot.purpose, proposal.purpose);
  addText('summary', 'Resumen', formSnapshot.summary, proposal.summary);
  addList('responsibilities', 'Responsabilidades principales', formSnapshot.responsibilities, proposal.responsibilities);
  addList('dailyActivities', 'Actividades diarias o periódicas', formSnapshot.dailyActivities, proposal.dailyActivities);
  addText('requiredExperience', 'Experiencia requerida', formSnapshot.requiredExperience, proposal.requiredExperience);
  addText('requiredEducation', 'Educación requerida', formSnapshot.requiredEducation, proposal.requiredEducation);
  addList('technicalSkills', 'Habilidades técnicas', formSnapshot.technicalSkills, proposal.technicalSkills);
  addList('requiredCertifications', 'Certificaciones requeridas', formSnapshot.requiredCertifications, proposal.requiredCertifications);
  addList('requiredLanguages', 'Idiomas requeridos', formSnapshot.requiredLanguages, proposal.requiredLanguages);
  addList('softSkills', 'Habilidades blandas', formSnapshot.softSkills, proposal.softSkills);
  addList('tools', 'Herramientas / tecnologías', formSnapshot.tools, proposal.tools);
  addList('performanceIndicators', 'Indicadores de desempeño', formSnapshot.performanceIndicators, proposal.performanceIndicators);
  addList('expectedOutcomes', 'Objetivos del puesto', formSnapshot.expectedOutcomes, proposal.expectedOutcomes);
  addText('workSchedule', 'Horario de trabajo', formSnapshot.workSchedule, proposal.workSchedule);
  addText('workModality', 'Modalidad de trabajo', formSnapshot.workModality, proposal.workModality);
  addText('workLocation', 'Ubicación', formSnapshot.workLocation, proposal.workLocation);
  addText('availabilityNotes', 'Disponibilidad', formSnapshot.availabilityNotes, proposal.availabilityNotes);
  addText('employmentType', 'Tipo de contratación', formSnapshot.employmentType, proposal.employmentType);
  addList('benefits', 'Prestaciones', formSnapshot.benefits, proposal.benefits);
  addText('salaryCurrency', 'Moneda', formSnapshot.salaryCurrency, proposal.salaryCurrency);

  if (proposal.requiresTravel !== formSnapshot.requiresTravel) {
    rows.push({
      key: 'requiresTravel',
      label: 'Requiere viajes',
      before: formSnapshot.requiresTravel ? 'Sí' : 'No',
      after: proposal.requiresTravel ? 'Sí' : 'No',
      applyValue: proposal.requiresTravel,
    });
  }

  if (proposal.salaryRangeMin != null && String(proposal.salaryRangeMin) !== formSnapshot.salaryRangeMin) {
    rows.push({
      key: 'salaryRangeMin',
      label: 'Salario mínimo',
      before: formSnapshot.salaryRangeMin || '(vacío)',
      after: String(proposal.salaryRangeMin),
      applyValue: String(proposal.salaryRangeMin),
    });
  }

  if (proposal.salaryRangeMax != null && String(proposal.salaryRangeMax) !== formSnapshot.salaryRangeMax) {
    rows.push({
      key: 'salaryRangeMax',
      label: 'Salario máximo',
      before: formSnapshot.salaryRangeMax || '(vacío)',
      after: String(proposal.salaryRangeMax),
      applyValue: String(proposal.salaryRangeMax),
    });
  }

  return rows;
}

const emptyForm = {
  departmentId: '',
  title: '',
  levelLabel: '',
  purpose: '',
  summary: '',
  responsibilities: '',
  dailyActivities: '',
  requiredExperience: '',
  requiredEducation: '',
  technicalSkills: '',
  requiredCertifications: '',
  requiredLanguages: '',
  softSkills: '',
  departmentProcesses: '',
  hrProcesses: '',
  tools: '',
  reportsToJobRoleId: '',
  performanceIndicators: '',
  workSchedule: '',
  workModality: '',
  requiresTravel: false,
  workLocation: '',
  availabilityNotes: '',
  employmentType: '',
  salaryRangeMin: '',
  salaryRangeMax: '',
  salaryCurrency: '',
  benefits: '',
  expectedOutcomes: '',
};

const inputClass =
  'mt-1 w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white';
const labelClass = 'block text-xs font-medium text-slate-500 dark:text-white/50';

function Section({
  number,
  title,
  description,
  children,
}: {
  number: number;
  title: string;
  description?: string;
  children: ReactNode;
}) {
  return (
    <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5">
      <div>
        <h2 className="flex items-center gap-2 text-sm font-semibold text-slate-900 dark:text-white">
          <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-amber-100 text-xs font-bold text-amber-700 dark:bg-amber-500/15 dark:text-amber-300">
            {number}
          </span>
          {title}
        </h2>
        {description && <p className="mt-1 pl-8 text-xs text-slate-400 dark:text-white/40">{description}</p>}
      </div>
      <div className="pl-8">{children}</div>
    </section>
  );
}

export function RoleDetailPage() {
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const params = useParams<{ jobRoleId?: string }>();
  const jobRoleId = params.jobRoleId;
  const isEditing = Boolean(jobRoleId);

  const [form, setForm] = useState(emptyForm);
  const [reviewRows, setReviewRows] = useState<DescriptionDiffRow[] | null>(null);
  const [acceptedKeys, setAcceptedKeys] = useState<Set<RoleFormKey>>(new Set());
  // Lets the HRAdmin tweak the AI's "after" text per field before applying it — starts as a
  // copy of each row's applyValue and only diverges once the user edits that field's box.
  const [editedValues, setEditedValues] = useState<Partial<Record<RoleFormKey, string | boolean>>>({});

  const departmentsQuery = useQuery({
    queryKey: ['departments', tenantId],
    queryFn: () => getDepartments(tenantId),
  });

  const jobRolesQuery = useQuery({
    queryKey: ['job-roles', tenantId],
    queryFn: () => getJobRoles(tenantId),
  });

  const roleQuery = useQuery({
    queryKey: ['job-role', jobRoleId],
    queryFn: () => getJobRole(jobRoleId!),
    enabled: isEditing,
  });

  // Seeds the form from the fetched role exactly once per jobRoleId — after that, the
  // form is fully user-controlled so a background refetch (e.g. window refocus, or the
  // invalidation triggered by updateMutation's own onSuccess) never clobbers an in-flight
  // edit with stale/duplicate data. Same pattern as EmployeeDetailPage's orgForm guard.
  const seededJobRoleIdRef = useRef<string | null>(null);

  useEffect(() => {
    if (roleQuery.data && seededJobRoleIdRef.current !== jobRoleId) {
      seededJobRoleIdRef.current = jobRoleId ?? null;
      const r = roleQuery.data;
      setForm({
        departmentId: r.departmentId,
        title: r.title,
        levelLabel: r.levelLabel ?? '',
        purpose: r.purpose ?? '',
        summary: r.summary ?? '',
        responsibilities: arrayToLines(r.responsibilities),
        dailyActivities: arrayToLines(r.dailyActivities),
        requiredExperience: r.requiredExperience ?? '',
        requiredEducation: r.requiredEducation ?? '',
        technicalSkills: arrayToLines(r.technicalSkills),
        requiredCertifications: arrayToLines(r.requiredCertifications),
        requiredLanguages: arrayToLines(r.requiredLanguages),
        softSkills: arrayToLines(r.softSkills),
        departmentProcesses: arrayToLines(r.departmentProcesses),
        hrProcesses: arrayToLines(r.hrProcesses),
        tools: arrayToLines(r.tools),
        reportsToJobRoleId: r.reportsToJobRoleId ?? '',
        performanceIndicators: arrayToLines(r.performanceIndicators),
        workSchedule: r.workSchedule ?? '',
        workModality: r.workModality ?? '',
        requiresTravel: r.requiresTravel,
        workLocation: r.workLocation ?? '',
        availabilityNotes: r.availabilityNotes ?? '',
        employmentType: r.employmentType ?? '',
        salaryRangeMin: r.salaryRangeMin?.toString() ?? '',
        salaryRangeMax: r.salaryRangeMax?.toString() ?? '',
        salaryCurrency: r.salaryCurrency ?? '',
        benefits: arrayToLines(r.benefits),
        expectedOutcomes: arrayToLines(r.expectedOutcomes),
      });
    } else if (!isEditing && departmentsQuery.data?.length) {
      setForm((f) => (f.departmentId ? f : { ...f, departmentId: departmentsQuery.data![0].departmentId }));
    }
  }, [roleQuery.data, isEditing, departmentsQuery.data, jobRoleId]);

  const otherJobRoles = useMemo(
    () => (jobRolesQuery.data ?? []).filter((r) => r.jobRoleId !== jobRoleId),
    [jobRolesQuery.data, jobRoleId],
  );

  const createMutation = useMutation({
    mutationFn: (body: SaveJobRoleRequest) => createJobRole(tenantId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId] });
      navigate('/roles');
    },
  });

  const updateMutation = useMutation({
    mutationFn: (body: SaveJobRoleRequest) => updateJobRole(jobRoleId!, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId] });
      void queryClient.invalidateQueries({ queryKey: ['job-role', jobRoleId] });
      navigate('/roles');
    },
  });

  const isSaving = createMutation.isPending || updateMutation.isPending;
  const mutationError = createMutation.error ?? updateMutation.error;

  // "Generar con IA" — JobRoleDescriptionAgent (Microsoft Agent Framework, backend) drafts
  // or improves this role's full description from the real company/department/title
  // context plus this organization's own similar job roles. Returns a pure PROPOSAL — it
  // is never written into `form` directly; buildDescriptionDiffRows() computes a
  // before/after diff against a snapshot of the form taken right now, and the review panel
  // below lets the HRAdmin accept/reject each field individually before anything changes.
  const generateMutation = useMutation({
    mutationFn: () => {
      const body: GenerateJobRoleDescriptionRequest = {
        jobRoleId: jobRoleId ?? null,
        departmentId: form.departmentId,
        title: form.title,
        levelLabel: form.levelLabel || null,
        purpose: form.purpose || null,
        summary: form.summary || null,
        responsibilities: linesToArray(form.responsibilities),
        requiredExperience: form.requiredExperience || null,
        requiredEducation: form.requiredEducation || null,
        requiredCertifications: linesToArray(form.requiredCertifications),
        requiredLanguages: linesToArray(form.requiredLanguages),
        technicalSkills: linesToArray(form.technicalSkills),
        softSkills: linesToArray(form.softSkills),
        tools: linesToArray(form.tools),
        dailyActivities: linesToArray(form.dailyActivities),
        performanceIndicators: linesToArray(form.performanceIndicators),
        expectedOutcomes: linesToArray(form.expectedOutcomes),
        workSchedule: form.workSchedule || null,
        workModality: form.workModality || null,
        requiresTravel: form.requiresTravel,
        workLocation: form.workLocation || null,
        availabilityNotes: form.availabilityNotes || null,
        employmentType: form.employmentType || null,
        salaryRangeMin: form.salaryRangeMin ? Number(form.salaryRangeMin) : null,
        salaryRangeMax: form.salaryRangeMax ? Number(form.salaryRangeMax) : null,
        salaryCurrency: form.salaryCurrency || null,
        benefits: linesToArray(form.benefits),
      };
      return generateJobRoleDescription(tenantId, body);
    },
    onSuccess: (data) => {
      const rows = buildDescriptionDiffRows(form, data);
      setReviewRows(rows);
      setAcceptedKeys(new Set(rows.map((row) => row.key)));
      setEditedValues(Object.fromEntries(rows.map((row) => [row.key, row.applyValue])));
    },
  });

  function toggleAccepted(key: RoleFormKey) {
    setAcceptedKeys((prev) => {
      const next = new Set(prev);
      if (next.has(key)) {
        next.delete(key);
      } else {
        next.add(key);
      }
      return next;
    });
  }

  function updateEditedValue(key: RoleFormKey, value: string | boolean) {
    setEditedValues((prev) => ({ ...prev, [key]: value }));
  }

  function closeReview() {
    setReviewRows(null);
    setEditedValues({});
  }

  function applyAcceptedProposals() {
    if (!reviewRows) return;
    setForm((f) => {
      const next = { ...f };
      for (const row of reviewRows) {
        if (acceptedKeys.has(row.key)) {
          (next as Record<RoleFormKey, string | boolean>)[row.key] = editedValues[row.key] ?? row.applyValue;
        }
      }
      return next;
    });
    closeReview();
  }

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const body: SaveJobRoleRequest = {
      departmentId: form.departmentId,
      title: form.title,
      levelLabel: form.levelLabel || null,
      purpose: form.purpose || null,
      summary: form.summary || null,
      responsibilities: linesToArray(form.responsibilities),
      dailyActivities: linesToArray(form.dailyActivities),
      requiredExperience: form.requiredExperience || null,
      requiredEducation: form.requiredEducation || null,
      technicalSkills: linesToArray(form.technicalSkills),
      requiredCertifications: linesToArray(form.requiredCertifications),
      requiredLanguages: linesToArray(form.requiredLanguages),
      softSkills: linesToArray(form.softSkills),
      departmentProcesses: linesToArray(form.departmentProcesses),
      hrProcesses: linesToArray(form.hrProcesses),
      tools: linesToArray(form.tools),
      reportsToJobRoleId: form.reportsToJobRoleId || null,
      performanceIndicators: linesToArray(form.performanceIndicators),
      workSchedule: form.workSchedule || null,
      workModality: form.workModality || null,
      requiresTravel: form.requiresTravel,
      workLocation: form.workLocation || null,
      availabilityNotes: form.availabilityNotes || null,
      employmentType: form.employmentType || null,
      salaryRangeMin: form.salaryRangeMin ? Number(form.salaryRangeMin) : null,
      salaryRangeMax: form.salaryRangeMax ? Number(form.salaryRangeMax) : null,
      salaryCurrency: form.salaryCurrency || null,
      benefits: linesToArray(form.benefits),
      expectedOutcomes: linesToArray(form.expectedOutcomes),
    };

    if (isEditing) {
      updateMutation.mutate(body);
    } else {
      createMutation.mutate(body);
    }
  }

  if (isEditing && roleQuery.isLoading) {
    return <p className="mx-auto max-w-3xl p-6 text-sm text-slate-400">Cargando…</p>;
  }

  const canGenerate = Boolean(form.title.trim()) && Boolean(form.departmentId);

  return (
    <div className="mx-auto max-w-3xl space-y-6 pb-16">
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => navigate('/roles')}
            className="flex h-8 w-8 items-center justify-center rounded-full text-slate-400 hover:bg-slate-100 dark:hover:bg-white/10"
          >
            <ArrowLeft className="h-4 w-4" />
          </button>
          <div>
            <h1 className="flex items-center gap-2 text-2xl font-semibold text-slate-900 dark:text-white">
              <Briefcase className="h-5 w-5 text-amber-500" />
              {isEditing ? 'Editar puesto' : 'Nuevo puesto'}
            </h1>
            <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
              Descripción de trabajo completa: título, responsabilidades, requisitos, condiciones y más.
            </p>
          </div>
        </div>
        <button
          type="button"
          onClick={() => generateMutation.mutate()}
          disabled={!canGenerate || generateMutation.isPending}
          title={canGenerate ? undefined : 'Indica al menos el departamento y el título del puesto'}
          className="flex shrink-0 items-center gap-2 rounded-full border border-amber-300 bg-amber-50 px-4 py-2 text-sm font-medium text-amber-700 transition hover:bg-amber-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-300 dark:hover:bg-amber-500/20"
        >
          <Sparkles className="h-4 w-4" />
          {generateMutation.isPending ? 'Generando…' : 'Generar con IA'}
        </button>
      </div>

      {mutationError && (
        <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
          {mutationError instanceof Error ? mutationError.message : 'Ocurrió un error inesperado.'}
        </div>
      )}

      {generateMutation.error && (
        <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
          {generateMutation.error instanceof Error ? generateMutation.error.message : 'No se pudo generar la propuesta.'}
        </div>
      )}

      {reviewRows && (
        <div className="space-y-4 rounded-2xl border border-amber-200 bg-amber-50/60 p-5 dark:border-amber-500/20 dark:bg-amber-500/10">
          <div className="flex items-start justify-between gap-3">
            <div>
              <h2 className="flex items-center gap-2 text-sm font-semibold text-amber-900 dark:text-amber-200">
                <Sparkles className="h-4 w-4" />
                Propuesta generada por IA
              </h2>
              <p className="mt-1 text-xs text-amber-800/70 dark:text-amber-200/70">
                Revisa cada campo: edita el texto propuesto si lo necesitas, y acepta o descarta los cambios
                individualmente antes de aplicarlos al formulario.
              </p>
            </div>
            <button
              type="button"
              onClick={closeReview}
              className="shrink-0 text-xs font-medium text-amber-800/80 hover:underline dark:text-amber-200/80"
            >
              Cerrar
            </button>
          </div>

          {reviewRows.length === 0 ? (
            <p className="text-xs text-amber-800/70 dark:text-amber-200/70">
              La IA no encontró cambios que proponer sobre el contenido actual.
            </p>
          ) : (
            <>
              <div className="space-y-3">
                {reviewRows.map((row) => {
                  const accepted = acceptedKeys.has(row.key);
                  const currentValue = editedValues[row.key] ?? row.applyValue;
                  const isDirty = currentValue !== row.applyValue;
                  return (
                    <div
                      key={row.key}
                      className={`flex items-start gap-3 rounded-xl border border-amber-200/70 bg-white p-3 transition dark:border-amber-500/20 dark:bg-white/5 ${accepted ? '' : 'opacity-60'}`}
                    >
                      <input
                        type="checkbox"
                        checked={accepted}
                        onChange={() => toggleAccepted(row.key)}
                        className="mt-1 h-4 w-4 shrink-0 rounded border-slate-300"
                      />
                      <div className="flex-1 space-y-1.5">
                        <div className="flex items-center justify-between gap-2">
                          <p className="text-xs font-semibold text-slate-700 dark:text-white/70">{row.label}</p>
                          {isDirty && (
                            <button
                              type="button"
                              onClick={() => updateEditedValue(row.key, row.applyValue)}
                              className="shrink-0 text-[11px] font-medium text-amber-700 hover:underline dark:text-amber-300"
                            >
                              Restaurar sugerencia
                            </button>
                          )}
                        </div>
                        <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                          <div className="whitespace-pre-wrap rounded-lg bg-red-50 px-2.5 py-1.5 text-xs text-red-700 line-through decoration-red-300 dark:bg-red-500/10 dark:text-red-300">
                            {row.before}
                          </div>
                          {row.key === 'requiresTravel' ? (
                            <select
                              value={String(currentValue)}
                              onChange={(e) => updateEditedValue(row.key, e.target.value === 'true')}
                              className="rounded-lg border border-green-200 bg-green-50 px-2.5 py-1.5 text-xs text-green-800 outline-none focus:border-green-400 dark:border-green-500/20 dark:bg-green-500/10 dark:text-green-300"
                            >
                              <option value="true">Sí</option>
                              <option value="false">No</option>
                            </select>
                          ) : (
                            <textarea
                              value={String(currentValue)}
                              onChange={(e) => updateEditedValue(row.key, e.target.value)}
                              rows={Math.min(6, Math.max(2, String(currentValue).split('\n').length))}
                              className="w-full resize-y rounded-lg border border-green-200 bg-green-50 px-2.5 py-1.5 text-xs text-green-800 outline-none focus:border-green-400 dark:border-green-500/20 dark:bg-green-500/10 dark:text-green-300"
                            />
                          )}
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>

              <div className="flex justify-end gap-2 pt-1">
                <button
                  type="button"
                  onClick={closeReview}
                  className="rounded-full px-4 py-2 text-xs font-medium text-slate-500 hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/10"
                >
                  Descartar todo
                </button>
                <button
                  type="button"
                  onClick={applyAcceptedProposals}
                  disabled={acceptedKeys.size === 0}
                  className="rounded-full bg-amber-500 px-4 py-2 text-xs font-semibold text-white hover:bg-amber-600 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  Aplicar seleccionados ({acceptedKeys.size})
                </button>
              </div>
            </>
          )}
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        <Section number={1} title="Título del puesto">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass}>Departamento</label>
              <select
                required
                value={form.departmentId}
                onChange={(e) => setForm((f) => ({ ...f, departmentId: e.target.value }))}
                className={inputClass}
              >
                <option value="" disabled>
                  Selecciona…
                </option>
                {departmentsQuery.data?.map((d) => (
                  <option key={d.departmentId} value={d.departmentId}>
                    {d.name}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className={labelClass}>Título del puesto</label>
              <input
                required
                value={form.title}
                onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
                placeholder="p. ej. Ejecutivo de Ventas"
                className={inputClass}
              />
            </div>
          </div>
          <div className="mt-4">
            <label className={labelClass}>Nivel / Seniority (opcional)</label>
            <input
              value={form.levelLabel}
              onChange={(e) => setForm((f) => ({ ...f, levelLabel: e.target.value }))}
              placeholder="Junior, Senior, Lead…"
              className={inputClass}
            />
          </div>
        </Section>

        <Section number={2} title="Resumen del puesto" description="Una breve descripción general del rol y su propósito.">
          <label className={labelClass}>Resumen</label>
          <textarea
            value={form.summary}
            onChange={(e) => setForm((f) => ({ ...f, summary: e.target.value }))}
            rows={3}
            className={inputClass}
          />
          <label className={`${labelClass} mt-4`}>Propósito (opcional)</label>
          <textarea
            value={form.purpose}
            onChange={(e) => setForm((f) => ({ ...f, purpose: e.target.value }))}
            rows={2}
            className={inputClass}
          />
        </Section>

        <Section number={3} title="Responsabilidades principales" description="Una responsabilidad por línea.">
          <textarea
            value={form.responsibilities}
            onChange={(e) => setForm((f) => ({ ...f, responsibilities: e.target.value }))}
            rows={4}
            placeholder={'Gestionar la cartera de clientes\nCumplir metas mensuales de ventas'}
            className={inputClass}
          />
        </Section>

        <Section number={4} title="Actividades diarias o periódicas" description="Una actividad por línea.">
          <textarea
            value={form.dailyActivities}
            onChange={(e) => setForm((f) => ({ ...f, dailyActivities: e.target.value }))}
            rows={4}
            placeholder={'Revisar correos y pendientes\nReunión semanal de equipo'}
            className={inputClass}
          />
        </Section>

        <Section
          number={5}
          title="Requisitos del puesto"
          description="Escolaridad, experiencia, conocimientos técnicos, certificaciones e idiomas."
        >
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass}>Escolaridad</label>
              <input
                value={form.requiredEducation}
                onChange={(e) => setForm((f) => ({ ...f, requiredEducation: e.target.value }))}
                placeholder="p. ej. Licenciatura en Administración"
                className={inputClass}
              />
            </div>
            <div>
              <label className={labelClass}>Experiencia requerida</label>
              <input
                value={form.requiredExperience}
                onChange={(e) => setForm((f) => ({ ...f, requiredExperience: e.target.value }))}
                placeholder="p. ej. 3+ años en ventas B2B"
                className={inputClass}
              />
            </div>
          </div>
          <div className="mt-4">
            <label className={labelClass}>Conocimientos técnicos (uno por línea)</label>
            <textarea
              value={form.technicalSkills}
              onChange={(e) => setForm((f) => ({ ...f, technicalSkills: e.target.value }))}
              rows={3}
              className={inputClass}
            />
          </div>
          <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass}>Certificaciones (una por línea)</label>
              <textarea
                value={form.requiredCertifications}
                onChange={(e) => setForm((f) => ({ ...f, requiredCertifications: e.target.value }))}
                rows={3}
                className={inputClass}
              />
            </div>
            <div>
              <label className={labelClass}>Idiomas (uno por línea)</label>
              <textarea
                value={form.requiredLanguages}
                onChange={(e) => setForm((f) => ({ ...f, requiredLanguages: e.target.value }))}
                rows={3}
                placeholder={'Inglés avanzado\nEspañol nativo'}
                className={inputClass}
              />
            </div>
          </div>
        </Section>

        <Section number={6} title="Habilidades y competencias" description="Habilidades blandas, una por línea.">
          <textarea
            value={form.softSkills}
            onChange={(e) => setForm((f) => ({ ...f, softSkills: e.target.value }))}
            rows={3}
            placeholder={'Comunicación efectiva\nTrabajo en equipo'}
            className={inputClass}
          />
        </Section>

        <Section number={7} title="Herramientas o tecnologías" description="Una herramienta por línea.">
          <textarea
            value={form.tools}
            onChange={(e) => setForm((f) => ({ ...f, tools: e.target.value }))}
            rows={3}
            placeholder={'Excel avanzado\nSalesforce'}
            className={inputClass}
          />
        </Section>

        <Section
          number={8}
          title="Relación jerárquica"
          description="A quién reporta este puesto y si tiene personal a cargo."
        >
          <label className={labelClass}>Reporta a</label>
          <select
            value={form.reportsToJobRoleId}
            onChange={(e) => setForm((f) => ({ ...f, reportsToJobRoleId: e.target.value }))}
            className={inputClass}
          >
            <option value="">Sin jefe directo / N/A</option>
            {otherJobRoles.map((r) => (
              <option key={r.jobRoleId} value={r.jobRoleId}>
                {r.title}
              </option>
            ))}
          </select>
          {isEditing && roleQuery.data && (
            <p className="mt-2 text-xs text-slate-400 dark:text-white/40">
              {roleQuery.data.hasDirectReports
                ? 'Este puesto tiene personal a cargo (otros puestos reportan a él).'
                : 'Este puesto no tiene personal a cargo actualmente.'}
            </p>
          )}
        </Section>

        <Section number={9} title="Indicadores de desempeño" description="Un indicador (KPI) por línea.">
          <textarea
            value={form.performanceIndicators}
            onChange={(e) => setForm((f) => ({ ...f, performanceIndicators: e.target.value }))}
            rows={3}
            placeholder={'Cumplimiento de meta mensual de ventas\nNPS de clientes atendidos'}
            className={inputClass}
          />
        </Section>

        <Section
          number={10}
          title="Condiciones de trabajo"
          description="Horario, modalidad, viajes, ubicación y disponibilidad."
        >
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass}>Horario</label>
              <input
                value={form.workSchedule}
                onChange={(e) => setForm((f) => ({ ...f, workSchedule: e.target.value }))}
                placeholder="Lunes a viernes, 9:00–18:00"
                className={inputClass}
              />
            </div>
            <div>
              <label className={labelClass}>Modalidad</label>
              <input
                value={form.workModality}
                onChange={(e) => setForm((f) => ({ ...f, workModality: e.target.value }))}
                placeholder="Presencial, remoto, híbrido…"
                className={inputClass}
              />
            </div>
          </div>
          <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass}>Ubicación</label>
              <input
                value={form.workLocation}
                onChange={(e) => setForm((f) => ({ ...f, workLocation: e.target.value }))}
                placeholder="Oficina central, CDMX"
                className={inputClass}
              />
            </div>
            <div className="flex items-center gap-2 pt-6">
              <input
                id="requiresTravel"
                type="checkbox"
                checked={form.requiresTravel}
                onChange={(e) => setForm((f) => ({ ...f, requiresTravel: e.target.checked }))}
                className="h-4 w-4 rounded border-slate-300"
              />
              <label htmlFor="requiresTravel" className="text-sm text-slate-600 dark:text-white/60">
                Requiere viajes
              </label>
            </div>
          </div>
          <div className="mt-4">
            <label className={labelClass}>Disponibilidad (opcional)</label>
            <input
              value={form.availabilityNotes}
              onChange={(e) => setForm((f) => ({ ...f, availabilityNotes: e.target.value }))}
              placeholder="Disponibilidad para guardias los fines de semana"
              className={inputClass}
            />
          </div>
        </Section>

        <Section number={11} title="Tipo de contratación">
          <input
            value={form.employmentType}
            onChange={(e) => setForm((f) => ({ ...f, employmentType: e.target.value }))}
            placeholder="Tiempo completo, medio tiempo, honorarios…"
            className={inputClass}
          />
        </Section>

        <Section number={12} title="Rango salarial y prestaciones">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <div>
              <label className={labelClass}>Salario mínimo</label>
              <input
                type="number"
                value={form.salaryRangeMin}
                onChange={(e) => setForm((f) => ({ ...f, salaryRangeMin: e.target.value }))}
                className={inputClass}
              />
            </div>
            <div>
              <label className={labelClass}>Salario máximo</label>
              <input
                type="number"
                value={form.salaryRangeMax}
                onChange={(e) => setForm((f) => ({ ...f, salaryRangeMax: e.target.value }))}
                className={inputClass}
              />
            </div>
            <div>
              <label className={labelClass}>Moneda</label>
              <input
                value={form.salaryCurrency}
                onChange={(e) => setForm((f) => ({ ...f, salaryCurrency: e.target.value }))}
                placeholder="MXN, USD…"
                className={inputClass}
              />
            </div>
          </div>
          <div className="mt-4">
            <label className={labelClass}>Prestaciones (una por línea)</label>
            <textarea
              value={form.benefits}
              onChange={(e) => setForm((f) => ({ ...f, benefits: e.target.value }))}
              rows={3}
              placeholder={'Seguro de gastos médicos mayores\nVales de despensa'}
              className={inputClass}
            />
          </div>
        </Section>

        <Section number={13} title="Objetivos del puesto" description="Resultados esperados, uno por línea.">
          <textarea
            value={form.expectedOutcomes}
            onChange={(e) => setForm((f) => ({ ...f, expectedOutcomes: e.target.value }))}
            rows={3}
            placeholder={'Incrementar la cartera de clientes activos en 20%\nReducir tiempo de respuesta a clientes'}
            className={inputClass}
          />
        </Section>

        <Section
          number={14}
          title="Procesos de empresa (Rutas de Carrera)"
          description="Requisitos de capacitación específicos de la empresa para este puesto — alimentan las categorías 'Procesos de Depto.' y 'Procesos de RRHH' en el plan de carrera del colaborador."
        >
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass}>Procesos de departamento (uno por línea)</label>
              <textarea
                value={form.departmentProcesses}
                onChange={(e) => setForm((f) => ({ ...f, departmentProcesses: e.target.value }))}
                rows={3}
                placeholder={'Proceso de armado de reportes de ventas\nProceso de cierre mensual'}
                className={inputClass}
              />
            </div>
            <div>
              <label className={labelClass}>Procesos de RRHH / cumplimiento (uno por línea)</label>
              <textarea
                value={form.hrProcesses}
                onChange={(e) => setForm((f) => ({ ...f, hrProcesses: e.target.value }))}
                rows={3}
                placeholder={'Prevención de Acoso Sexual Laboral\nCódigo de Ética y Conducta'}
                className={inputClass}
              />
            </div>
          </div>
        </Section>

        <div className="flex justify-end gap-2 pt-2">
          <button
            type="button"
            onClick={() => navigate('/roles')}
            className="rounded-full px-4 py-2 text-sm font-medium text-slate-500 hover:bg-slate-100 dark:text-white/50 dark:hover:bg-white/10"
          >
            Cancelar
          </button>
          <button
            type="submit"
            disabled={isSaving}
            className="rounded-full bg-brand-600 px-6 py-2 text-sm font-medium text-white transition hover:bg-brand-700 disabled:opacity-60"
          >
            {isSaving ? 'Guardando…' : 'Guardar puesto'}
          </button>
        </div>
      </form>
    </div>
  );
}
