import { useEffect, useMemo, useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useParams } from 'react-router';
import { ArrowLeft, IdCard, Paperclip, Sparkles, MapPin, Mail, Phone, User, Globe } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { extractPdfText } from '@/lib/pdfText';
import {
  extractPersonResume,
  getDepartments,
  getJobRoles,
  getPersonResume,
  getTenantPeople,
  savePersonResume,
  searchAddresses,
  updatePersonOrgAssignment,
  type AddressSuggestion,
  type ExtractPersonResumeResponse,
  type PersonOrgAssignmentRequest,
  type SavePersonResumeRequest,
  type StudentAcademicProfile,
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
  hasPreviousValue: boolean;
  after: string;
  applyValue: string;
}

/// Compares the AI proposal against the CURRENT form snapshot (taken at the moment the
/// proposal arrived) and returns only the fields that genuinely changed, for the
/// before/after review UI.
function buildResumeDiffRows(
  formSnapshot: ResumeFormState,
  proposal: ExtractPersonResumeResponse,
  t: (key: string) => string,
): ResumeDiffRow[] {
  const rows: ResumeDiffRow[] = [];

  function addText(key: ResumeFormKey, labelKey: string, current: string, proposed: string | null | undefined) {
    const proposedTrimmed = (proposed ?? '').trim();
    if (!proposedTrimmed || proposedTrimmed === current.trim()) return;
    rows.push({
      key,
      label: t(labelKey),
      before: current.trim(),
      hasPreviousValue: Boolean(current.trim()),
      after: proposedTrimmed,
      applyValue: proposedTrimmed,
    });
  }

  function addList(key: ResumeFormKey, labelKey: string, current: string, proposed: string[]) {
    const proposedLines = arrayToLines(proposed).trim();
    if (!proposedLines || proposedLines === current.trim()) return;
    rows.push({
      key,
      label: t(labelKey),
      before: current.trim(),
      hasPreviousValue: Boolean(current.trim()),
      after: proposedLines,
      applyValue: proposedLines,
    });
  }

  addText('address', 'employeeDetail.diffFieldLabels.address', formSnapshot.address, proposal.address);
  addText('linkedInUrl', 'employeeDetail.diffFieldLabels.linkedInUrl', formSnapshot.linkedInUrl, proposal.linkedInUrl);
  addText('professionalObjective', 'employeeDetail.diffFieldLabels.professionalObjective', formSnapshot.professionalObjective, proposal.professionalObjective);
  addText('professionalSummary', 'employeeDetail.diffFieldLabels.professionalSummary', formSnapshot.professionalSummary, proposal.professionalSummary);
  addList('skills', 'employeeDetail.diffFieldLabels.skills', formSnapshot.skills, proposal.skills);
  addList('workExperience', 'employeeDetail.diffFieldLabels.workExperience', formSnapshot.workExperience, proposal.workExperience);
  addList('education', 'employeeDetail.diffFieldLabels.education', formSnapshot.education, proposal.education);
  addList('certifications', 'employeeDetail.diffFieldLabels.certifications', formSnapshot.certifications, proposal.certifications);
  addList('languages', 'employeeDetail.diffFieldLabels.languages', formSnapshot.languages, proposal.languages);

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
  displayName: string;
  email: string;
  phoneNumber: string;
  address: string;
  linkedInUrl: string;
}

