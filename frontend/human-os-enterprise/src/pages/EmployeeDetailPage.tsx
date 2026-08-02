import { useEffect, useMemo, useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useParams } from 'react-router';
import { ArrowLeft, IdCard, Paperclip, Sparkles } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { extractPdfText } from '@/lib/pdfText';
import {
  extractPersonResume,
  getDepartments,
  getJobRoles,
  getPersonResume,
  getTenantPeople,
  savePersonResume,
  updatePersonOrgAssignment,
  type ExtractPersonResumeResponse,
  type PersonOrgAssignmentRequest,
  type SavePersonResumeRequest,
} from '@/api/orgStructureApi';

const ORG_ROLE_LABELS: Record<string, string> = {
  Employee: 'Empleado',
  Manager: 'Gerente',
  HRAdmin: 'Administrador RRHH',
};

function linesToArray(value: string): string[] {
  return value
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean);
}

function arrayToLines(value: string[] | undefined): string {
  return (value ?? []).join('\n');
}

const emptyForm = {
  address: '',
  linkedInUrl: '',
  professionalObjective: '',
  professionalSummary: '',
  skills: '',
  workExperience: '',
  education: '',
  certifications: '',
  languages: '',
  rawResumeText: '',
};

type ResumeFormState = typeof emptyForm;
type ResumeFormKey = keyof ResumeFormState;

/// One row of the "Extraer con IA" before/after review panel — same pattern as
/// RoleDetailPage's DescriptionDiffRow: `applyValue` is already shaped exactly like the
/// form field it targets (a joined multi-line string) so applying an accepted row is a
/// single assignment, never a re-parse.
interface ResumeDiffRow {
  key: ResumeFormKey;
  label: string;
  before: string;
  after: string;
  applyValue: string;
}

/// Compares the AI proposal against the CURRENT form snapshot (taken at the moment the
/// proposal arrived) and returns only the fields that genuinely changed, for the
/// before/after review UI.
function buildResumeDiffRows(
  formSnapshot: ResumeFormState,
  proposal: ExtractPersonResumeResponse,
): ResumeDiffRow[] {
  const rows: ResumeDiffRow[] = [];

  function addText(key: ResumeFormKey, label: string, current: string, proposed: string | null | undefined) {
    const proposedTrimmed = (proposed ?? '').trim();
    if (!proposedTrimmed || proposedTrimmed === current.trim()) return;
    rows.push({ key, label, before: current.trim() || '(vacío)', after: proposedTrimmed, applyValue: proposedTrimmed });
  }

  function addList(key: ResumeFormKey, label: string, current: string, proposed: string[]) {
    const proposedLines = arrayToLines(proposed).trim();
    if (!proposedLines || proposedLines === current.trim()) return;
    rows.push({ key, label, before: current.trim() || '(vacío)', after: proposedLines, applyValue: proposedLines });
  }

  addText('address', 'Dirección', formSnapshot.address, proposal.address);
  addText('linkedInUrl', 'LinkedIn', formSnapshot.linkedInUrl, proposal.linkedInUrl);
  addText('professionalObjective', 'Objetivo profesional', formSnapshot.professionalObjective, proposal.professionalObjective);
  addText('professionalSummary', 'Resumen profesional', formSnapshot.professionalSummary, proposal.professionalSummary);
  addList('skills', 'Habilidades', formSnapshot.skills, proposal.skills);
  addList('workExperience', 'Experiencia laboral', formSnapshot.workExperience, proposal.workExperience);
  addList('education', 'Educación', formSnapshot.education, proposal.education);
  addList('certifications', 'Certificaciones', formSnapshot.certifications, proposal.certifications);
  addList('languages', 'Idiomas', formSnapshot.languages, proposal.languages);

  return rows;
}

const inputClass =
  'mt-1 w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white';
const labelClass = 'block text-xs font-medium text-slate-500 dark:text-white/50';

interface OrgAssignmentFormState {
  departmentId: string;
  jobRoleId: string;
  managerId: string;
  orgRole: string;
}