export function EmployeeDetailPage() {
  const { t } = useTranslation();
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
  const [academicProfile, setAcademicProfile] = useState<StudentAcademicProfile | null>(null);

  // Bing / Azure Maps address autocomplete state
  const [addressSuggestions, setAddressSuggestions] = useState<AddressSuggestion[]>([]);
  const [isSearchingAddress, setIsSearchingAddress] = useState(false);

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
        displayName: person.displayName ?? '',
        email: person.email ?? '',
        phoneNumber: person.phoneNumber ?? '',
        address: person.address ?? '',
        linkedInUrl: person.linkedInUrl ?? '',
      });
    }
  }, [person, orgForm]);

  const orgMutation = useMutation({
    mutationFn: (body: PersonOrgAssignmentRequest) => updatePersonOrgAssignment(personId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['tenant-people', tenantId] });
      void queryClient.invalidateQueries({ queryKey: ['person-resume', personId] });
    },
  });

  const addressSearchTimerRef = useRef<number | null>(null);

  function handleAddressQueryChange(query: string) {
    setOrgForm((f) => (f ? { ...f, address: query } : f));
    setForm((f) => ({ ...f, address: query }));

    if (addressSearchTimerRef.current) {
      window.clearTimeout(addressSearchTimerRef.current);
    }

    if (!query || query.trim().length < 3) {
      setAddressSuggestions([]);
      setIsSearchingAddress(false);
      return;
    }

    setIsSearchingAddress(true);
    addressSearchTimerRef.current = window.setTimeout(() => {
      void searchAddresses(query)
        .then((suggs) => {
          setAddressSuggestions(suggs);
        })
        .finally(() => setIsSearchingAddress(false));
    }, 300);
  }

  function selectAddressSuggestion(sugg: AddressSuggestion) {
    const val = sugg.address;
    setOrgForm((f) => (f ? { ...f, address: val } : f));
    setForm((f) => ({ ...f, address: val }));
    setAddressSuggestions([]);
  }

  function handleOrgSubmit(e: FormEvent) {
    e.preventDefault();
    if (!orgForm) return;
    orgMutation.mutate({
      departmentId: orgForm.departmentId || null,
      jobRoleId: orgForm.jobRoleId || null,
      managerId: orgForm.managerId || null,
      orgRole: orgForm.orgRole || null,
      displayName: orgForm.displayName || null,
      email: orgForm.email || null,
      phoneNumber: orgForm.phoneNumber || null,
      address: orgForm.address || null,
      linkedInUrl: orgForm.linkedInUrl || null,
    });
  }

  const allJobRoles = useMemo(() => jobRolesQuery.data ?? [], [jobRolesQuery.data]);

  const selectedDepartment = useMemo(() => {
    if (!orgForm?.departmentId) return null;
    return departmentsQuery.data?.find((d) => d.departmentId === orgForm.departmentId);
  }, [departmentsQuery.data, orgForm?.departmentId]);

  const deptRoles = useMemo(() => {
    if (!orgForm?.departmentId) return [];
    return allJobRoles.filter((r) => r.departmentId === orgForm.departmentId);
  }, [allJobRoles, orgForm?.departmentId]);

  const otherRoles = useMemo(() => {
    if (!orgForm?.departmentId) return allJobRoles;
    return allJobRoles.filter((r) => r.departmentId !== orgForm.departmentId);
  }, [allJobRoles, orgForm?.departmentId]);

  const resumeQuery = useQuery({
    queryKey: ['person-resume', personId],
    queryFn: () => getPersonResume(personId),
  });

  const seededResumePersonIdRef = useRef<string | null>(null);

  useEffect(() => {
    if (resumeQuery.data && seededResumePersonIdRef.current !== personId) {
      seededResumePersonIdRef.current = personId;
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
      setAcademicProfile(r.studentAcademicProfile ?? null);
      if (r.address) {
        setOrgForm((prev) => (prev && !prev.address ? { ...prev, address: r.address ?? '' } : prev));
      }
      if (r.linkedInUrl) {
        setOrgForm((prev) => (prev && !prev.linkedInUrl ? { ...prev, linkedInUrl: r.linkedInUrl ?? '' } : prev));
      }
    }
  }, [resumeQuery.data, personId]);

  const saveMutation = useMutation({
    mutationFn: (body: SavePersonResumeRequest) => savePersonResume(personId, body),
    onSuccess: (savedData) => {
      queryClient.setQueryData(['person-resume', personId], savedData);
    },
  });

  // "Extraer con IA" — ResumeExtractionAgent (Microsoft Agent Framework, backend) reads the
  // raw pasted résumé/CV text and extracts structured fields from it. Returns a pure
  // PROPOSAL — it is never written into `form` directly; buildResumeDiffRows() computes a
  // before/after diff against a snapshot of the form taken right now, and the review panel
  // below lets the HRAdmin accept/reject each field individually before anything changes.
  const extractMutation = useMutation({
    mutationFn: async () => {
      const proposal = await extractPersonResume(personId, form.rawResumeText);
      if (proposal.studentAcademicProfile) {
        const saved = await savePersonResume(personId, {
          ...form,
          skills: linesToArray(form.skills),
          workExperience: linesToArray(form.workExperience),
          education: linesToArray(form.education),
          certifications: linesToArray(form.certifications),
          languages: linesToArray(form.languages),
          studentAcademicProfile: proposal.studentAcademicProfile,
        });
        queryClient.setQueryData(['person-resume', personId], saved);
      }
      return proposal;
    },
    onSuccess: (data) => {
      setAcademicProfile(data.studentAcademicProfile ?? null);
      const rows = buildResumeDiffRows(form, data, (k) => t(k as any));
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
      studentAcademicProfile: academicProfile,
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
        setPdfError(t('employeeDetail.pdfScannedError', { file: file.name }));
        return;
      }
      setForm((f) => ({ ...f, rawResumeText: text }));
    } catch {
      setPdfError(t('employeeDetail.pdfInvalidError', { file: file.name }));
    } finally {
      setIsReadingPdf(false);
    }
  }

  if (resumeQuery.isLoading) {
    return <p className="mx-auto max-w-3xl p-6 text-sm text-slate-400">{t('common.loading')}</p>;
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
            {person?.displayName || person?.email || t('employeeDetail.titleFallback')}
          </h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
            {person
              ? `${person.jobRoleTitle ?? t('people.unassignedRole')} · ${person.departmentName ?? t('people.unassignedDept')}`
              : t('employeeDetail.subtitleFallback')}
          </p>
        </div>
      </div>

      {saveMutation.error && (
        <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
          {saveMutation.error instanceof Error ? saveMutation.error.message : t('common.unexpectedError')}
        </div>
      )}
      {saveMutation.isSuccess && !saveMutation.isPending && (
        <div className="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-700 dark:border-emerald-500/20 dark:bg-emerald-500/10 dark:text-emerald-300">
          {t('employeeDetail.resumeSaved')}
        </div>
      )}

      {orgMutation.error && (
        <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
          {orgMutation.error instanceof Error ? orgMutation.error.message : t('common.unexpectedError')}
        </div>
      )}
      {orgMutation.isSuccess && !orgMutation.isPending && (
        <div className="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-700 dark:border-emerald-500/20 dark:bg-emerald-500/10 dark:text-emerald-300">
          {t('employeeDetail.orgSaved')}
        </div>
      )}

      {orgForm && (
        <form
          onSubmit={handleOrgSubmit}
          className="space-y-5 rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5"
        >
          <div className="flex items-center justify-between border-b border-slate-100 pb-3 dark:border-white/10">
            <h2 className="text-sm font-semibold text-slate-900 dark:text-white">{t('employeeDetail.orgAssignmentTitle')}</h2>
            <span className="text-xs text-slate-400 dark:text-white/40">ID: {personId}</span>
          </div>

          {/* Personal & Contact Information */}
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <div>
              <label className={labelClass}>
                <span className="inline-flex items-center gap-1.5">
                  <User className="h-3.5 w-3.5 text-sky-500" />
                  {t('employeeDetail.fullNameLabel')}
                </span>
              </label>
              <input
                type="text"
                value={orgForm.displayName}
                onChange={(e) => setOrgForm((f) => (f ? { ...f, displayName: e.target.value } : f))}
                placeholder="Nombre completo"
                className={inputClass}
              />
            </div>
            <div>
              <label className={labelClass}>
                <span className="inline-flex items-center gap-1.5">
                  <Mail className="h-3.5 w-3.5 text-sky-500" />
                  {t('employeeDetail.emailLabel')}
                </span>
              </label>
              <input
                type="email"
                value={orgForm.email}
                onChange={(e) => setOrgForm((f) => (f ? { ...f, email: e.target.value } : f))}
                placeholder="ejemplo@empresa.com"
                className={inputClass}
              />
            </div>
            <div>
              <label className={labelClass}>
                <span className="inline-flex items-center gap-1.5">
                  <Phone className="h-3.5 w-3.5 text-sky-500" />
                  {t('employeeDetail.phoneLabel')}
                </span>
              </label>
              <input
                type="tel"
                value={orgForm.phoneNumber}
                onChange={(e) => setOrgForm((f) => (f ? { ...f, phoneNumber: e.target.value } : f))}
                placeholder="+1 555 123 4567"
                className={inputClass}
              />
            </div>
          </div>

          {/* Address with Bing / Azure Maps autocomplete */}
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="relative">
              <label className={labelClass}>
                <span className="inline-flex items-center gap-1.5">
                  <MapPin className="h-3.5 w-3.5 text-emerald-500" />
                  {t('employeeDetail.addressLabel')}
                </span>
              </label>
              <div className="relative">
                <input
                  type="text"
                  value={orgForm.address}
                  onChange={(e) => handleAddressQueryChange(e.target.value)}
                  placeholder="Escribe una dirección (ej. Austin, TX)..."
                  className={inputClass}
                  autoComplete="off"
                />
                {isSearchingAddress && (
                  <span className="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-slate-400">
                    Buscando…
                  </span>
                )}
              </div>

              {/* Autocomplete Dropdown */}
              {addressSuggestions.length > 0 && (
                <div className="absolute left-0 right-0 top-full z-20 mt-1 max-h-60 overflow-y-auto rounded-xl border border-slate-200 bg-white py-1 shadow-xl dark:border-white/10 dark:bg-slate-900">
                  {addressSuggestions.map((sugg, idx) => (
                    <button
                      key={sugg.entityId || idx}
                      type="button"
                      onClick={() => selectAddressSuggestion(sugg)}
                      className="flex w-full items-start gap-2 px-3 py-2 text-left text-xs text-slate-700 transition hover:bg-sky-50 dark:text-slate-200 dark:hover:bg-sky-950/50"
                    >
                      <MapPin className="mt-0.5 h-3.5 w-3.5 shrink-0 text-emerald-500" />
                      <div className="flex-1">
                        <p className="font-medium text-slate-900 dark:text-white">{sugg.address}</p>
                        {(sugg.city || sugg.region || sugg.countryCode) && (
                          <p className="text-[11px] text-slate-400 dark:text-white/40">
                            {[sugg.city, sugg.region, sugg.countryCode, sugg.postalCode].filter(Boolean).join(', ')}
                          </p>
                        )}
                      </div>
                    </button>
                  ))}
                </div>
              )}
            </div>

            <div>
              <label className={labelClass}>
                <span className="inline-flex items-center gap-1.5">
                  <Globe className="h-3.5 w-3.5 text-sky-500" />
                  {t('employeeDetail.linkedInLabel')}
                </span>
              </label>
              <input
                type="text"
                value={orgForm.linkedInUrl}
                onChange={(e) => {
                  const val = e.target.value;
                  setOrgForm((f) => (f ? { ...f, linkedInUrl: val } : f));
                  setForm((f) => ({ ...f, linkedInUrl: val }));
                }}
                placeholder={t('employeeDetail.linkedInPlaceholder')}
                className={inputClass}
              />
            </div>
          </div>

          {/* Org structure / roles */}
          <div className="grid grid-cols-1 gap-4 pt-1 sm:grid-cols-2">
            <div>
              <label className={labelClass}>{t('employeeDetail.departmentLabel')}</label>
              <select
                value={orgForm.departmentId}
                onChange={(e) =>
                  setOrgForm((f) => (f ? { ...f, departmentId: e.target.value } : f))
                }
                className={inputClass}
              >
                <option value="">{t('common.unassigned')}</option>
                {departmentsQuery.data?.map((d) => (
                  <option key={d.departmentId} value={d.departmentId}>
                    {d.name}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className={labelClass}>{t('employeeDetail.roleLabel')}</label>
              <select
                value={orgForm.jobRoleId}
                onChange={(e) => {
                  const selectedId = e.target.value;
                  const foundRole = allJobRoles.find((r) => r.jobRoleId === selectedId);
                  setOrgForm((f) => {
                    if (!f) return f;
                    return {
                      ...f,
                      jobRoleId: selectedId,
                      departmentId: !f.departmentId && foundRole?.departmentId ? foundRole.departmentId : f.departmentId,
                    };
                  });
                }}
                className={inputClass}
              >
                <option value="">{t('common.unassigned')}</option>
                {deptRoles.length > 0 && (
                  <optgroup
                    label={t('employeeDetail.deptRolesGroup', {
                      dept: selectedDepartment?.name || t('employeeDetail.departmentLabel'),
                    })}
                  >
                    {deptRoles.map((r) => (
                      <option key={r.jobRoleId} value={r.jobRoleId}>
                        {r.title} {r.levelLabel ? `(${r.levelLabel})` : ''}
                      </option>
                    ))}
                  </optgroup>
                )}
                <optgroup label={deptRoles.length > 0 ? t('employeeDetail.allCompanyRolesGroup') : t('employeeDetail.allRolesLabel')}>
                  {(deptRoles.length > 0 ? otherRoles : allJobRoles).map((r) => (
                    <option key={r.jobRoleId} value={r.jobRoleId}>
                      {r.title} {r.levelLabel ? `[${r.levelLabel}]` : ''}{r.departmentName ? ` — ${r.departmentName}` : ''}
                    </option>
                  ))}
                </optgroup>
              </select>
            </div>
            <div>
              <label className={labelClass}>{t('employeeDetail.managerLabel')}</label>
              <select
                value={orgForm.managerId}
                onChange={(e) => setOrgForm((f) => (f ? { ...f, managerId: e.target.value } : f))}
                className={inputClass}
              >
                <option value="">{t('common.unassigned')}</option>
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
              <label className={labelClass}>{t('employeeDetail.permissionsLabel')}</label>
              <select
                value={orgForm.orgRole}
                onChange={(e) => setOrgForm((f) => (f ? { ...f, orgRole: e.target.value } : f))}
                className={inputClass}
              >
                <option value="Employee">{t('roles.Employee')}</option>
                <option value="Manager">{t('roles.Manager')}</option>
                <option value="HRAdmin">{t('roles.HRAdmin')}</option>
              </select>
            </div>
          </div>

          <div className="flex justify-end pt-2">
            <button
              type="submit"
              disabled={orgMutation.isPending}
              className="rounded-full bg-gradient-to-r from-sky-600 to-sky-500 px-5 py-2.5 text-sm font-semibold text-white shadow-md shadow-sky-500/30 transition hover:brightness-105 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {orgMutation.isPending ? t('common.saving') : t('employeeDetail.saveAssignment')}
            </button>
          </div>
        </form>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5">
          <div className="flex items-start justify-between gap-3">
            <div>
              <h2 className="text-sm font-semibold text-slate-900 dark:text-white">{t('employeeDetail.pasteResumeTitle')}</h2>
              <p className="mt-1 text-xs text-slate-400 dark:text-white/40">
                {t('employeeDetail.pasteResumeSubtitle')}
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
                {isReadingPdf ? t('employeeDetail.readingPdf') : t('employeeDetail.uploadPdf')}
              </button>
              <button
                type="button"
                onClick={() => extractMutation.mutate()}
                disabled={!canExtract || extractMutation.isPending}
                title={canExtract ? undefined : t('employeeDetail.extractTooltipDisabled')}
                className="flex items-center gap-2 rounded-full border border-amber-300 bg-amber-50 px-4 py-2 text-sm font-medium text-amber-700 transition hover:bg-amber-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-300 dark:hover:bg-amber-500/20"
              >
                <Sparkles className="h-4 w-4" />
                {extractMutation.isPending ? t('employeeDetail.extracting') : t('employeeDetail.extractAi')}
              </button>
            </div>
          </div>

          {pdfError && <p className="text-xs text-red-600 dark:text-red-400">{pdfError}</p>}
          <textarea
            value={form.rawResumeText}
            onChange={(e) => setForm((f) => ({ ...f, rawResumeText: e.target.value }))}
            rows={10}
            placeholder={t('employeeDetail.pasteResumePlaceholder')}
            className={inputClass}
          />
        </section>

        {extractMutation.error && (
          <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
            {extractMutation.error instanceof Error ? extractMutation.error.message : t('employeeDetail.extractError')}
          </div>
        )}

        {academicProfile && academicProfile.semesters.length > 0 && (
          <section className="space-y-4 rounded-2xl border border-emerald-200 bg-emerald-50/50 p-5 dark:border-emerald-500/20 dark:bg-emerald-500/10">
            <div>
              <h2 className="text-sm font-semibold text-emerald-900 dark:text-emerald-200">Perfil académico estructurado</h2>
              <p className="mt-1 text-xs text-emerald-800/70 dark:text-emerald-200/70">
                {academicProfile.institution} · {academicProfile.degree} · Promedio acumulado: {academicProfile.cumulativeAverage ?? 'N/D'} / {academicProfile.averageScale ?? '100'}
              </p>
            </div>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              {academicProfile.semesters.map((semester) => (
                <div key={semester.name} className="rounded-xl border border-emerald-200 bg-white p-3 dark:border-emerald-500/20 dark:bg-white/5">
                  <div className="flex justify-between text-sm font-semibold text-slate-800 dark:text-white">
                    <span>{semester.name}</span><span>{semester.average ?? 'N/D'}</span>
                  </div>
                  <div className="mt-2 space-y-1 text-xs text-slate-600 dark:text-white/70">
                    {semester.subjects.map((subject) => <div key={subject.name} className="flex justify-between gap-2"><span>{subject.name}</span><span>{subject.grade ?? 'N/D'}</span></div>)}
                  </div>
                </div>
              ))}
            </div>
          </section>
        )}

        {reviewRows && (
          <div className="space-y-4 rounded-2xl border border-amber-200 bg-amber-50/60 p-5 dark:border-amber-500/20 dark:bg-amber-500/10">
            <div className="flex items-start justify-between gap-3">
              <div>
                <h2 className="flex items-center gap-2 text-sm font-semibold text-amber-900 dark:text-amber-200">
                  <Sparkles className="h-4 w-4" />
                  {t('employeeDetail.aiProposalTitle')}
                </h2>
                <p className="mt-1 text-xs text-amber-800/70 dark:text-amber-200/70">
                  {t('employeeDetail.aiProposalSubtitle')}
                </p>
              </div>
              <button
                type="button"
                onClick={closeReview}
                className="shrink-0 text-xs font-medium text-amber-800/80 hover:underline dark:text-amber-200/80"
              >
                {t('common.close')}
              </button>
            </div>

            {reviewRows.length === 0 ? (
              <p className="text-xs text-amber-800/70 dark:text-amber-200/70">
                {t('employeeDetail.aiProposalEmpty')}
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
                                {t('common.restoreSuggestion')}
                              </button>
                            )}
                          </div>
                          <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                            {row.hasPreviousValue ? (
                              <div className="whitespace-pre-wrap rounded-lg bg-red-50 px-2.5 py-1.5 text-xs text-red-700 line-through decoration-red-300 dark:bg-red-500/10 dark:text-red-300">
                                {row.before}
                              </div>
                            ) : (
                              <div className="flex items-center rounded-lg border border-dashed border-slate-200 bg-slate-50 px-2.5 py-1.5 text-xs italic text-slate-400 dark:border-white/10 dark:bg-white/5 dark:text-white/40">
                                {t('common.noPreviousValue')}
                              </div>
                            )}
                            <textarea
                              value={currentValue}
                              onChange={(e) => updateEditedValue(row.key, e.target.value)}
                              rows={Math.min(10, Math.max(3, currentValue.split('\n').length + 1))}
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
                    {t('common.discardAll')}
                  </button>
                  <button
                    type="button"
                    onClick={applyAcceptedProposals}
                    disabled={acceptedKeys.size === 0}
                    className="rounded-full bg-amber-500 px-4 py-2 text-xs font-semibold text-white hover:bg-amber-600 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    {t('common.applySelected', { count: acceptedKeys.size })}
                  </button>
                </div>
              </>
            )}
          </div>
        )}

        <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5">
          <h2 className="text-sm font-semibold text-slate-900 dark:text-white">{t('employeeDetail.contactTitle')}</h2>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass}>{t('employeeDetail.addressLabel')}</label>
              <input
                value={form.address}
                onChange={(e) => setForm((f) => ({ ...f, address: e.target.value }))}
                className={inputClass}
              />
            </div>
            <div>
              <label className={labelClass}>{t('employeeDetail.linkedInLabel')}</label>
              <input
                value={form.linkedInUrl}
                onChange={(e) => setForm((f) => ({ ...f, linkedInUrl: e.target.value }))}
                placeholder={t('employeeDetail.linkedInPlaceholder')}
                className={inputClass}
              />
            </div>
          </div>
        </section>

        <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5">
          <h2 className="text-sm font-semibold text-slate-900 dark:text-white">{t('employeeDetail.professionalProfileTitle')}</h2>
          <div>
            <label className={labelClass}>{t('employeeDetail.objectiveLabel')}</label>
            <textarea
              value={form.professionalObjective}
              onChange={(e) => setForm((f) => ({ ...f, professionalObjective: e.target.value }))}
              rows={2}
              className={inputClass}
            />
          </div>
          <div>
            <label className={labelClass}>{t('employeeDetail.summaryLabel')}</label>
            <textarea
              value={form.professionalSummary}
              onChange={(e) => setForm((f) => ({ ...f, professionalSummary: e.target.value }))}
              rows={3}
              className={inputClass}
            />
          </div>
          <div>
            <label className={labelClass}>{t('employeeDetail.skillsLabel')}</label>
            <div className="space-y-3">
              <div className="flex flex-wrap gap-2 rounded-lg border border-slate-200 bg-slate-50 p-4 dark:border-white/10 dark:bg-white/5">
                {linesToArray(form.skills).length > 0 ? (
                  linesToArray(form.skills).map((skill, idx) => (
                    <div
                      key={idx}
                      className="inline-flex items-center gap-2 rounded-full bg-sky-100 px-3 py-1.5 text-sm text-sky-900 dark:bg-sky-500/20 dark:text-sky-200"
                    >
                      {skill}
                      <button
                        type="button"
                        onClick={() => {
                          const skills = linesToArray(form.skills);
                          skills.splice(idx, 1);
                          setForm((f) => ({ ...f, skills: arrayToLines(skills) }));
                        }}
                        className="ml-1 text-sky-600 hover:text-sky-800 dark:text-sky-300 dark:hover:text-sky-100"
                      >
                        ✕
                      </button>
                    </div>
                  ))
                ) : (
                  <p className="text-xs text-slate-400 dark:text-white/50">No skills yet</p>
                )}
              </div>
              <textarea
                value={form.skills}
                onChange={(e) => setForm((f) => ({ ...f, skills: e.target.value }))}
                rows={4}
                placeholder="Skills (one per line)"
                className={inputClass}
              />
            </div>
          </div>
        </section>

        <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5">
          <h2 className="text-sm font-semibold text-slate-900 dark:text-white">{t('employeeDetail.careerPathTitle')}</h2>
          <div>
            <label className={labelClass}>{t('employeeDetail.workExperienceLabel')}</label>
            <div className="space-y-3">
              {/* Card Display */}
              <div className="space-y-2 rounded-lg border border-slate-200 bg-slate-50 p-4 dark:border-white/10 dark:bg-white/5">
                {linesToArray(form.workExperience).length > 0 ? (
                  linesToArray(form.workExperience).map((exp, idx) => {
                    const parts = exp.split(' — ');
                    const company = parts[0] || 'Unknown';
                    return (
                      <div
                        key={idx}
                        className="rounded-lg border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-white/10"
                      >
                        <div className="flex items-start justify-between gap-3">
                          <div className="flex-1">
                            <h3 className="font-semibold text-slate-900 dark:text-white">{company}</h3>
                            <p className="mt-1 text-sm text-slate-600 whitespace-pre-wrap dark:text-white/70">
                              {parts.slice(1).join(' — ')}
                            </p>
                          </div>
                          <button
                            type="button"
                            onClick={() => {
                              const exps = linesToArray(form.workExperience);
                              exps.splice(idx, 1);
                              setForm((f) => ({ ...f, workExperience: arrayToLines(exps) }));
                            }}
                            className="shrink-0 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-200"
                          >
                            ✕
                          </button>
                        </div>
                      </div>
                    );
                  })
                ) : (
                  <p className="text-xs text-slate-400 dark:text-white/50">No work experience yet</p>
                )}
              </div>
              {/* Text Area for editing */}
              <textarea
                value={form.workExperience}
                onChange={(e) => setForm((f) => ({ ...f, workExperience: e.target.value }))}
                rows={6}
                placeholder="Work Experience (one company per line)"
                className={inputClass}
              />
            </div>
          </div>
          <div>
            <label className={labelClass}>{t('employeeDetail.educationLabel')}</label>
            <div className="space-y-3">
              {/* Card Display for Education */}
              <div className="space-y-2 rounded-lg border border-slate-200 bg-slate-50 p-4 dark:border-white/10 dark:bg-white/5">
                {linesToArray(form.education).length > 0 ? (
                  linesToArray(form.education).map((edu, idx) => (
                    <div
                      key={idx}
                      className="flex items-start justify-between gap-3 rounded-lg border border-slate-200 bg-white p-3 dark:border-white/10 dark:bg-white/10"
                    >
                      <div className="flex-1">
                        <p className="text-sm font-medium text-slate-900 dark:text-white">{edu}</p>
                      </div>
                      <button
                        type="button"
                        onClick={() => {
                          const edus = linesToArray(form.education);
                          edus.splice(idx, 1);
                          setForm((f) => ({ ...f, education: arrayToLines(edus) }));
                        }}
                        className="shrink-0 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-200"
                      >
                        ✕
                      </button>
                    </div>
                  ))
                ) : (
                  <p className="text-xs text-slate-400 dark:text-white/50">No education yet</p>
                )}
              </div>
              <textarea
                value={form.education}
                onChange={(e) => setForm((f) => ({ ...f, education: e.target.value }))}
                rows={4}
                placeholder="Education (one entry per line)"
                className={inputClass}
              />
            </div>
          </div>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass}>{t('employeeDetail.certificationsLabel')}</label>
              <div className="space-y-3">
                <div className="flex flex-wrap gap-2 rounded-lg border border-slate-200 bg-slate-50 p-4 dark:border-white/10 dark:bg-white/5">
                  {linesToArray(form.certifications).length > 0 ? (
                    linesToArray(form.certifications).map((cert, idx) => (
                      <div
                        key={idx}
                        className="inline-flex items-center gap-2 rounded-full bg-emerald-100 px-3 py-1.5 text-sm text-emerald-900 dark:bg-emerald-500/20 dark:text-emerald-200"
                      >
                        {cert}
                        <button
                          type="button"
                          onClick={() => {
                            const certs = linesToArray(form.certifications);
                            certs.splice(idx, 1);
                            setForm((f) => ({ ...f, certifications: arrayToLines(certs) }));
                          }}
                          className="ml-1 text-emerald-600 hover:text-emerald-800 dark:text-emerald-300 dark:hover:text-emerald-100"
                        >
                          ✕
                        </button>
                      </div>
                    ))
                  ) : (
                    <p className="text-xs text-slate-400 dark:text-white/50">No certifications yet</p>
                  )}
                </div>
                <textarea
                  value={form.certifications}
                  onChange={(e) => setForm((f) => ({ ...f, certifications: e.target.value }))}
                  rows={3}
                  placeholder="Certifications (one per line)"
                  className={inputClass}
                />
              </div>
            </div>
            <div>
              <label className={labelClass}>{t('employeeDetail.languagesLabel')}</label>
              <div className="space-y-3">
                <div className="flex flex-wrap gap-2 rounded-lg border border-slate-200 bg-slate-50 p-4 dark:border-white/10 dark:bg-white/5">
                  {linesToArray(form.languages).length > 0 ? (
                    linesToArray(form.languages).map((lang, idx) => (
                      <div
                        key={idx}
                        className="inline-flex items-center gap-2 rounded-full bg-purple-100 px-3 py-1.5 text-sm text-purple-900 dark:bg-purple-500/20 dark:text-purple-200"
                      >
                        {lang}
                        <button
                          type="button"
                          onClick={() => {
                            const langs = linesToArray(form.languages);
                            langs.splice(idx, 1);
                            setForm((f) => ({ ...f, languages: arrayToLines(langs) }));
                          }}
                          className="ml-1 text-purple-600 hover:text-purple-800 dark:text-purple-300 dark:hover:text-purple-100"
                        >
                          ✕
                        </button>
                      </div>
                    ))
                  ) : (
                    <p className="text-xs text-slate-400 dark:text-white/50">No languages yet</p>
                  )}
                </div>
                <textarea
                  value={form.languages}
                  onChange={(e) => setForm((f) => ({ ...f, languages: e.target.value }))}
                  rows={3}
                  placeholder="Languages (one per line)"
                  className={inputClass}
                />
              </div>
            </div>
          </div>
        </section>

        <div className="flex justify-end gap-2">
          <button
            type="submit"
            disabled={saveMutation.isPending}
            className="rounded-full bg-sky-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-sky-700 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {saveMutation.isPending ? t('common.saving') : t('employeeDetail.saveButton')}
          </button>
        </div>
      </form>
    </div>
  );
}