export function EmployeeDetailPage() {
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const params = useParams<{ personId: string }>();
  const personId = params.personId!;
  const pdfInputRef = useRef<HTMLInputElement>(null);
  const [pdfError, setPdfError] = useState<string | null>(null);
  const [isReadingPdf, setIsReadingPdf] = useState(false);

  const [form, setForm] = useState(emptyForm);
  const [reviewRows, setReviewRows] = useState<ResumeDiffRow[] | null>(null);
  const [acceptedKeys, setAcceptedKeys] = useState<Set<ResumeFormKey>>(new Set());
  // Lets the HRAdmin tweak the AI's "after" text per field before applying it — starts as a
  // copy of each row's applyValue and only diverges once the user edits that field's box.
  const [editedValues, setEditedValues] = useState<Partial<Record<ResumeFormKey, string>>>({});
  const [orgForm, setOrgForm] = useState<OrgAssignmentFormState | null>(null);

  const peopleQuery = useQuery({
    queryKey: ['tenant-people', tenantId],
    queryFn: () => getTenantPeople(tenantId),
  });

  const person = useMemo(
    () => peopleQuery.data?.find((p) => p.personId === personId),
    [peopleQuery.data, personId],
  );

  const departmentsQuery = useQuery({
    queryKey: ['departments', tenantId],
    queryFn: () => getDepartments(tenantId),
  });
  const jobRolesQuery = useQuery({
    queryKey: ['job-roles', tenantId],
    queryFn: () => getJobRoles(tenantId),
  });

  // Seeds the org-assignment form from the person record exactly once — after that, the
  // form is fully user-controlled so an in-flight edit never gets clobbered by a refetch
  // (e.g. the invalidation triggered by orgMutation's own onSuccess).
  useEffect(() => {
    if (person && !orgForm) {
      setOrgForm({
        departmentId: person.departmentId ?? '',
        jobRoleId: person.jobRoleId ?? '',
        managerId: person.managerId ?? '',
        orgRole: person.orgRole,
      });
    }
  }, [person, orgForm]);

  const orgMutation = useMutation({
    mutationFn: (body: PersonOrgAssignmentRequest) => updatePersonOrgAssignment(personId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['tenant-people', tenantId] });
    },
  });

  function handleOrgSubmit(e: FormEvent) {
    e.preventDefault();
    if (!orgForm) return;
    orgMutation.mutate({
      departmentId: orgForm.departmentId || null,
      jobRoleId: orgForm.jobRoleId || null,
      managerId: orgForm.managerId || null,
      orgRole: orgForm.orgRole || null,
    });
  }

  function jobRolesForDepartment(departmentId: string) {
    const roles = jobRolesQuery.data ?? [];
    return departmentId ? roles.filter((r) => r.departmentId === departmentId) : roles;
  }

  const resumeQuery = useQuery({
    queryKey: ['person-resume', personId],
    queryFn: () => getPersonResume(personId),
  });

  useEffect(() => {
    if (resumeQuery.data) {
      const r = resumeQuery.data;
      setForm({
        address: r.address ?? '',
        linkedInUrl: r.linkedInUrl ?? '',
        professionalObjective: r.professionalObjective ?? '',
        professionalSummary: r.professionalSummary ?? '',
        skills: arrayToLines(r.skills),
        workExperience: arrayToLines(r.workExperience),
        education: arrayToLines(r.education),
        certifications: arrayToLines(r.certifications),
        languages: arrayToLines(r.languages),
        rawResumeText: r.rawResumeText ?? '',
      });
    }
  }, [resumeQuery.data]);

  const saveMutation = useMutation({
    mutationFn: (body: SavePersonResumeRequest) => savePersonResume(personId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['person-resume', personId] });
    },
  });

  // "Extraer con IA" — ResumeExtractionAgent (Microsoft Agent Framework, backend) reads the
  // raw pasted résumé/CV text and extracts structured fields from it. Returns a pure
  // PROPOSAL — it is never written into `form` directly; buildResumeDiffRows() computes a
  // before/after diff against a snapshot of the form taken right now, and the review panel
  // below lets the HRAdmin accept/reject each field individually before anything changes.
  const extractMutation = useMutation({
    mutationFn: () => extractPersonResume(personId, form.rawResumeText),
    onSuccess: (data) => {
      const rows = buildResumeDiffRows(form, data);
      setReviewRows(rows);
      setAcceptedKeys(new Set(rows.map((row) => row.key)));
      setEditedValues(Object.fromEntries(rows.map((row) => [row.key, row.applyValue])));
    },
  });

  function toggleAccepted(key: ResumeFormKey) {
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

  function updateEditedValue(key: ResumeFormKey, value: string) {
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
          next[row.key] = editedValues[row.key] ?? row.applyValue;
        }
      }
      return next;
    });
    closeReview();
  }

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const body: SavePersonResumeRequest = {
      address: form.address || null,
      linkedInUrl: form.linkedInUrl || null,
      professionalObjective: form.professionalObjective || null,
      professionalSummary: form.professionalSummary || null,
      skills: linesToArray(form.skills),
      workExperience: linesToArray(form.workExperience),
      education: linesToArray(form.education),
      certifications: linesToArray(form.certifications),
      languages: linesToArray(form.languages),
      rawResumeText: form.rawResumeText || null,
    };
    saveMutation.mutate(body);
  }

  const canExtract = Boolean(form.rawResumeText.trim());

  // "Subir PDF" — reads the file entirely client-side via pdfjs-dist (no backend upload,
  // no Data Lake storage dependency) and drops the extracted text straight into the same
  // textarea "Pegar currículum" already uses, so the rest of the flow (Extraer con IA →
  // review → apply → guardar) is identical whether the text was pasted or came from a PDF.
  async function handlePdfSelected(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;

    setPdfError(null);
    setIsReadingPdf(true);
    try {
      const text = await extractPdfText(file);
      if (!text) {
        setPdfError(`No se pudo extraer texto de "${file.name}" — ¿es un PDF escaneado (solo imágenes)?`);
        return;
      }
      setForm((f) => ({ ...f, rawResumeText: text }));
    } catch {
      setPdfError(`No se pudo leer "${file.name}". Verifica que sea un PDF válido.`);
    } finally {
      setIsReadingPdf(false);
    }
  }

  if (resumeQuery.isLoading) {
    return <p className="mx-auto max-w-3xl p-6 text-sm text-slate-400">Cargando…</p>;
  }

  return (
    <div className="mx-auto max-w-3xl space-y-6 pb-16">
      <div className="flex items-center gap-3">
        <button
          type="button"
          onClick={() => navigate('/personal')}
          className="flex h-8 w-8 items-center justify-center rounded-full text-slate-400 hover:bg-slate-100 dark:hover:bg-white/10"
        >
          <ArrowLeft className="h-4 w-4" />
        </button>
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-semibold text-slate-900 dark:text-white">
            <IdCard className="h-5 w-5 text-sky-500" />
            {person?.displayName || person?.email || 'Ficha del empleado'}
          </h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
            {person
              ? `${person.jobRoleTitle ?? 'Sin puesto asignado'} · ${person.departmentName ?? 'Sin departamento'}`
              : 'Currículum, contacto y perfil profesional completos.'}
          </p>
        </div>
      </div>

      {saveMutation.error && (
        <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
          {saveMutation.error instanceof Error ? saveMutation.error.message : 'Ocurrió un error inesperado.'}
        </div>
      )}
      {saveMutation.isSuccess && !saveMutation.isPending && (
        <div className="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-700 dark:border-emerald-500/20 dark:bg-emerald-500/10 dark:text-emerald-300">
          Currículum guardado.
        </div>
      )}

      {orgMutation.error && (
        <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
          {orgMutation.error instanceof Error ? orgMutation.error.message : 'Ocurrió un error inesperado.'}
        </div>
      )}
      {orgMutation.isSuccess && !orgMutation.isPending && (
        <div className="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-700 dark:border-emerald-500/20 dark:bg-emerald-500/10 dark:text-emerald-300">
          Asignación organizacional guardada.
        </div>
      )}

      {orgForm && (
        <form
          onSubmit={handleOrgSubmit}
          className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5"
        >
          <h2 className="text-sm font-semibold text-slate-900 dark:text-white">Asignación organizacional</h2>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass}>Departamento</label>
              <select
                value={orgForm.departmentId}
                onChange={(e) =>
                  setOrgForm((f) => (f ? { ...f, departmentId: e.target.value, jobRoleId: '' } : f))
                }
                className={inputClass}
              >
                <option value="">Sin asignar</option>
                {departmentsQuery.data?.map((d) => (
                  <option key={d.departmentId} value={d.departmentId}>
                    {d.name}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className={labelClass}>Puesto</label>
              <select
                value={orgForm.jobRoleId}
                onChange={(e) => setOrgForm((f) => (f ? { ...f, jobRoleId: e.target.value } : f))}
                className={inputClass}
              >
                <option value="">Sin asignar</option>
                {jobRolesForDepartment(orgForm.departmentId).map((r) => (
                  <option key={r.jobRoleId} value={r.jobRoleId}>
                    {r.title}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className={labelClass}>Gerente</label>
              <select
                value={orgForm.managerId}
                onChange={(e) => setOrgForm((f) => (f ? { ...f, managerId: e.target.value } : f))}
                className={inputClass}
              >
                <option value="">Sin asignar</option>
                {(peopleQuery.data ?? [])
                  .filter((p) => p.personId !== personId && p.isActive)
                  .map((p) => (
                    <option key={p.personId} value={p.personId}>
                      {p.displayName || p.email}
                    </option>
                  ))}
              </select>
            </div>
            <div>
              <label className={labelClass}>Nivel de permisos</label>
              <select
                value={orgForm.orgRole}
                onChange={(e) => setOrgForm((f) => (f ? { ...f, orgRole: e.target.value } : f))}
                className={inputClass}
              >
                {Object.entries(ORG_ROLE_LABELS).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <div className="flex justify-end">
            <button
              type="submit"
              disabled={orgMutation.isPending}
              className="rounded-full bg-gradient-to-r from-sky-600 to-sky-500 px-5 py-2.5 text-sm font-semibold text-white shadow-md shadow-sky-500/30 transition hover:brightness-105 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {orgMutation.isPending ? 'Guardando…' : 'Guardar asignación'}
            </button>
          </div>
        </form>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5">
          <div className="flex items-start justify-between gap-3">
            <div>
              <h2 className="text-sm font-semibold text-slate-900 dark:text-white">Pegar currículum (CV)</h2>
              <p className="mt-1 text-xs text-slate-400 dark:text-white/40">
                Pega el texto completo del currículum, o sube el archivo PDF directamente, y usa "Extraer con
                IA" para llenar automáticamente los campos de abajo — siempre puedes revisar y editar antes de
                guardar.
              </p>
            </div>
            <div className="flex shrink-0 items-center gap-2">
              <input
                ref={pdfInputRef}
                type="file"
                accept="application/pdf"
                onChange={handlePdfSelected}
                className="hidden"
              />
              <button
                type="button"
                onClick={() => pdfInputRef.current?.click()}
                disabled={isReadingPdf}
                className="flex items-center gap-2 rounded-full border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-600 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50 dark:border-white/10 dark:bg-white/5 dark:text-white/70 dark:hover:bg-white/10"
              >
                <Paperclip className="h-4 w-4" />
                {isReadingPdf ? 'Leyendo PDF…' : 'Subir PDF'}
              </button>
              <button
                type="button"
                onClick={() => extractMutation.mutate()}
                disabled={!canExtract || extractMutation.isPending}
                title={canExtract ? undefined : 'Pega o sube primero el currículum'}
                className="flex items-center gap-2 rounded-full border border-amber-300 bg-amber-50 px-4 py-2 text-sm font-medium text-amber-700 transition hover:bg-amber-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-300 dark:hover:bg-amber-500/20"
              >
                <Sparkles className="h-4 w-4" />
                {extractMutation.isPending ? 'Extrayendo…' : 'Extraer con IA'}
              </button>
            </div>
          </div>
          {pdfError && <p className="text-xs text-red-600 dark:text-red-400">{pdfError}</p>}
          <textarea
            value={form.rawResumeText}
            onChange={(e) => setForm((f) => ({ ...f, rawResumeText: e.target.value }))}
            rows={10}
            placeholder="Pega aquí el texto completo del currículum, o sube un PDF con el botón de arriba…"
            className={inputClass}
          />
        </section>

        {extractMutation.error && (
          <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
            {extractMutation.error instanceof Error ? extractMutation.error.message : 'No se pudo extraer el currículum.'}
          </div>
        )}

        {reviewRows && (
          <div className="space-y-4 rounded-2xl border border-amber-200 bg-amber-50/60 p-5 dark:border-amber-500/20 dark:bg-amber-500/10">
            <div className="flex items-start justify-between gap-3">
              <div>
                <h2 className="flex items-center gap-2 text-sm font-semibold text-amber-900 dark:text-amber-200">
                  <Sparkles className="h-4 w-4" />
                  Propuesta extraída por IA
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
                La IA no encontró información nueva que proponer sobre el contenido actual.
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
                            <textarea
                              value={currentValue}
                              onChange={(e) => updateEditedValue(row.key, e.target.value)}
                              rows={Math.min(6, Math.max(2, currentValue.split('\n').length))}
                              className="w-full resize-y rounded-lg border border-green-200 bg-green-50 px-2.5 py-1.5 text-xs text-green-800 outline-none focus:border-green-400 dark:border-green-500/20 dark:bg-green-500/10 dark:text-green-300"
                            />
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

        <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5">
          <h2 className="text-sm font-semibold text-slate-900 dark:text-white">Contacto</h2>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass}>Dirección</label>
              <input
                value={form.address}
                onChange={(e) => setForm((f) => ({ ...f, address: e.target.value }))}
                className={inputClass}
              />
            </div>
            <div>
              <label className={labelClass}>LinkedIn</label>
              <input
                value={form.linkedInUrl}
                onChange={(e) => setForm((f) => ({ ...f, linkedInUrl: e.target.value }))}
                placeholder="https://www.linkedin.com/in/…"
                className={inputClass}
              />
            </div>
          </div>
        </section>

        <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5">
          <h2 className="text-sm font-semibold text-slate-900 dark:text-white">Perfil profesional</h2>
          <div>
            <label className={labelClass}>Objetivo profesional</label>
            <textarea
              value={form.professionalObjective}
              onChange={(e) => setForm((f) => ({ ...f, professionalObjective: e.target.value }))}
              rows={2}
              className={inputClass}
            />
          </div>
          <div>
            <label className={labelClass}>Resumen profesional</label>
            <textarea
              value={form.professionalSummary}
              onChange={(e) => setForm((f) => ({ ...f, professionalSummary: e.target.value }))}
              rows={3}
              className={inputClass}
            />
          </div>
          <div>
            <label className={labelClass}>Habilidades (una por línea)</label>
            <textarea
              value={form.skills}
              onChange={(e) => setForm((f) => ({ ...f, skills: e.target.value }))}
              rows={4}
              className={inputClass}
            />
          </div>
        </section>

        <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5">
          <h2 className="text-sm font-semibold text-slate-900 dark:text-white">Trayectoria</h2>
          <div>
            <label className={labelClass}>Experiencia laboral (una por línea)</label>
            <textarea
              value={form.workExperience}
              onChange={(e) => setForm((f) => ({ ...f, workExperience: e.target.value }))}
              rows={5}
              className={inputClass}
            />
          </div>
          <div>
            <label className={labelClass}>Educación (una por línea)</label>
            <textarea
              value={form.education}
              onChange={(e) => setForm((f) => ({ ...f, education: e.target.value }))}
              rows={3}
              className={inputClass}
            />
          </div>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass}>Certificaciones (una por línea)</label>
              <textarea
                value={form.certifications}
                onChange={(e) => setForm((f) => ({ ...f, certifications: e.target.value }))}
                rows={3}
                className={inputClass}
              />
            </div>
            <div>
              <label className={labelClass}>Idiomas (uno por línea)</label>
              <textarea
                value={form.languages}
                onChange={(e) => setForm((f) => ({ ...f, languages: e.target.value }))}
                rows={3}
                className={inputClass}
              />
            </div>
          </div>
        </section>

        <div className="flex justify-end gap-2">
          <button
            type="submit"
            disabled={saveMutation.isPending}
            className="rounded-full bg-sky-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-sky-700 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {saveMutation.isPending ? 'Guardando…' : 'Guardar'}
          </button>
        </div>
      </form>
    </div>
  );
}
