import { useEffect, useMemo, useRef, useState, type FormEvent, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useLocation, useNavigate, useParams } from 'react-router';
import {
  ArrowLeft,
  Award,
  BookOpen,
  Briefcase,
  CheckCircle2,
  Cpu,
  ExternalLink,
  Flame,
  Globe,
  GraduationCap,
  Handshake,
  Heart,
  ListChecks,
  RefreshCw,
  Rocket,
  Sparkles,
  Wrench,
  X,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import {
  attachDepartmentJobRole,
  createJobRole,
  generateJobRoleDescription,
  getJobRole,
  getJobRoles,
  updateJobRole,
  type GenerateJobRoleDescriptionRequest,
  type GenerateJobRoleDescriptionResponse,
  type SaveJobRoleRequest,
} from '@/api/orgStructureApi';
import { getSkills } from '@/api/skillsApi';
import { getResponsibilities } from '@/api/responsibilitiesApi';
import { getCertifications } from '@/api/certificationsApi';
import { getTools } from '@/api/toolsApi';

function linesToArray(value: string): string[] {
  return value
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean);
}

function arrayToLines(value: string[] | undefined): string {
  return (value ?? []).join('\n');
}

function normalizeItemName(name: string): string {
  return name.trim().toLowerCase().replace(/[.\s]+$/, '');
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
  hasPreviousValue: boolean;
  after: string;
  applyValue: string | boolean;
}

/// Compares the AI proposal against the CURRENT form snapshot (taken at the moment the
/// proposal arrived — never re-diffed against a form the user keeps editing) and returns
/// only the fields that genuinely changed, for the before/after review UI.
function buildDescriptionDiffRows(
  formSnapshot: RoleFormState,
  proposal: GenerateJobRoleDescriptionResponse,
  t: (key: string, options?: Record<string, unknown>) => string,
): DescriptionDiffRow[] {
  const rows: DescriptionDiffRow[] = [];

  function addText(key: RoleFormKey, labelKey: string, current: string, proposed: string | null | undefined) {
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

  function addList(key: RoleFormKey, labelKey: string, current: string, proposed: string[]) {
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

  addText('levelLabel', 'roleDetail.diffFieldLabels.levelLabel', formSnapshot.levelLabel, proposal.levelLabel);
  addText('purpose', 'roleDetail.diffFieldLabels.purpose', formSnapshot.purpose, proposal.purpose);
  addText('summary', 'roleDetail.diffFieldLabels.summary', formSnapshot.summary, proposal.summary);
  addList('responsibilities', 'roleDetail.diffFieldLabels.responsibilities', formSnapshot.responsibilities, proposal.responsibilities);
  addText('requiredExperience', 'roleDetail.diffFieldLabels.requiredExperience', formSnapshot.requiredExperience, proposal.requiredExperience);
  addText('requiredEducation', 'roleDetail.diffFieldLabels.requiredEducation', formSnapshot.requiredEducation, proposal.requiredEducation);
  addList('technicalSkills', 'roleDetail.diffFieldLabels.technicalSkills', formSnapshot.technicalSkills, proposal.technicalSkills);
  addList('requiredCertifications', 'roleDetail.diffFieldLabels.requiredCertifications', formSnapshot.requiredCertifications, proposal.requiredCertifications);
  addList('requiredLanguages', 'roleDetail.diffFieldLabels.requiredLanguages', formSnapshot.requiredLanguages, proposal.requiredLanguages);
  addList('softSkills', 'roleDetail.diffFieldLabels.softSkills', formSnapshot.softSkills, proposal.softSkills);
  addList('tools', 'roleDetail.diffFieldLabels.tools', formSnapshot.tools, proposal.tools);
  addList('performanceIndicators', 'roleDetail.diffFieldLabels.performanceIndicators', formSnapshot.performanceIndicators, proposal.performanceIndicators);
  addText('workSchedule', 'roleDetail.diffFieldLabels.workSchedule', formSnapshot.workSchedule, proposal.workSchedule);
  addText('workModality', 'roleDetail.diffFieldLabels.workModality', formSnapshot.workModality, proposal.workModality);
  addText('workLocation', 'roleDetail.diffFieldLabels.workLocation', formSnapshot.workLocation, proposal.workLocation);
  addText('availabilityNotes', 'roleDetail.diffFieldLabels.availabilityNotes', formSnapshot.availabilityNotes, proposal.availabilityNotes);
  addText('employmentType', 'roleDetail.diffFieldLabels.employmentType', formSnapshot.employmentType, proposal.employmentType);
  addList('benefits', 'roleDetail.diffFieldLabels.benefits', formSnapshot.benefits, proposal.benefits);
  addText('salaryCurrency', 'roleDetail.diffFieldLabels.salaryCurrency', formSnapshot.salaryCurrency, proposal.salaryCurrency);

  if (proposal.requiresTravel !== formSnapshot.requiresTravel) {
    rows.push({
      key: 'requiresTravel',
      label: t('roleDetail.diffFieldLabels.requiresTravel'),
      before: formSnapshot.requiresTravel ? t('common.yes') : t('common.no'),
      hasPreviousValue: true,
      after: proposal.requiresTravel ? t('common.yes') : t('common.no'),
      applyValue: proposal.requiresTravel,
    });
  }

  if (proposal.salaryRangeMin != null && String(proposal.salaryRangeMin) !== formSnapshot.salaryRangeMin) {
    rows.push({
      key: 'salaryRangeMin',
      label: t('roleDetail.diffFieldLabels.salaryRangeMin'),
      before: formSnapshot.salaryRangeMin,
      hasPreviousValue: Boolean(formSnapshot.salaryRangeMin),
      after: String(proposal.salaryRangeMin),
      applyValue: String(proposal.salaryRangeMin),
    });
  }

  if (proposal.salaryRangeMax != null && String(proposal.salaryRangeMax) !== formSnapshot.salaryRangeMax) {
    rows.push({
      key: 'salaryRangeMax',
      label: t('roleDetail.diffFieldLabels.salaryRangeMax'),
      before: formSnapshot.salaryRangeMax,
      hasPreviousValue: Boolean(formSnapshot.salaryRangeMax),
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
  startInYourRoleTechnicalSkills: '',
  startInYourRoleSoftSkills: '',
  startInYourRoleDepartmentProcesses: '',
  startInYourRoleHrProcesses: '',
  inYourRoleTechnicalSkills: '',
  inYourRoleSoftSkills: '',
  inYourRoleDepartmentProcesses: '',
  inYourRoleHrProcesses: '',
};

const inputClass =
  'mt-1 w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white';
const labelClass = 'block text-xs font-medium text-slate-500 dark:text-white/50';

const SENIORITY_LEVEL_KEYS = [
  'cLevel',
  'vp',
  'director',
  'manager',
  'lead',
  'senior',
  'mid',
  'junior',
  'associate',
  'coordinator',
  'specialist',
  'analyst',
  'assistant',
  'intern',
] as const;

const EDUCATION_LEVEL_KEYS = [
  'basicEducation',
  'highSchool',
  'technicalDegree',
  'bachelorsDegree',
  'specialization',
  'mastersDegree',
  'doctorate',
  'postDoctorate',
  'notRequired',
] as const;

const COMMON_LANGUAGE_KEYS = [
  'english',
  'spanish',
  'mandarin',
  'french',
  'german',
  'portuguese',
  'japanese',
  'italian',
  'russian',
  'arabic',
] as const;

const EMPLOYMENT_TYPE_KEYS = [
  'fullTime',
  'partTime',
  'permanent',
  'temporary',
  'contractor',
  'internship',
  'byProject',
  'seasonal',
] as const;

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
  const { t, i18n } = useTranslation();
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const navigate = useNavigate();
  const location = useLocation();
  const queryClient = useQueryClient();
  const params = useParams<{ jobRoleId?: string }>();
  const jobRoleId = params.jobRoleId;
  const isEditing = Boolean(jobRoleId);

  const locationState = location.state as { returnUrl?: string; departmentId?: string } | null;
  const searchParams = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const returnUrl = locationState?.returnUrl || searchParams.get('returnUrl') || '/roles';
  const callerDepartmentId = locationState?.departmentId || searchParams.get('departmentId') || null;

  type RoleDetailTab = 'editJobRole' | 'startInYourRole' | 'inYourRole' | 'stayCurrent' | 'masterYourJob';
  const [activeTab, setActiveTab] = useState<RoleDetailTab>('editJobRole');

  const [form, setForm] = useState(emptyForm);
  const [reviewRows, setReviewRows] = useState<DescriptionDiffRow[] | null>(null);
  const [acceptedKeys, setAcceptedKeys] = useState<Set<RoleFormKey>>(new Set());
  // Lets the HRAdmin tweak the AI's "after" text per field before applying it — starts as a
  // copy of each row's applyValue and only diverges once the user edits that field's box.
  const [editedValues, setEditedValues] = useState<Partial<Record<RoleFormKey, string | boolean>>>({});

  const jobRolesQuery = useQuery({
    queryKey: ['job-roles', tenantId],
    queryFn: () => getJobRoles(tenantId),
  });

  const roleQuery = useQuery({
    queryKey: ['job-role', jobRoleId],
    queryFn: () => getJobRole(jobRoleId!),
    enabled: isEditing,
  });

  const responsibilitiesCatalogQuery = useQuery({
    queryKey: ['responsibilities', tenantId],
    queryFn: () => getResponsibilities(tenantId),
  });

  const responsibilitiesList = useMemo(() => linesToArray(form.responsibilities), [form.responsibilities]);

  const addResponsibility = (respName: string) => {
    const trimmed = respName.trim();
    if (!trimmed) return;
    if (responsibilitiesList.some((r) => r.toLowerCase() === trimmed.toLowerCase())) return;
    const nextList = [...responsibilitiesList, trimmed];
    setForm((f) => ({ ...f, responsibilities: arrayToLines(nextList) }));
  };

  const removeResponsibility = (respName: string) => {
    const nextList = responsibilitiesList.filter((r) => r.toLowerCase() !== respName.toLowerCase());
    setForm((f) => ({ ...f, responsibilities: arrayToLines(nextList) }));
  };

  const technicalSkillsCatalogQuery = useQuery({
    queryKey: ['skills', tenantId, 'job-skills'],
    queryFn: () => getSkills(tenantId, 'job-skills'),
  });

  const technicalSkillsList = useMemo(() => linesToArray(form.technicalSkills), [form.technicalSkills]);

  const addTechnicalSkill = (skillName: string) => {
    const trimmed = skillName.trim();
    if (!trimmed) return;
    if (technicalSkillsList.some((s) => s.toLowerCase() === trimmed.toLowerCase())) return;
    const nextList = [...technicalSkillsList, trimmed];
    setForm((f) => ({ ...f, technicalSkills: arrayToLines(nextList) }));
  };

  const removeTechnicalSkill = (skillName: string) => {
    const nextList = technicalSkillsList.filter((s) => s.toLowerCase() !== skillName.toLowerCase());
    setForm((f) => ({ ...f, technicalSkills: arrayToLines(nextList) }));
  };

  const languagesList = useMemo(() => linesToArray(form.requiredLanguages), [form.requiredLanguages]);

  const addLanguage = (langName: string) => {
    const trimmed = langName.trim();
    if (!trimmed) return;
    if (languagesList.some((l) => l.toLowerCase() === trimmed.toLowerCase())) return;
    const nextList = [...languagesList, trimmed];
    setForm((f) => ({ ...f, requiredLanguages: arrayToLines(nextList) }));
  };

  const removeLanguage = (langName: string) => {
    const nextList = languagesList.filter((l) => l.toLowerCase() !== langName.toLowerCase());
    setForm((f) => ({ ...f, requiredLanguages: arrayToLines(nextList) }));
  };

  const certificationsCatalogQuery = useQuery({
    queryKey: ['certifications', tenantId],
    queryFn: () => getCertifications(tenantId),
  });

  const certificationsList = useMemo(() => linesToArray(form.requiredCertifications), [form.requiredCertifications]);

  const addCertification = (certName: string) => {
    const trimmed = certName.trim();
    if (!trimmed) return;
    if (certificationsList.some((c) => c.toLowerCase() === trimmed.toLowerCase())) return;
    const nextList = [...certificationsList, trimmed];
    setForm((f) => ({ ...f, requiredCertifications: arrayToLines(nextList) }));
  };

  const removeCertification = (certName: string) => {
    const nextList = certificationsList.filter((c) => c.toLowerCase() !== certName.toLowerCase());
    setForm((f) => ({ ...f, requiredCertifications: arrayToLines(nextList) }));
  };

  const softSkillsCatalogQuery = useQuery({
    queryKey: ['skills', tenantId, 'soft-skills'],
    queryFn: () => getSkills(tenantId, 'soft-skills'),
  });

  const softSkillsList = useMemo(() => linesToArray(form.softSkills), [form.softSkills]);

  const addSoftSkill = (skillName: string) => {
    const trimmed = skillName.trim();
    if (!trimmed) return;
    if (softSkillsList.some((s) => s.toLowerCase() === trimmed.toLowerCase())) return;
    const nextList = [...softSkillsList, trimmed];
    setForm((f) => ({ ...f, softSkills: arrayToLines(nextList) }));
  };

  const removeSoftSkill = (skillName: string) => {
    const nextList = softSkillsList.filter((s) => s.toLowerCase() !== skillName.toLowerCase());
    setForm((f) => ({ ...f, softSkills: arrayToLines(nextList) }));
  };

  const toolsCatalogQuery = useQuery({
    queryKey: ['tools', tenantId],
    queryFn: () => getTools(tenantId),
  });

  const toolsList = useMemo(() => linesToArray(form.tools), [form.tools]);

  const addTool = (toolName: string) => {
    const trimmed = toolName.trim();
    if (!trimmed) return;
    if (toolsList.some((t) => t.toLowerCase() === trimmed.toLowerCase())) return;
    const nextList = [...toolsList, trimmed];
    setForm((f) => ({ ...f, tools: arrayToLines(nextList) }));
  };

  const removeTool = (toolName: string) => {
    const nextList = toolsList.filter((t) => t.toLowerCase() !== toolName.toLowerCase());
    setForm((f) => ({ ...f, tools: arrayToLines(nextList) }));
  };

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
        departmentId: r.departmentId ?? callerDepartmentId ?? '',
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
        startInYourRoleTechnicalSkills: arrayToLines(
          r.startInYourRoleTechnicalSkills && r.startInYourRoleTechnicalSkills.length > 0
            ? r.startInYourRoleTechnicalSkills
            : (r.technicalSkills ?? [])
        ),
        startInYourRoleSoftSkills: arrayToLines(
          r.startInYourRoleSoftSkills && r.startInYourRoleSoftSkills.length > 0
            ? r.startInYourRoleSoftSkills
            : (r.softSkills ?? [])
        ),
        startInYourRoleDepartmentProcesses: arrayToLines(
          r.startInYourRoleDepartmentProcesses && r.startInYourRoleDepartmentProcesses.length > 0
            ? r.startInYourRoleDepartmentProcesses
            : (r.departmentProcesses ?? [])
        ),
        startInYourRoleHrProcesses: arrayToLines(
          r.startInYourRoleHrProcesses && r.startInYourRoleHrProcesses.length > 0
            ? r.startInYourRoleHrProcesses
            : (r.hrProcesses ?? [])
        ),
        inYourRoleTechnicalSkills: arrayToLines(
          r.inYourRoleTechnicalSkills && r.inYourRoleTechnicalSkills.length > 0
            ? r.inYourRoleTechnicalSkills
            : (r.technicalSkills ?? [])
        ),
        inYourRoleSoftSkills: arrayToLines(
          r.inYourRoleSoftSkills && r.inYourRoleSoftSkills.length > 0
            ? r.inYourRoleSoftSkills
            : (r.softSkills ?? [])
        ),
        inYourRoleDepartmentProcesses: arrayToLines(
          r.inYourRoleDepartmentProcesses && r.inYourRoleDepartmentProcesses.length > 0
            ? r.inYourRoleDepartmentProcesses
            : (r.departmentProcesses ?? [])
        ),
        inYourRoleHrProcesses: arrayToLines(
          r.inYourRoleHrProcesses && r.inYourRoleHrProcesses.length > 0
            ? r.inYourRoleHrProcesses
            : (r.hrProcesses ?? [])
        ),
      });
    } else if (!isEditing && callerDepartmentId) {
      setForm((f) => (f.departmentId ? f : { ...f, departmentId: callerDepartmentId }));
    }
  }, [roleQuery.data, isEditing, jobRoleId, callerDepartmentId]);

  const otherJobRoles = useMemo(
    () => (jobRolesQuery.data ?? []).filter((r) => r.jobRoleId !== jobRoleId),
    [jobRolesQuery.data, jobRoleId],
  );

  const createMutation = useMutation({
    mutationFn: async (body: SaveJobRoleRequest) => {
      const created = await createJobRole(tenantId, body);
      if (callerDepartmentId) {
        try {
          await attachDepartmentJobRole(callerDepartmentId, created.jobRoleId);
        } catch {
          // Ignore if already linked
        }
      }
      return created;
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId] });
      if (callerDepartmentId) {
        void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId, callerDepartmentId] });
        void queryClient.invalidateQueries({ queryKey: ['departments', tenantId] });
        void queryClient.invalidateQueries({ queryKey: ['department', callerDepartmentId] });
      }
      navigate(returnUrl);
    },
  });

  const [saveSuccessNotification, setSaveSuccessNotification] = useState<string | null>(null);

  const updateMutation = useMutation({
    mutationFn: (body: SaveJobRoleRequest) => updateJobRole(jobRoleId!, body),
    onSuccess: (updated) => {
      void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId] });
      void queryClient.invalidateQueries({ queryKey: ['job-role', jobRoleId] });
      if (callerDepartmentId) {
        void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId, callerDepartmentId] });
        void queryClient.invalidateQueries({ queryKey: ['departments', tenantId] });
        void queryClient.invalidateQueries({ queryKey: ['department', callerDepartmentId] });
      }
      // Actualizar el estado local para asegurar sincronización sin salir de la página
      setForm((f) => ({
        ...f,
        startInYourRoleTechnicalSkills: arrayToLines(updated.startInYourRoleTechnicalSkills),
        startInYourRoleSoftSkills: arrayToLines(updated.startInYourRoleSoftSkills),
        startInYourRoleDepartmentProcesses: arrayToLines(updated.startInYourRoleDepartmentProcesses),
        startInYourRoleHrProcesses: arrayToLines(updated.startInYourRoleHrProcesses),
        inYourRoleTechnicalSkills: arrayToLines(updated.inYourRoleTechnicalSkills),
        inYourRoleSoftSkills: arrayToLines(updated.inYourRoleSoftSkills),
        inYourRoleDepartmentProcesses: arrayToLines(updated.inYourRoleDepartmentProcesses),
        inYourRoleHrProcesses: arrayToLines(updated.inYourRoleHrProcesses),
      }));
      setSaveSuccessNotification(i18n.language === 'es' ? 'Cambios guardados exitosamente.' : 'Changes saved successfully.');
      setTimeout(() => setSaveSuccessNotification(null), 3500);
    },
  });

  function handleGoBack() {
    if (locationState?.returnUrl || searchParams.get('returnUrl')) {
      navigate(returnUrl);
    } else if (window.history.length > 1) {
      navigate(-1);
    } else {
      navigate('/roles');
    }
  }

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
        languageCode: i18n.language.startsWith('en') ? 'en' : 'es',
        jobRoleId: jobRoleId ?? null,
        departmentId: form.departmentId || null,
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
      const rows = buildDescriptionDiffRows(form, data, t);
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
      departmentId: form.departmentId || null,
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
      startInYourRoleTechnicalSkills: linesToArray(form.startInYourRoleTechnicalSkills),
      startInYourRoleSoftSkills: linesToArray(form.startInYourRoleSoftSkills),
      startInYourRoleDepartmentProcesses: linesToArray(form.startInYourRoleDepartmentProcesses),
      startInYourRoleHrProcesses: linesToArray(form.startInYourRoleHrProcesses),
      inYourRoleTechnicalSkills: linesToArray(form.inYourRoleTechnicalSkills),
      inYourRoleSoftSkills: linesToArray(form.inYourRoleSoftSkills),
      inYourRoleDepartmentProcesses: linesToArray(form.inYourRoleDepartmentProcesses),
      inYourRoleHrProcesses: linesToArray(form.inYourRoleHrProcesses),
    };

    if (isEditing) {
      updateMutation.mutate(body);
    } else {
      createMutation.mutate(body);
    }
  }

  if (isEditing && roleQuery.isLoading) {
    return <p className="mx-auto max-w-3xl p-6 text-sm text-slate-400">{t('common.loading')}</p>;
  }

  const canGenerate = Boolean(form.title.trim());

  return (
    <div className="mx-auto max-w-3xl space-y-6 pb-16">
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={handleGoBack}
            className="flex h-8 w-8 items-center justify-center rounded-full text-slate-400 hover:bg-slate-100 dark:hover:bg-white/10"
          >
            <ArrowLeft className="h-4 w-4" />
          </button>
          <div>
            <h1 className="flex items-center gap-2 text-2xl font-semibold text-slate-900 dark:text-white">
              <Briefcase className="h-5 w-5 text-amber-500" />
              {isEditing ? t('roleDetail.titleEdit') : t('roleDetail.titleNew')}
            </h1>
            <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
              {t('roleDetail.subtitle')}
            </p>
          </div>
        </div>
        <button
          type="button"
          onClick={() => generateMutation.mutate()}
          disabled={!canGenerate || generateMutation.isPending}
          title={canGenerate ? undefined : t('roleDetail.generateTooltipDisabled')}
          className="flex shrink-0 items-center gap-2 rounded-full border border-amber-300 bg-amber-50 px-4 py-2 text-sm font-medium text-amber-700 transition hover:bg-amber-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-300 dark:hover:bg-amber-500/20"
        >
          <Sparkles className="h-4 w-4" />
          {generateMutation.isPending ? t('roleDetail.generating') : t('roleDetail.generateAi')}
        </button>
      </div>

      <div className="flex gap-1 overflow-x-auto border-b border-slate-200 dark:border-white/10" role="tablist">
        {(['editJobRole', 'startInYourRole', 'inYourRole', 'stayCurrent', 'masterYourJob'] as const).map((tab) => (
          <button
            key={tab}
            type="button"
            role="tab"
            aria-selected={activeTab === tab}
            onClick={() => setActiveTab(tab)}
            className={`shrink-0 border-b-2 px-3 py-2 text-xs font-medium transition ${
              activeTab === tab
                ? 'border-brand-600 text-brand-700 dark:text-brand-300'
                : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700 dark:text-white/50 dark:hover:text-white'
            }`}
          >
            {t(`roleDetail.tabs.${tab}`)}
          </button>
        ))}
      </div>

      {saveSuccessNotification && (
        <div className="flex items-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm font-medium text-emerald-800 dark:border-emerald-500/20 dark:bg-emerald-500/10 dark:text-emerald-300">
          <CheckCircle2 className="h-4 w-4 shrink-0 text-emerald-600 dark:text-emerald-400" />
          <span>{saveSuccessNotification}</span>
        </div>
      )}

      {mutationError && (
        <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
          {mutationError instanceof Error ? mutationError.message : t('common.unexpectedError')}
        </div>
      )}

      {generateMutation.error && (
        <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
          {generateMutation.error instanceof Error ? generateMutation.error.message : t('roleDetail.generateError')}
        </div>
      )}

      {reviewRows && (
        <div className="space-y-4 rounded-2xl border border-amber-200 bg-amber-50/60 p-5 dark:border-amber-500/20 dark:bg-amber-500/10">
          <div className="flex items-start justify-between gap-3">
            <div>
              <h2 className="flex items-center gap-2 text-sm font-semibold text-amber-900 dark:text-amber-200">
                <Sparkles className="h-4 w-4" />
                {t('roleDetail.aiProposalTitle')}
              </h2>
              <p className="mt-1 text-xs text-amber-800/70 dark:text-amber-200/70">
                {t('roleDetail.aiProposalSubtitle')}
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
              {t('roleDetail.aiProposalEmpty')}
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
                          {row.key === 'requiresTravel' ? (
                            <select
                              value={String(currentValue)}
                              onChange={(e) => updateEditedValue(row.key, e.target.value === 'true')}
                              className="rounded-lg border border-green-200 bg-green-50 px-2.5 py-1.5 text-xs text-green-800 outline-none focus:border-green-400 dark:border-green-500/20 dark:bg-green-500/10 dark:text-green-300"
                            >
                              <option value="true">{t('common.yes')}</option>
                              <option value="false">{t('common.no')}</option>
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

      {activeTab === 'editJobRole' && (
        <form onSubmit={handleSubmit} className="space-y-4">
        <Section number={1} title={t('roleDetail.section1.title')}>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass}>{t('roleDetail.section1.jobTitle')}</label>
              <input
                required
                value={form.title}
                onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
                placeholder={t('roleDetail.section1.jobTitlePlaceholder')}
                className={inputClass}
              />
            </div>
            <div>
              <label className={labelClass}>{t('roleDetail.section1.seniority')}</label>
              <select
                value={form.levelLabel}
                onChange={(e) => setForm((f) => ({ ...f, levelLabel: e.target.value }))}
                className={inputClass}
              >
                <option value="">{t('roleDetail.section1.noLevel')}</option>
                {SENIORITY_LEVEL_KEYS.map((key) => {
                  const label = t(`roleDetail.section1.levels.${key}`);
                  return (
                    <option key={key} value={label}>
                      {label}
                    </option>
                  );
                })}
                {form.levelLabel &&
                  !SENIORITY_LEVEL_KEYS.some((k) => t(`roleDetail.section1.levels.${k}`) === form.levelLabel) && (
                    <option value={form.levelLabel}>{form.levelLabel}</option>
                  )}
              </select>
            </div>
          </div>
        </Section>

        <Section number={2} title={t('roleDetail.section2.title')} description={t('roleDetail.section2.description')}>
          <label className={labelClass}>{t('roleDetail.section2.summary')}</label>
          <textarea
            value={form.summary}
            onChange={(e) => setForm((f) => ({ ...f, summary: e.target.value }))}
            rows={3}
            className={inputClass}
          />
          <label className={`${labelClass} mt-4`}>{t('roleDetail.section2.purpose')}</label>
          <textarea
            value={form.purpose}
            onChange={(e) => setForm((f) => ({ ...f, purpose: e.target.value }))}
            rows={2}
            className={inputClass}
          />
        </Section>

        <Section number={3} title={t('roleDetail.section3.title')} description={t('roleDetail.section3.description')}>
          <div className="flex items-center justify-end -mt-2 mb-2">
            <Link
              to="/responsibilities"
              target="_blank"
              rel="noreferrer"
              className="flex items-center gap-1 text-xs font-medium text-blue-600 hover:text-blue-700 dark:text-blue-400 dark:hover:text-blue-300"
            >
              <span>{t('roleDetail.section3.viewCatalog')}</span>
              <ExternalLink className="h-3 w-3" />
            </Link>
          </div>

          <select
            className={inputClass}
            value=""
            onChange={(e) => {
              if (e.target.value) {
                addResponsibility(e.target.value);
              }
            }}
          >
            <option value="">
              {responsibilitiesCatalogQuery.isLoading
                ? t('common.loading')
                : t('roleDetail.section3.selectFromCatalog')}
            </option>
            {(responsibilitiesCatalogQuery.data ?? [])
              .filter((r) => !responsibilitiesList.some((rl) => normalizeItemName(rl) === normalizeItemName(r.name)))
              .map((r) => (
                <option key={r.responsibilityId} value={r.name}>
                  {r.name} {r.description ? `— ${r.description}` : ''}
                </option>
              ))}
          </select>

          {responsibilitiesList.length > 0 && (
            <div className="mt-2.5 flex flex-wrap gap-1.5">
              {responsibilitiesList.map((respName) => {
                const isInCatalog = (responsibilitiesCatalogQuery.data ?? []).some(
                  (r) => normalizeItemName(r.name) === normalizeItemName(respName),
                );
                return (
                  <span
                    key={respName}
                    className={`inline-flex items-center gap-1.5 rounded-lg px-2.5 py-1 text-xs font-medium ${
                      isInCatalog
                        ? 'bg-blue-100 text-blue-800 dark:bg-blue-500/20 dark:text-blue-300'
                        : 'border border-dashed border-amber-300 bg-amber-50 text-amber-900 dark:border-amber-500/40 dark:bg-amber-500/10 dark:text-amber-200'
                    }`}
                  >
                    <ListChecks className="h-3.5 w-3.5 text-blue-600 dark:text-blue-400" />
                    <span>{respName}</span>
                    {!isInCatalog && (
                      <span className="rounded bg-amber-200/80 px-1 py-0.5 text-[9px] font-bold uppercase tracking-wider text-amber-900 dark:bg-amber-400/30 dark:text-amber-200">
                        {t('common.newBadge')}
                      </span>
                    )}
                    <button
                      type="button"
                      onClick={() => removeResponsibility(respName)}
                      className="text-slate-400 hover:text-slate-700 dark:hover:text-white"
                      title={t('common.remove')}
                    >
                      <X className="h-3 w-3" />
                    </button>
                  </span>
                );
              })}
            </div>
          )}
        </Section>

        <Section
          number={4}
          title={t('roleDetail.section5.title')}
          description={t('roleDetail.section5.description')}
        >
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass}>{t('roleDetail.section5.education')}</label>
              <select
                value={form.requiredEducation}
                onChange={(e) => setForm((f) => ({ ...f, requiredEducation: e.target.value }))}
                className={inputClass}
              >
                <option value="">{t('roleDetail.section5.selectEducation')}</option>
                {EDUCATION_LEVEL_KEYS.map((key) => {
                  const label = t(`roleDetail.section5.educationLevels.${key}`);
                  return (
                    <option key={key} value={label}>
                      {label}
                    </option>
                  );
                })}
                {form.requiredEducation &&
                  !EDUCATION_LEVEL_KEYS.some((k) => t(`roleDetail.section5.educationLevels.${k}`) === form.requiredEducation) && (
                    <option value={form.requiredEducation}>{form.requiredEducation}</option>
                  )}
              </select>
            </div>
            <div>
              <label className={labelClass}>{t('roleDetail.section5.experience')}</label>
              <input
                value={form.requiredExperience}
                onChange={(e) => setForm((f) => ({ ...f, requiredExperience: e.target.value }))}
                placeholder={t('roleDetail.section5.experiencePlaceholder')}
                className={inputClass}
              />
            </div>
          </div>
          <div className="mt-4">
            <div className="flex items-center justify-between">
              <label className={labelClass}>{t('roleDetail.section5.technicalSkills')}</label>
              <Link
                to="/skills/job-skills"
                target="_blank"
                rel="noreferrer"
                className="flex items-center gap-1 text-xs font-medium text-amber-600 hover:text-amber-700 dark:text-amber-400 dark:hover:text-amber-300"
              >
                <span>{t('roleDetail.section5.viewCatalog')}</span>
                <ExternalLink className="h-3 w-3" />
              </Link>
            </div>

            {/* Catalog Selector combobox */}
            <div className="mt-2">
              <select
                className={inputClass}
                value=""
                onChange={(e) => {
                  if (e.target.value) {
                    addTechnicalSkill(e.target.value);
                  }
                }}
              >
                <option value="">
                  {technicalSkillsCatalogQuery.isLoading
                    ? t('common.loading')
                    : t('roleDetail.section5.selectFromCatalog')}
                </option>
                {(technicalSkillsCatalogQuery.data ?? [])
                  .filter((s) => !technicalSkillsList.some((ts) => ts.toLowerCase() === s.name.toLowerCase()))
                  .map((s) => (
                    <option key={s.skillId} value={s.name}>
                      {s.name} {s.description ? `— ${s.description}` : ''}
                    </option>
                  ))}
              </select>
            </div>

            {/* Selected Skills Chips */}
            {technicalSkillsList.length > 0 && (
              <div className="mt-2.5 flex flex-wrap gap-1.5">
                {technicalSkillsList.map((skillName) => {
                  const isInCatalog = (technicalSkillsCatalogQuery.data ?? []).some(
                    (s) => s.name.toLowerCase() === skillName.toLowerCase(),
                  );
                  return (
                    <span
                      key={skillName}
                      className={`inline-flex items-center gap-1.5 rounded-lg px-2.5 py-1 text-xs font-medium ${
                        isInCatalog
                          ? 'bg-amber-100 text-amber-800 dark:bg-amber-500/20 dark:text-amber-300'
                          : 'border border-dashed border-amber-300 bg-amber-50 text-amber-900 dark:border-amber-500/40 dark:bg-amber-500/10 dark:text-amber-200'
                      }`}
                    >
                      <GraduationCap className="h-3.5 w-3.5 text-amber-600 dark:text-amber-400" />
                      <span>{skillName}</span>
                      {!isInCatalog && (
                        <span className="rounded bg-amber-200/80 px-1 py-0.5 text-[9px] font-bold uppercase tracking-wider text-amber-900 dark:bg-amber-400/30 dark:text-amber-200">
                          {t('common.newBadge')}
                        </span>
                      )}
                      <button
                        type="button"
                        onClick={() => removeTechnicalSkill(skillName)}
                        className="text-slate-400 hover:text-slate-700 dark:hover:text-white"
                        title={t('common.remove')}
                      >
                        <X className="h-3 w-3" />
                      </button>
                    </span>
                  );
                })}
              </div>
            )}
          </div>
          <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <div className="flex items-center justify-between">
                <label className={labelClass}>{t('roleDetail.section5.certifications')}</label>
                <Link
                  to="/certifications"
                  target="_blank"
                  rel="noreferrer"
                  className="flex items-center gap-1 text-xs font-medium text-amber-600 hover:text-amber-700 dark:text-amber-400 dark:hover:text-amber-300"
                >
                  <span>{t('roleDetail.section5.viewCertificationsCatalog')}</span>
                  <ExternalLink className="h-3 w-3" />
                </Link>
              </div>

              <select
                className={inputClass}
                value=""
                onChange={(e) => {
                  if (e.target.value) {
                    addCertification(e.target.value);
                  }
                }}
              >
                <option value="">
                  {certificationsCatalogQuery.isLoading
                    ? t('common.loading')
                    : t('roleDetail.section5.selectCertification')}
                </option>
                {(certificationsCatalogQuery.data ?? [])
                  .filter((c) => !certificationsList.some((cl) => cl.toLowerCase() === c.name.toLowerCase()))
                  .map((c) => (
                    <option key={c.certificationId} value={c.name}>
                      {c.name} {c.issuer ? `(${c.issuer})` : ''}
                    </option>
                  ))}
              </select>

              {certificationsList.length > 0 && (
                <div className="mt-2.5 flex flex-wrap gap-1.5">
                  {certificationsList.map((certName) => {
                    const isInCatalog = (certificationsCatalogQuery.data ?? []).some(
                      (c) => c.name.toLowerCase() === certName.toLowerCase(),
                    );
                    return (
                      <span
                        key={certName}
                        className={`inline-flex items-center gap-1.5 rounded-lg px-2.5 py-1 text-xs font-medium ${
                          isInCatalog
                            ? 'bg-amber-100 text-amber-800 dark:bg-amber-500/20 dark:text-amber-300'
                            : 'border border-dashed border-amber-300 bg-amber-50 text-amber-900 dark:border-amber-500/40 dark:bg-amber-500/10 dark:text-amber-200'
                        }`}
                      >
                        <Award className="h-3.5 w-3.5 text-amber-600 dark:text-amber-400" />
                        <span>{certName}</span>
                        {!isInCatalog && (
                          <span className="rounded bg-amber-200/80 px-1 py-0.5 text-[9px] font-bold uppercase tracking-wider text-amber-900 dark:bg-amber-400/30 dark:text-amber-200">
                            {t('common.newBadge')}
                          </span>
                        )}
                        <button
                          type="button"
                          onClick={() => removeCertification(certName)}
                          className="text-slate-400 hover:text-slate-700 dark:hover:text-white"
                          title={t('common.remove')}
                        >
                          <X className="h-3 w-3" />
                        </button>
                      </span>
                    );
                  })}
                </div>
              )}
            </div>
            <div>
              <label className={labelClass}>{t('roleDetail.section5.languages')}</label>
              <select
                className={inputClass}
                value=""
                onChange={(e) => {
                  if (e.target.value) {
                    addLanguage(e.target.value);
                  }
                }}
              >
                <option value="">{t('roleDetail.section5.selectLanguage')}</option>
                {COMMON_LANGUAGE_KEYS.filter(
                  (k) =>
                    !languagesList.some(
                      (l) => l.toLowerCase() === t(`roleDetail.section5.languagesList.${k}`).toLowerCase(),
                    ),
                ).map((key) => {
                  const label = t(`roleDetail.section5.languagesList.${key}`);
                  return (
                    <option key={key} value={label}>
                      {label}
                    </option>
                  );
                })}
              </select>

              {languagesList.length > 0 && (
                <div className="mt-2.5 flex flex-wrap gap-1.5">
                  {languagesList.map((langName) => (
                    <span
                      key={langName}
                      className="inline-flex items-center gap-1.5 rounded-lg bg-sky-100 px-2.5 py-1 text-xs font-medium text-sky-800 dark:bg-sky-500/20 dark:text-sky-300"
                    >
                      <Globe className="h-3.5 w-3.5 text-sky-600 dark:text-sky-400" />
                      <span>{langName}</span>
                      <button
                        type="button"
                        onClick={() => removeLanguage(langName)}
                        className="text-slate-400 hover:text-slate-700 dark:hover:text-white"
                        title={t('common.remove')}
                      >
                        <X className="h-3 w-3" />
                      </button>
                    </span>
                  ))}
                </div>
              )}
            </div>
          </div>
        </Section>

        <Section number={5} title={t('roleDetail.section6.title')} description={t('roleDetail.section6.description')}>
          <div className="flex items-center justify-end -mt-2 mb-2">
            <Link
              to="/skills/soft-skills"
              target="_blank"
              rel="noreferrer"
              className="flex items-center gap-1 text-xs font-medium text-violet-600 hover:text-violet-700 dark:text-violet-400 dark:hover:text-violet-300"
            >
              <span>{t('roleDetail.section6.viewCatalog')}</span>
              <ExternalLink className="h-3 w-3" />
            </Link>
          </div>

          <select
            className={inputClass}
            value=""
            onChange={(e) => {
              if (e.target.value) {
                addSoftSkill(e.target.value);
              }
            }}
          >
            <option value="">
              {softSkillsCatalogQuery.isLoading
                ? t('common.loading')
                : t('roleDetail.section6.selectFromCatalog')}
            </option>
            {(softSkillsCatalogQuery.data ?? [])
              .filter((s) => !softSkillsList.some((ss) => ss.toLowerCase() === s.name.toLowerCase()))
              .map((s) => (
                <option key={s.skillId} value={s.name}>
                  {s.name} {s.description ? `— ${s.description}` : ''}
                </option>
              ))}
          </select>

          {softSkillsList.length > 0 && (
            <div className="mt-2.5 flex flex-wrap gap-1.5">
              {softSkillsList.map((skillName) => {
                const isInCatalog = (softSkillsCatalogQuery.data ?? []).some(
                  (s) => s.name.toLowerCase() === skillName.toLowerCase(),
                );
                return (
                  <span
                    key={skillName}
                    className={`inline-flex items-center gap-1.5 rounded-lg px-2.5 py-1 text-xs font-medium ${
                      isInCatalog
                        ? 'bg-violet-100 text-violet-800 dark:bg-violet-500/20 dark:text-violet-300'
                        : 'border border-dashed border-amber-300 bg-amber-50 text-amber-900 dark:border-amber-500/40 dark:bg-amber-500/10 dark:text-amber-200'
                    }`}
                  >
                    <Heart className="h-3.5 w-3.5 text-violet-600 dark:text-violet-400" />
                    <span>{skillName}</span>
                    {!isInCatalog && (
                      <span className="rounded bg-amber-200/80 px-1 py-0.5 text-[9px] font-bold uppercase tracking-wider text-amber-900 dark:bg-amber-400/30 dark:text-amber-200">
                        {t('common.newBadge')}
                      </span>
                    )}
                    <button
                      type="button"
                      onClick={() => removeSoftSkill(skillName)}
                      className="text-slate-400 hover:text-slate-700 dark:hover:text-white"
                      title={t('common.remove')}
                    >
                      <X className="h-3 w-3" />
                    </button>
                  </span>
                );
              })}
            </div>
          )}
        </Section>

        <Section number={6} title={t('roleDetail.section7.title')} description={t('roleDetail.section7.description')}>
          <select
            className={inputClass}
            value=""
            onChange={(e) => {
              if (e.target.value) {
                addTool(e.target.value);
              }
            }}
          >
            <option value="">
              {toolsCatalogQuery.isLoading
                ? t('common.loading')
                : t('roleDetail.section7.selectTool')}
            </option>
            {(toolsCatalogQuery.data ?? [])
              .filter((tool) => !toolsList.some((t) => t.toLowerCase() === tool.name.toLowerCase()))
              .map((tool) => (
                <option key={tool.toolId} value={tool.name}>
                  {tool.name} {tool.description ? `— ${tool.description}` : ''}
                </option>
              ))}
          </select>

          {toolsList.length > 0 && (
            <div className="mt-2.5 flex flex-wrap gap-1.5">
              {toolsList.map((toolName) => {
                const isInCatalog = (toolsCatalogQuery.data ?? []).some(
                  (t) => t.name.toLowerCase() === toolName.toLowerCase(),
                );
                return (
                  <span
                    key={toolName}
                    className={`inline-flex items-center gap-1.5 rounded-lg px-2.5 py-1 text-xs font-medium ${
                      isInCatalog
                        ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300'
                        : 'border border-dashed border-amber-300 bg-amber-50 text-amber-900 dark:border-amber-500/40 dark:bg-amber-500/10 dark:text-amber-200'
                    }`}
                  >
                    <Wrench className="h-3.5 w-3.5 text-emerald-600 dark:text-emerald-400" />
                    <span>{toolName}</span>
                    {!isInCatalog && (
                      <span className="rounded bg-amber-200/80 px-1 py-0.5 text-[9px] font-bold uppercase tracking-wider text-amber-900 dark:bg-amber-400/30 dark:text-amber-200">
                        {t('common.newBadge')}
                      </span>
                    )}
                    <button
                      type="button"
                      onClick={() => removeTool(toolName)}
                      className="text-slate-400 hover:text-slate-700 dark:hover:text-white"
                      title={t('common.remove')}
                    >
                      <X className="h-3 w-3" />
                    </button>
                  </span>
                );
              })}
            </div>
          )}
        </Section>

        <Section
          number={7}
          title={t('roleDetail.section8.title')}
          description={t('roleDetail.section8.description')}
        >
          <label className={labelClass}>{t('roleDetail.section8.reportsTo')}</label>
          <select
            value={form.reportsToJobRoleId}
            onChange={(e) => setForm((f) => ({ ...f, reportsToJobRoleId: e.target.value }))}
            className={inputClass}
          >
            <option value="">{t('roleDetail.section8.noDirectBoss')}</option>
            {otherJobRoles.map((r) => (
              <option key={r.jobRoleId} value={r.jobRoleId}>
                {r.title}
              </option>
            ))}
          </select>
          {isEditing && roleQuery.data && (
            <p className="mt-2 text-xs text-slate-400 dark:text-white/40">
              {roleQuery.data.hasDirectReports
                ? t('roleDetail.section8.hasDirectReportsInfo')
                : t('roleDetail.section8.noDirectReportsInfo')}
            </p>
          )}
        </Section>

        <Section number={8} title={t('roleDetail.section9.title')} description={t('roleDetail.section9.description')}>
          <textarea
            value={form.performanceIndicators}
            onChange={(e) => setForm((f) => ({ ...f, performanceIndicators: e.target.value }))}
            rows={3}
            placeholder={t('roleDetail.section9.placeholder')}
            className={inputClass}
          />
        </Section>

        <Section
          number={9}
          title={t('roleDetail.section10.title')}
          description={t('roleDetail.section10.description')}
        >
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass}>{t('roleDetail.section10.schedule')}</label>
              <input
                value={form.workSchedule}
                onChange={(e) => setForm((f) => ({ ...f, workSchedule: e.target.value }))}
                placeholder={t('roleDetail.section10.schedulePlaceholder')}
                className={inputClass}
              />
            </div>
            <div>
              <label className={labelClass}>{t('roleDetail.section10.modality')}</label>
              <input
                value={form.workModality}
                onChange={(e) => setForm((f) => ({ ...f, workModality: e.target.value }))}
                placeholder={t('roleDetail.section10.modalityPlaceholder')}
                className={inputClass}
              />
            </div>
          </div>
          <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass}>{t('roleDetail.section10.location')}</label>
              <input
                value={form.workLocation}
                onChange={(e) => setForm((f) => ({ ...f, workLocation: e.target.value }))}
                placeholder={t('roleDetail.section10.locationPlaceholder')}
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
                {t('roleDetail.section10.requiresTravel')}
              </label>
            </div>
          </div>
          <div className="mt-4">
            <label className={labelClass}>{t('roleDetail.section10.availability')}</label>
            <input
              value={form.availabilityNotes}
              onChange={(e) => setForm((f) => ({ ...f, availabilityNotes: e.target.value }))}
              placeholder={t('roleDetail.section10.availabilityPlaceholder')}
              className={inputClass}
            />
          </div>
        </Section>

        <Section number={10} title={t('roleDetail.section11.title')}>
          <select
            value={form.employmentType}
            onChange={(e) => setForm((f) => ({ ...f, employmentType: e.target.value }))}
            className={inputClass}
          >
            <option value="">{t('roleDetail.section11.selectType')}</option>
            {EMPLOYMENT_TYPE_KEYS.map((key) => {
              const label = t(`roleDetail.section11.types.${key}`);
              return (
                <option key={key} value={label}>
                  {label}
                </option>
              );
            })}
            {form.employmentType &&
              !EMPLOYMENT_TYPE_KEYS.some((k) => t(`roleDetail.section11.types.${k}`) === form.employmentType) && (
                <option value={form.employmentType}>{form.employmentType}</option>
              )}
          </select>
        </Section>

        <Section number={11} title={t('roleDetail.section12.title')}>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <div>
              <label className={labelClass}>{t('roleDetail.section12.salaryMin')}</label>
              <input
                type="number"
                value={form.salaryRangeMin}
                onChange={(e) => setForm((f) => ({ ...f, salaryRangeMin: e.target.value }))}
                className={inputClass}
              />
            </div>
            <div>
              <label className={labelClass}>{t('roleDetail.section12.salaryMax')}</label>
              <input
                type="number"
                value={form.salaryRangeMax}
                onChange={(e) => setForm((f) => ({ ...f, salaryRangeMax: e.target.value }))}
                className={inputClass}
              />
            </div>
            <div>
              <label className={labelClass}>{t('roleDetail.section12.currency')}</label>
              <input
                value={form.salaryCurrency}
                onChange={(e) => setForm((f) => ({ ...f, salaryCurrency: e.target.value }))}
                placeholder={t('roleDetail.section12.currencyPlaceholder')}
                className={inputClass}
              />
            </div>
          </div>
          <div className="mt-4">
            <label className={labelClass}>{t('roleDetail.section12.benefits')}</label>
            <textarea
              value={form.benefits}
              onChange={(e) => setForm((f) => ({ ...f, benefits: e.target.value }))}
              rows={3}
              placeholder={t('roleDetail.section12.benefitsPlaceholder')}
              className={inputClass}
            />
          </div>
        </Section>

        <div className="flex justify-end gap-2 pt-2">
          <button
            type="button"
            onClick={handleGoBack}
            className="rounded-full px-4 py-2 text-sm font-medium text-slate-500 hover:bg-slate-100 dark:text-white/50 dark:hover:bg-white/10"
          >
            {t('common.cancel')}
          </button>
          <button
            type="submit"
            disabled={isSaving}
            className="rounded-full bg-brand-600 px-6 py-2 text-sm font-medium text-white transition hover:bg-brand-700 disabled:opacity-60"
          >
            {isSaving ? t('common.saving') : t('roleDetail.saveRole')}
          </button>
        </div>
      </form>
      )}

      {activeTab === 'startInYourRole' && (
        <div className="space-y-6">
          <section className="overflow-hidden rounded-3xl bg-gradient-to-br from-sky-600 via-blue-600 to-indigo-700 p-6 text-white shadow-xl shadow-blue-900/10">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div className="max-w-2xl">
                <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-blue-100">
                  <span className="flex h-5 w-5 items-center justify-center rounded-full bg-white/20 text-[10px] font-bold">01</span>
                  <span>{i18n.language === 'es' ? 'Primeros 90 días' : 'First 90 days'}</span>
                  <span className="rounded-full bg-emerald-400/20 px-2 py-0.5 text-[10px] font-bold text-emerald-200">
                    {i18n.language === 'es' ? 'Estás aquí' : 'You are here'}
                  </span>
                </div>
                <h2 className="mt-3 text-2xl font-bold sm:text-3xl">
                  {i18n.language === 'es' ? 'Comienza en tu rol' : 'Start in Your Role'}
                </h2>
                <p className="mt-2 text-sm leading-relaxed text-blue-50 sm:text-base">
                  {i18n.language === 'es'
                    ? 'Conoce la empresa, completa tu capacitación esencial y comienza a desarrollar los conocimientos que requiere tu puesto.'
                    : 'Get to know the company, complete your essential training, and begin developing the knowledge required for your role.'}
                </p>
              </div>
              <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-white/15 ring-1 ring-white/25 backdrop-blur">
                <Rocket className="h-7 w-7" />
              </div>
            </div>
          </section>

          {/* Selector interactivo: Izquierda catálogo de capacidades del puesto / Derecha selección Day One */}
          {(() => {
            const techList = linesToArray(form.technicalSkills);
            const softList = linesToArray(form.softSkills);
            const deptProcList = linesToArray(form.departmentProcesses);
            const hrProcList = linesToArray(form.hrProcesses);

            const selTech = new Set(linesToArray(form.startInYourRoleTechnicalSkills));
            const selSoft = new Set(linesToArray(form.startInYourRoleSoftSkills));
            const selDept = new Set(linesToArray(form.startInYourRoleDepartmentProcesses));
            const selHr = new Set(linesToArray(form.startInYourRoleHrProcesses));

            const totalAvailable = techList.length + softList.length + deptProcList.length + hrProcList.length;
            const totalSelected = selTech.size + selSoft.size + selDept.size + selHr.size;

            const toggleItem = (category: 'tech' | 'soft' | 'dept' | 'hr', item: string) => {
              if (category === 'tech') {
                const next = new Set(selTech);
                next.has(item) ? next.delete(item) : next.add(item);
                setForm((f) => ({ ...f, startInYourRoleTechnicalSkills: arrayToLines(Array.from(next)) }));
              } else if (category === 'soft') {
                const next = new Set(selSoft);
                next.has(item) ? next.delete(item) : next.add(item);
                setForm((f) => ({ ...f, startInYourRoleSoftSkills: arrayToLines(Array.from(next)) }));
              } else if (category === 'dept') {
                const next = new Set(selDept);
                next.has(item) ? next.delete(item) : next.add(item);
                setForm((f) => ({ ...f, startInYourRoleDepartmentProcesses: arrayToLines(Array.from(next)) }));
              } else if (category === 'hr') {
                const next = new Set(selHr);
                next.has(item) ? next.delete(item) : next.add(item);
                setForm((f) => ({ ...f, startInYourRoleHrProcesses: arrayToLines(Array.from(next)) }));
              }
            };

            const selectAllCategory = (category: 'tech' | 'soft' | 'dept' | 'hr') => {
              if (category === 'tech') {
                const next = new Set([...selTech, ...techList]);
                setForm((f) => ({ ...f, startInYourRoleTechnicalSkills: arrayToLines(Array.from(next)) }));
              } else if (category === 'soft') {
                const next = new Set([...selSoft, ...softList]);
                setForm((f) => ({ ...f, startInYourRoleSoftSkills: arrayToLines(Array.from(next)) }));
              } else if (category === 'dept') {
                const next = new Set([...selDept, ...deptProcList]);
                setForm((f) => ({ ...f, startInYourRoleDepartmentProcesses: arrayToLines(Array.from(next)) }));
              } else if (category === 'hr') {
                const next = new Set([...selHr, ...hrProcList]);
                setForm((f) => ({ ...f, startInYourRoleHrProcesses: arrayToLines(Array.from(next)) }));
              }
            };

            const deselectAllCategory = (category: 'tech' | 'soft' | 'dept' | 'hr') => {
              if (category === 'tech') {
                setForm((f) => ({ ...f, startInYourRoleTechnicalSkills: '' }));
              } else if (category === 'soft') {
                setForm((f) => ({ ...f, startInYourRoleSoftSkills: '' }));
              } else if (category === 'dept') {
                setForm((f) => ({ ...f, startInYourRoleDepartmentProcesses: '' }));
              } else if (category === 'hr') {
                setForm((f) => ({ ...f, startInYourRoleHrProcesses: '' }));
              }
            };

            return (
              <div className="space-y-4">
                <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-white/5">
                  <div>
                    <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                      {i18n.language === 'es' ? 'Plan de Arranque (Día Uno / Primeros 90 Días)' : 'Day One / First 90 Days Training Plan'}
                    </h3>
                    <p className="text-xs text-slate-500 dark:text-white/60">
                      {i18n.language === 'es'
                        ? 'Selecciona del lado izquierdo los cursos y capacidades prioritarios que el nuevo colaborador debe desarrollar al iniciar en su rol.'
                        : 'Select the priority capabilities and courses from the left that new hires must complete upon starting in this role.'}
                    </p>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="rounded-full bg-blue-50 px-3 py-1 text-xs font-semibold text-blue-700 dark:bg-blue-500/10 dark:text-blue-300">
                      {totalSelected} / {totalAvailable} {i18n.language === 'es' ? 'seleccionados' : 'selected'}
                    </span>
                    <button
                      type="button"
                      onClick={(e) => handleSubmit(e as unknown as FormEvent)}
                      disabled={isSaving}
                      className="rounded-full bg-brand-600 px-5 py-2 text-xs font-semibold text-white transition hover:bg-brand-700 disabled:opacity-60"
                    >
                      {isSaving ? t('common.saving') : (i18n.language === 'es' ? 'Guardar Plan' : 'Save Plan')}
                    </button>
                  </div>
                </div>

                <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
                  {/* Columna Izquierda: Catálogo Disponible del Puesto */}
                  <div className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5">
                    <div className="flex items-center justify-between border-b border-slate-100 pb-3 dark:border-white/5">
                      <div>
                        <h4 className="text-xs font-bold uppercase tracking-wider text-slate-900 dark:text-white">
                          {i18n.language === 'es' ? '1. Capacidades y Procesos del Puesto' : '1. Role Capabilities & Processes'}
                        </h4>
                        <p className="text-[11px] text-slate-400 dark:text-white/40">
                          {i18n.language === 'es' ? 'Haz clic en las casillas para agregar o quitar del plan' : 'Click checkboxes to add or remove from day one'}
                        </p>
                      </div>
                      <span className="text-xs text-slate-400">{totalAvailable} {i18n.language === 'es' ? 'disponibles' : 'available'}</span>
                    </div>

                    {totalAvailable === 0 ? (
                      <div className="rounded-xl border border-dashed border-slate-200 p-6 text-center text-xs text-slate-400 dark:border-white/10">
                        {i18n.language === 'es'
                          ? 'Este rol aún no tiene habilidades o procesos capturados. Agrégalos en el tab "Editar Puesto".'
                          : 'This role has no technical/soft skills or processes configured yet. Add them in the "Edit Job Role" tab.'}
                      </div>
                    ) : (
                      <div className="space-y-5">
                        {/* Técnicas */}
                        {techList.length > 0 && (
                          <div className="space-y-2">
                            <div className="flex items-center justify-between text-xs font-semibold text-sky-700 dark:text-sky-300">
                              <span className="flex items-center gap-1.5">
                                <Wrench className="h-3.5 w-3.5" />
                                {i18n.language === 'es' ? 'Habilidades Técnicas' : 'Technical Skills'} ({techList.length})
                              </span>
                              <div className="flex items-center gap-2 text-[11px] font-normal">
                                <button type="button" onClick={() => selectAllCategory('tech')} className="text-blue-600 hover:underline dark:text-blue-400">
                                  {i18n.language === 'es' ? 'Todas' : 'All'}
                                </button>
                                <span>·</span>
                                <button type="button" onClick={() => deselectAllCategory('tech')} className="text-slate-400 hover:underline">
                                  {i18n.language === 'es' ? 'Ninguna' : 'None'}
                                </button>
                              </div>
                            </div>
                            <div className="space-y-1">
                              {techList.map((skill) => {
                                const checked = selTech.has(skill);
                                return (
                                  <label
                                    key={skill}
                                    className={`flex cursor-pointer items-center justify-between gap-2 rounded-lg border p-2 text-xs transition ${
                                      checked
                                        ? 'border-sky-300 bg-sky-50/70 text-sky-950 font-medium dark:border-sky-500/30 dark:bg-sky-500/10 dark:text-sky-200'
                                        : 'border-slate-100 bg-slate-50/50 text-slate-700 hover:bg-slate-100/60 dark:border-white/5 dark:bg-white/[0.02] dark:text-white/70'
                                    }`}
                                  >
                                    <div className="flex items-center gap-2">
                                      <input
                                        type="checkbox"
                                        checked={checked}
                                        onChange={() => toggleItem('tech', skill)}
                                        className="h-3.5 w-3.5 rounded border-slate-300 text-brand-600"
                                      />
                                      <span>{skill}</span>
                                    </div>
                                    <span className="rounded bg-sky-100 px-1.5 py-0.5 text-[10px] font-semibold text-sky-700 dark:bg-sky-500/20 dark:text-sky-300">
                                      {i18n.language === 'es' ? 'Técnica' : 'Technical'}
                                    </span>
                                  </label>
                                );
                              })}
                            </div>
                          </div>
                        )}

                        {/* Blandas / No técnicas */}
                        {softList.length > 0 && (
                          <div className="space-y-2">
                            <div className="flex items-center justify-between text-xs font-semibold text-violet-700 dark:text-violet-300">
                              <span className="flex items-center gap-1.5">
                                <Heart className="h-3.5 w-3.5" />
                                {i18n.language === 'es' ? 'Habilidades No Técnicas / Blandas' : 'Soft / Non-Technical Skills'} ({softList.length})
                              </span>
                              <div className="flex items-center gap-2 text-[11px] font-normal">
                                <button type="button" onClick={() => selectAllCategory('soft')} className="text-blue-600 hover:underline dark:text-blue-400">
                                  {i18n.language === 'es' ? 'Todas' : 'All'}
                                </button>
                                <span>·</span>
                                <button type="button" onClick={() => deselectAllCategory('soft')} className="text-slate-400 hover:underline">
                                  {i18n.language === 'es' ? 'Ninguna' : 'None'}
                                </button>
                              </div>
                            </div>
                            <div className="space-y-1">
                              {softList.map((skill) => {
                                const checked = selSoft.has(skill);
                                return (
                                  <label
                                    key={skill}
                                    className={`flex cursor-pointer items-center justify-between gap-2 rounded-lg border p-2 text-xs transition ${
                                      checked
                                        ? 'border-violet-300 bg-violet-50/70 text-violet-950 font-medium dark:border-violet-500/30 dark:bg-violet-500/10 dark:text-violet-200'
                                        : 'border-slate-100 bg-slate-50/50 text-slate-700 hover:bg-slate-100/60 dark:border-white/5 dark:bg-white/[0.02] dark:text-white/70'
                                    }`}
                                  >
                                    <div className="flex items-center gap-2">
                                      <input
                                        type="checkbox"
                                        checked={checked}
                                        onChange={() => toggleItem('soft', skill)}
                                        className="h-3.5 w-3.5 rounded border-slate-300 text-brand-600"
                                      />
                                      <span>{skill}</span>
                                    </div>
                                    <span className="rounded bg-violet-100 px-1.5 py-0.5 text-[10px] font-semibold text-violet-700 dark:bg-violet-500/20 dark:text-violet-300">
                                      {i18n.language === 'es' ? 'Blanda' : 'Soft'}
                                    </span>
                                  </label>
                                );
                              })}
                            </div>
                          </div>
                        )}

                        {/* Procesos de Depto */}
                        {deptProcList.length > 0 && (
                          <div className="space-y-2">
                            <div className="flex items-center justify-between text-xs font-semibold text-amber-700 dark:text-amber-300">
                              <span className="flex items-center gap-1.5">
                                <Briefcase className="h-3.5 w-3.5" />
                                {i18n.language === 'es' ? 'Procesos del Puesto / Departamento' : 'Department & Role Processes'} ({deptProcList.length})
                              </span>
                              <div className="flex items-center gap-2 text-[11px] font-normal">
                                <button type="button" onClick={() => selectAllCategory('dept')} className="text-blue-600 hover:underline dark:text-blue-400">
                                  {i18n.language === 'es' ? 'Todos' : 'All'}
                                </button>
                                <span>·</span>
                                <button type="button" onClick={() => deselectAllCategory('dept')} className="text-slate-400 hover:underline">
                                  {i18n.language === 'es' ? 'Ninguno' : 'None'}
                                </button>
                              </div>
                            </div>
                            <div className="space-y-1">
                              {deptProcList.map((proc) => {
                                const checked = selDept.has(proc);
                                return (
                                  <label
                                    key={proc}
                                    className={`flex cursor-pointer items-center justify-between gap-2 rounded-lg border p-2 text-xs transition ${
                                      checked
                                        ? 'border-amber-300 bg-amber-50/70 text-amber-950 font-medium dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-200'
                                        : 'border-slate-100 bg-slate-50/50 text-slate-700 hover:bg-slate-100/60 dark:border-white/5 dark:bg-white/[0.02] dark:text-white/70'
                                    }`}
                                  >
                                    <div className="flex items-center gap-2">
                                      <input
                                        type="checkbox"
                                        checked={checked}
                                        onChange={() => toggleItem('dept', proc)}
                                        className="h-3.5 w-3.5 rounded border-slate-300 text-brand-600"
                                      />
                                      <span>{proc}</span>
                                    </div>
                                    <span className="rounded bg-amber-100 px-1.5 py-0.5 text-[10px] font-semibold text-amber-700 dark:bg-amber-500/20 dark:text-amber-300">
                                      {i18n.language === 'es' ? 'Proceso Puesto' : 'Process'}
                                    </span>
                                  </label>
                                );
                              })}
                            </div>
                          </div>
                        )}

                        {/* Procesos de RRHH */}
                        {hrProcList.length > 0 && (
                          <div className="space-y-2">
                            <div className="flex items-center justify-between text-xs font-semibold text-rose-700 dark:text-rose-300">
                              <span className="flex items-center gap-1.5">
                                <BookOpen className="h-3.5 w-3.5" />
                                {i18n.language === 'es' ? 'Procesos y Políticas de RRHH' : 'HR & Compliance Policies'} ({hrProcList.length})
                              </span>
                              <div className="flex items-center gap-2 text-[11px] font-normal">
                                <button type="button" onClick={() => selectAllCategory('hr')} className="text-blue-600 hover:underline dark:text-blue-400">
                                  {i18n.language === 'es' ? 'Todos' : 'All'}
                                </button>
                                <span>·</span>
                                <button type="button" onClick={() => deselectAllCategory('hr')} className="text-slate-400 hover:underline">
                                  {i18n.language === 'es' ? 'Ninguno' : 'None'}
                                </button>
                              </div>
                            </div>
                            <div className="space-y-1">
                              {hrProcList.map((proc) => {
                                const checked = selHr.has(proc);
                                return (
                                  <label
                                    key={proc}
                                    className={`flex cursor-pointer items-center justify-between gap-2 rounded-lg border p-2 text-xs transition ${
                                      checked
                                        ? 'border-rose-300 bg-rose-50/70 text-rose-950 font-medium dark:border-rose-500/30 dark:bg-rose-500/10 dark:text-rose-200'
                                        : 'border-slate-100 bg-slate-50/50 text-slate-700 hover:bg-slate-100/60 dark:border-white/5 dark:bg-white/[0.02] dark:text-white/70'
                                    }`}
                                  >
                                    <div className="flex items-center gap-2">
                                      <input
                                        type="checkbox"
                                        checked={checked}
                                        onChange={() => toggleItem('hr', proc)}
                                        className="h-3.5 w-3.5 rounded border-slate-300 text-brand-600"
                                      />
                                      <span>{proc}</span>
                                    </div>
                                    <span className="rounded bg-rose-100 px-1.5 py-0.5 text-[10px] font-semibold text-rose-700 dark:bg-rose-500/20 dark:text-rose-300">
                                      {i18n.language === 'es' ? 'RRHH' : 'HR'}
                                    </span>
                                  </label>
                                );
                              })}
                            </div>
                          </div>
                        )}
                      </div>
                    )}
                  </div>

                  {/* Columna Derecha: Plan Configurado "Día Uno / Primeros 90 Días" */}
                  <div className="space-y-4 rounded-2xl border border-blue-200 bg-blue-50/30 p-5 dark:border-blue-500/20 dark:bg-blue-500/5">
                    <div className="flex items-center justify-between border-b border-blue-100 pb-3 dark:border-white/5">
                      <div>
                        <h4 className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-blue-900 dark:text-blue-200">
                          <Rocket className="h-4 w-4 text-blue-600 dark:text-blue-400" />
                          {i18n.language === 'es' ? '2. Plan de Arranque del Empleado' : '2. New Hire Day One Plan'}
                        </h4>
                        <p className="text-[11px] text-blue-700/70 dark:text-blue-300/70">
                          {i18n.language === 'es'
                            ? 'Cursos y capacidades requeridas en los primeros 90 días'
                            : 'Essential courses and capabilities assigned for the first 90 days'}
                        </p>
                      </div>
                      <span className="rounded-full bg-blue-600 px-2.5 py-0.5 text-xs font-bold text-white">
                        {totalSelected}
                      </span>
                    </div>

                    {totalSelected === 0 ? (
                      <div className="rounded-xl border border-dashed border-blue-200 bg-white/60 p-8 text-center text-xs text-blue-800/60 dark:border-blue-400/20 dark:bg-transparent dark:text-blue-200/60">
                        <Sparkles className="mx-auto mb-2 h-6 w-6 text-blue-400" />
                        <p className="font-semibold">
                          {i18n.language === 'es' ? 'Sin capacidades seleccionadas aún' : 'No capabilities selected yet'}
                        </p>
                        <p className="mt-1">
                          {i18n.language === 'es'
                            ? 'Marca las casillas a la izquierda para armar el plan de los primeros días.'
                            : 'Check items on the left to build the initial onboarding roadmap.'}
                        </p>
                      </div>
                    ) : (
                      <div className="space-y-3">
                        {/* Items seleccionados por categoría */}
                        {selTech.size > 0 && (
                          <div className="space-y-1.5">
                            <p className="text-[11px] font-bold uppercase tracking-wider text-sky-800 dark:text-sky-300">
                              {i18n.language === 'es' ? 'Técnicas' : 'Technical'} ({selTech.size})
                            </p>
                            {Array.from(selTech).map((item) => (
                              <div
                                key={item}
                                className="flex items-center justify-between gap-2 rounded-lg bg-white px-3 py-2 text-xs shadow-xs dark:bg-white/10 dark:text-white"
                              >
                                <span className="font-medium text-slate-800 dark:text-white">{item}</span>
                                <button
                                  type="button"
                                  onClick={() => toggleItem('tech', item)}
                                  className="text-slate-400 hover:text-red-600 dark:hover:text-red-400"
                                  title={t('common.remove')}
                                >
                                  <X className="h-3.5 w-3.5" />
                                </button>
                              </div>
                            ))}
                          </div>
                        )}

                        {selSoft.size > 0 && (
                          <div className="space-y-1.5">
                            <p className="text-[11px] font-bold uppercase tracking-wider text-violet-800 dark:text-violet-300">
                              {i18n.language === 'es' ? 'No Técnicas / Blandas' : 'Soft'} ({selSoft.size})
                            </p>
                            {Array.from(selSoft).map((item) => (
                              <div
                                key={item}
                                className="flex items-center justify-between gap-2 rounded-lg bg-white px-3 py-2 text-xs shadow-xs dark:bg-white/10 dark:text-white"
                              >
                                <span className="font-medium text-slate-800 dark:text-white">{item}</span>
                                <button
                                  type="button"
                                  onClick={() => toggleItem('soft', item)}
                                  className="text-slate-400 hover:text-red-600 dark:hover:text-red-400"
                                  title={t('common.remove')}
                                >
                                  <X className="h-3.5 w-3.5" />
                                </button>
                              </div>
                            ))}
                          </div>
                        )}

                        {selDept.size > 0 && (
                          <div className="space-y-1.5">
                            <p className="text-[11px] font-bold uppercase tracking-wider text-amber-800 dark:text-amber-300">
                              {i18n.language === 'es' ? 'Procesos del Puesto' : 'Role Processes'} ({selDept.size})
                            </p>
                            {Array.from(selDept).map((item) => (
                              <div
                                key={item}
                                className="flex items-center justify-between gap-2 rounded-lg bg-white px-3 py-2 text-xs shadow-xs dark:bg-white/10 dark:text-white"
                              >
                                <span className="font-medium text-slate-800 dark:text-white">{item}</span>
                                <button
                                  type="button"
                                  onClick={() => toggleItem('dept', item)}
                                  className="text-slate-400 hover:text-red-600 dark:hover:text-red-400"
                                  title={t('common.remove')}
                                >
                                  <X className="h-3.5 w-3.5" />
                                </button>
                              </div>
                            ))}
                          </div>
                        )}

                        {selHr.size > 0 && (
                          <div className="space-y-1.5">
                            <p className="text-[11px] font-bold uppercase tracking-wider text-rose-800 dark:text-rose-300">
                              {i18n.language === 'es' ? 'Procesos y Políticas de RRHH' : 'HR & Compliance'} ({selHr.size})
                            </p>
                            {Array.from(selHr).map((item) => (
                              <div
                                key={item}
                                className="flex items-center justify-between gap-2 rounded-lg bg-white px-3 py-2 text-xs shadow-xs dark:bg-white/10 dark:text-white"
                              >
                                <span className="font-medium text-slate-800 dark:text-white">{item}</span>
                                <button
                                  type="button"
                                  onClick={() => toggleItem('hr', item)}
                                  className="text-slate-400 hover:text-red-600 dark:hover:text-red-400"
                                  title={t('common.remove')}
                                >
                                  <X className="h-3.5 w-3.5" />
                                </button>
                              </div>
                            ))}
                          </div>
                        )}
                      </div>
                    )}

                    <div className="pt-2">
                      <button
                        type="button"
                        onClick={(e) => handleSubmit(e as unknown as FormEvent)}
                        disabled={isSaving}
                        className="flex w-full items-center justify-center gap-2 rounded-xl bg-blue-600 py-2.5 text-xs font-semibold text-white transition hover:bg-blue-700 disabled:opacity-60"
                      >
                        <CheckCircle2 className="h-4 w-4" />
                        {isSaving ? t('common.saving') : (i18n.language === 'es' ? 'Guardar Cambios del Plan' : 'Save Plan Changes')}
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            );
          })()}
        </div>
      )}

      {activeTab === 'inYourRole' && (
        <div className="space-y-6">
          <section className="overflow-hidden rounded-3xl bg-gradient-to-br from-blue-600 via-indigo-600 to-violet-700 p-6 text-white shadow-xl shadow-indigo-900/10">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div className="max-w-2xl">
                <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-indigo-100">
                  <span className="flex h-5 w-5 items-center justify-center rounded-full bg-white/20 text-[10px] font-bold">02</span>
                  <span>{i18n.language === 'es' ? 'Después de los 90 días' : 'After 90 days'}</span>
                  <span className="rounded-full bg-blue-400/20 px-2 py-0.5 text-[10px] font-bold text-blue-200">
                    {i18n.language === 'es' ? 'En curso / Día a día' : 'Ongoing / Day-to-day'}
                  </span>
                </div>
                <h2 className="mt-3 text-2xl font-bold sm:text-3xl">
                  {i18n.language === 'es' ? 'En tu puesto' : 'In your role'}
                </h2>
                <p className="mt-2 text-sm leading-relaxed text-indigo-50 sm:text-base">
                  {i18n.language === 'es'
                    ? 'Crece en tu día a día: cursos asignados, objetivos reales y evidencia de tu progreso.'
                    : 'Grow in your day-to-day: assigned courses, real objectives, and evidence of your progress.'}
                </p>
              </div>
              <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-white/15 ring-1 ring-white/25 backdrop-blur">
                <Briefcase className="h-7 w-7" />
              </div>
            </div>
          </section>

          {/* Selector interactivo: Izquierda catálogo de capacidades del puesto / Derecha selección "En tu puesto (después de 90 días)" */}
          {(() => {
            const techList = linesToArray(form.technicalSkills);
            const softList = linesToArray(form.softSkills);
            const deptProcList = linesToArray(form.departmentProcesses);
            const hrProcList = linesToArray(form.hrProcesses);

            const selTech = new Set(linesToArray(form.inYourRoleTechnicalSkills));
            const selSoft = new Set(linesToArray(form.inYourRoleSoftSkills));
            const selDept = new Set(linesToArray(form.inYourRoleDepartmentProcesses));
            const selHr = new Set(linesToArray(form.inYourRoleHrProcesses));

            const totalAvailable = techList.length + softList.length + deptProcList.length + hrProcList.length;
            const totalSelected = selTech.size + selSoft.size + selDept.size + selHr.size;

            const toggleItem = (category: 'tech' | 'soft' | 'dept' | 'hr', item: string) => {
              if (category === 'tech') {
                const next = new Set(selTech);
                next.has(item) ? next.delete(item) : next.add(item);
                setForm((f) => ({ ...f, inYourRoleTechnicalSkills: arrayToLines(Array.from(next)) }));
              } else if (category === 'soft') {
                const next = new Set(selSoft);
                next.has(item) ? next.delete(item) : next.add(item);
                setForm((f) => ({ ...f, inYourRoleSoftSkills: arrayToLines(Array.from(next)) }));
              } else if (category === 'dept') {
                const next = new Set(selDept);
                next.has(item) ? next.delete(item) : next.add(item);
                setForm((f) => ({ ...f, inYourRoleDepartmentProcesses: arrayToLines(Array.from(next)) }));
              } else if (category === 'hr') {
                const next = new Set(selHr);
                next.has(item) ? next.delete(item) : next.add(item);
                setForm((f) => ({ ...f, inYourRoleHrProcesses: arrayToLines(Array.from(next)) }));
              }
            };

            const selectAllCategory = (category: 'tech' | 'soft' | 'dept' | 'hr') => {
              if (category === 'tech') {
                const next = new Set([...selTech, ...techList]);
                setForm((f) => ({ ...f, inYourRoleTechnicalSkills: arrayToLines(Array.from(next)) }));
              } else if (category === 'soft') {
                const next = new Set([...selSoft, ...softList]);
                setForm((f) => ({ ...f, inYourRoleSoftSkills: arrayToLines(Array.from(next)) }));
              } else if (category === 'dept') {
                const next = new Set([...selDept, ...deptProcList]);
                setForm((f) => ({ ...f, inYourRoleDepartmentProcesses: arrayToLines(Array.from(next)) }));
              } else if (category === 'hr') {
                const next = new Set([...selHr, ...hrProcList]);
                setForm((f) => ({ ...f, inYourRoleHrProcesses: arrayToLines(Array.from(next)) }));
              }
            };

            const deselectAllCategory = (category: 'tech' | 'soft' | 'dept' | 'hr') => {
              if (category === 'tech') {
                setForm((f) => ({ ...f, inYourRoleTechnicalSkills: '' }));
              } else if (category === 'soft') {
                setForm((f) => ({ ...f, inYourRoleSoftSkills: '' }));
              } else if (category === 'dept') {
                setForm((f) => ({ ...f, inYourRoleDepartmentProcesses: '' }));
              } else if (category === 'hr') {
                setForm((f) => ({ ...f, inYourRoleHrProcesses: '' }));
              }
            };

            return (
              <div className="space-y-4">
                <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-white/5">
                  <div>
                    <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                      {i18n.language === 'es' ? 'Plan de Desarrollo Continuo (Después de 90 Días)' : 'Ongoing Development Plan (After 90 Days)'}
                    </h3>
                    <p className="text-xs text-slate-500 dark:text-white/60">
                      {i18n.language === 'es'
                        ? 'Selecciona las capacidades técnicas, blandas y procesos que el colaborador cursará de manera continua en su rol.'
                        : 'Select the technical, soft, and process capabilities the employee will develop on an ongoing basis in their role.'}
                    </p>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="rounded-full bg-indigo-50 px-3 py-1 text-xs font-semibold text-indigo-700 dark:bg-indigo-500/15 dark:text-indigo-300">
                      {totalSelected} {i18n.language === 'es' ? 'seleccionadas de' : 'selected of'} {totalAvailable}
                    </span>
                  </div>
                </div>

                <div className="grid gap-6 lg:grid-cols-2">
                  {/* Columna Izquierda: Catálogo Disponible del Puesto */}
                  <div className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5">
                    <div className="flex items-center justify-between border-b border-slate-100 pb-3 dark:border-white/5">
                      <div>
                        <h4 className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-slate-700 dark:text-slate-300">
                          <BookOpen className="h-4 w-4 text-indigo-600 dark:text-indigo-400" />
                          {i18n.language === 'es' ? '1. Capacidades y Procesos del Puesto' : '1. Role Capabilities & Processes'}
                        </h4>
                        <p className="text-[11px] text-slate-500 dark:text-white/60">
                          {i18n.language === 'es' ? 'Marca las casillas que aplicarán para la etapa continua' : 'Check the items to include in the ongoing stage'}
                        </p>
                      </div>
                      <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-bold text-slate-700 dark:bg-white/10 dark:text-white">
                        {totalAvailable}
                      </span>
                    </div>

                    {totalAvailable === 0 ? (
                      <p className="py-8 text-center text-xs italic text-slate-400">
                        {i18n.language === 'es'
                          ? 'No hay capacidades registradas en la pestaña "Editar Puesto". Agrega habilidades técnicas o blandas primero.'
                          : 'No capabilities found. Add technical or soft skills in the "Edit Job Role" tab first.'}
                      </p>
                    ) : (
                      <div className="space-y-4 max-h-[600px] overflow-y-auto pr-1">
                        {/* Habilidades Técnicas */}
                        {techList.length > 0 && (
                          <div className="space-y-2">
                            <div className="flex items-center justify-between text-xs font-semibold text-sky-700 dark:text-sky-300">
                              <span className="flex items-center gap-1.5">
                                <Cpu className="h-3.5 w-3.5" />
                                {i18n.language === 'es' ? 'Habilidades Técnicas' : 'Technical Skills'} ({techList.length})
                              </span>
                              <div className="flex items-center gap-2 text-[11px] font-normal">
                                <button type="button" onClick={() => selectAllCategory('tech')} className="text-indigo-600 hover:underline dark:text-indigo-400">
                                  {i18n.language === 'es' ? 'Todos' : 'All'}
                                </button>
                                <span>·</span>
                                <button type="button" onClick={() => deselectAllCategory('tech')} className="text-slate-400 hover:underline">
                                  {i18n.language === 'es' ? 'Ninguno' : 'None'}
                                </button>
                              </div>
                            </div>
                            <div className="space-y-1">
                              {techList.map((skill) => {
                                const checked = selTech.has(skill);
                                return (
                                  <label
                                    key={skill}
                                    className={`flex cursor-pointer items-center justify-between gap-2 rounded-lg border p-2 text-xs transition ${
                                      checked
                                        ? 'border-sky-300 bg-sky-50/70 text-sky-950 font-medium dark:border-sky-500/30 dark:bg-sky-500/10 dark:text-sky-200'
                                        : 'border-slate-100 bg-slate-50/50 text-slate-700 hover:bg-slate-100/60 dark:border-white/5 dark:bg-white/[0.02] dark:text-white/70'
                                    }`}
                                  >
                                    <div className="flex items-center gap-2 min-w-0">
                                      <input
                                        type="checkbox"
                                        checked={checked}
                                        onChange={() => toggleItem('tech', skill)}
                                        className="h-3.5 w-3.5 rounded border-slate-300 text-brand-600"
                                      />
                                      <span className="truncate">{skill}</span>
                                    </div>
                                    <span className="rounded bg-sky-100 px-1.5 py-0.5 text-[10px] font-semibold text-sky-700 dark:bg-sky-500/20 dark:text-sky-300">
                                      {i18n.language === 'es' ? 'Técnica' : 'Tech'}
                                    </span>
                                  </label>
                                );
                              })}
                            </div>
                          </div>
                        )}

                        {/* Habilidades Blandas */}
                        {softList.length > 0 && (
                          <div className="space-y-2">
                            <div className="flex items-center justify-between text-xs font-semibold text-violet-700 dark:text-violet-300">
                              <span className="flex items-center gap-1.5">
                                <Handshake className="h-3.5 w-3.5" />
                                {i18n.language === 'es' ? 'Habilidades Blandas' : 'Soft Skills'} ({softList.length})
                              </span>
                              <div className="flex items-center gap-2 text-[11px] font-normal">
                                <button type="button" onClick={() => selectAllCategory('soft')} className="text-indigo-600 hover:underline dark:text-indigo-400">
                                  {i18n.language === 'es' ? 'Todos' : 'All'}
                                </button>
                                <span>·</span>
                                <button type="button" onClick={() => deselectAllCategory('soft')} className="text-slate-400 hover:underline">
                                  {i18n.language === 'es' ? 'Ninguno' : 'None'}
                                </button>
                              </div>
                            </div>
                            <div className="space-y-1">
                              {softList.map((skill) => {
                                const checked = selSoft.has(skill);
                                return (
                                  <label
                                    key={skill}
                                    className={`flex cursor-pointer items-center justify-between gap-2 rounded-lg border p-2 text-xs transition ${
                                      checked
                                        ? 'border-violet-300 bg-violet-50/70 text-violet-950 font-medium dark:border-violet-500/30 dark:bg-violet-500/10 dark:text-violet-200'
                                        : 'border-slate-100 bg-slate-50/50 text-slate-700 hover:bg-slate-100/60 dark:border-white/5 dark:bg-white/[0.02] dark:text-white/70'
                                    }`}
                                  >
                                    <div className="flex items-center gap-2 min-w-0">
                                      <input
                                        type="checkbox"
                                        checked={checked}
                                        onChange={() => toggleItem('soft', skill)}
                                        className="h-3.5 w-3.5 rounded border-slate-300 text-brand-600"
                                      />
                                      <span className="truncate">{skill}</span>
                                    </div>
                                    <span className="rounded bg-violet-100 px-1.5 py-0.5 text-[10px] font-semibold text-violet-700 dark:bg-violet-500/20 dark:text-violet-300">
                                      {i18n.language === 'es' ? 'Blanda' : 'Soft'}
                                    </span>
                                  </label>
                                );
                              })}
                            </div>
                          </div>
                        )}

                        {/* Procesos del Puesto */}
                        {deptProcList.length > 0 && (
                          <div className="space-y-2">
                            <div className="flex items-center justify-between text-xs font-semibold text-amber-700 dark:text-amber-300">
                              <span className="flex items-center gap-1.5">
                                <Briefcase className="h-3.5 w-3.5" />
                                {i18n.language === 'es' ? 'Procesos del Puesto' : 'Role Processes'} ({deptProcList.length})
                              </span>
                              <div className="flex items-center gap-2 text-[11px] font-normal">
                                <button type="button" onClick={() => selectAllCategory('dept')} className="text-indigo-600 hover:underline dark:text-indigo-400">
                                  {i18n.language === 'es' ? 'Todos' : 'All'}
                                </button>
                                <span>·</span>
                                <button type="button" onClick={() => deselectAllCategory('dept')} className="text-slate-400 hover:underline">
                                  {i18n.language === 'es' ? 'Ninguno' : 'None'}
                                </button>
                              </div>
                            </div>
                            <div className="space-y-1">
                              {deptProcList.map((proc) => {
                                const checked = selDept.has(proc);
                                return (
                                  <label
                                    key={proc}
                                    className={`flex cursor-pointer items-center justify-between gap-2 rounded-lg border p-2 text-xs transition ${
                                      checked
                                        ? 'border-amber-300 bg-amber-50/70 text-amber-950 font-medium dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-200'
                                        : 'border-slate-100 bg-slate-50/50 text-slate-700 hover:bg-slate-100/60 dark:border-white/5 dark:bg-white/[0.02] dark:text-white/70'
                                    }`}
                                  >
                                    <div className="flex items-center gap-2 min-w-0">
                                      <input
                                        type="checkbox"
                                        checked={checked}
                                        onChange={() => toggleItem('dept', proc)}
                                        className="h-3.5 w-3.5 rounded border-slate-300 text-brand-600"
                                      />
                                      <span className="truncate">{proc}</span>
                                    </div>
                                    <span className="rounded bg-amber-100 px-1.5 py-0.5 text-[10px] font-semibold text-amber-700 dark:bg-amber-500/20 dark:text-amber-300">
                                      {i18n.language === 'es' ? 'Proceso Puesto' : 'Process'}
                                    </span>
                                  </label>
                                );
                              })}
                            </div>
                          </div>
                        )}

                        {/* Procesos de RRHH */}
                        {hrProcList.length > 0 && (
                          <div className="space-y-2">
                            <div className="flex items-center justify-between text-xs font-semibold text-rose-700 dark:text-rose-300">
                              <span className="flex items-center gap-1.5">
                                <BookOpen className="h-3.5 w-3.5" />
                                {i18n.language === 'es' ? 'Procesos y Políticas de RRHH' : 'HR & Compliance Policies'} ({hrProcList.length})
                              </span>
                              <div className="flex items-center gap-2 text-[11px] font-normal">
                                <button type="button" onClick={() => selectAllCategory('hr')} className="text-indigo-600 hover:underline dark:text-indigo-400">
                                  {i18n.language === 'es' ? 'Todos' : 'All'}
                                </button>
                                <span>·</span>
                                <button type="button" onClick={() => deselectAllCategory('hr')} className="text-slate-400 hover:underline">
                                  {i18n.language === 'es' ? 'Ninguno' : 'None'}
                                </button>
                              </div>
                            </div>
                            <div className="space-y-1">
                              {hrProcList.map((proc) => {
                                const checked = selHr.has(proc);
                                return (
                                  <label
                                    key={proc}
                                    className={`flex cursor-pointer items-center justify-between gap-2 rounded-lg border p-2 text-xs transition ${
                                      checked
                                        ? 'border-rose-300 bg-rose-50/70 text-rose-950 font-medium dark:border-rose-500/30 dark:bg-rose-500/10 dark:text-rose-200'
                                        : 'border-slate-100 bg-slate-50/50 text-slate-700 hover:bg-slate-100/60 dark:border-white/5 dark:bg-white/[0.02] dark:text-white/70'
                                    }`}
                                  >
                                    <div className="flex items-center gap-2 min-w-0">
                                      <input
                                        type="checkbox"
                                        checked={checked}
                                        onChange={() => toggleItem('hr', proc)}
                                        className="h-3.5 w-3.5 rounded border-slate-300 text-brand-600"
                                      />
                                      <span className="truncate">{proc}</span>
                                    </div>
                                    <span className="rounded bg-rose-100 px-1.5 py-0.5 text-[10px] font-semibold text-rose-700 dark:bg-rose-500/20 dark:text-rose-300">
                                      {i18n.language === 'es' ? 'RRHH' : 'HR'}
                                    </span>
                                  </label>
                                );
                              })}
                            </div>
                          </div>
                        )}
                      </div>
                    )}
                  </div>

                  {/* Columna Derecha: Plan Configurado "En tu puesto (Después de 90 días)" */}
                  <div className="space-y-4 rounded-2xl border border-indigo-200 bg-indigo-50/30 p-5 dark:border-indigo-500/20 dark:bg-indigo-500/5">
                    <div className="flex items-center justify-between border-b border-indigo-100 pb-3 dark:border-white/5">
                      <div>
                        <h4 className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-indigo-900 dark:text-indigo-200">
                          <Briefcase className="h-4 w-4 text-indigo-600 dark:text-indigo-400" />
                          {i18n.language === 'es' ? '2. Plan Seleccionado para tu Puesto' : '2. Ongoing Plan for This Role'}
                        </h4>
                        <p className="text-[11px] text-indigo-700/70 dark:text-indigo-300/70">
                          {i18n.language === 'es'
                            ? 'Cursos y capacidades visibles para el colaborador tras los primeros 90 días'
                            : 'Capabilities active for the employee in their ongoing role stage'}
                        </p>
                      </div>
                      <span className="rounded-full bg-indigo-600 px-2.5 py-0.5 text-xs font-bold text-white">
                        {totalSelected}
                      </span>
                    </div>

                    {totalSelected === 0 ? (
                      <div className="rounded-xl border border-dashed border-indigo-200 bg-white/60 p-8 text-center text-xs text-indigo-800/60 dark:border-indigo-400/20 dark:bg-transparent dark:text-indigo-200/60">
                        <Sparkles className="mx-auto mb-2 h-6 w-6 text-indigo-400" />
                        <p className="font-semibold">
                          {i18n.language === 'es' ? 'Sin capacidades seleccionadas aún' : 'No capabilities selected yet'}
                        </p>
                        <p className="mt-1">
                          {i18n.language === 'es'
                            ? 'Marca las casillas a la izquierda para armar el plan continuo.'
                            : 'Check items on the left to configure the ongoing role roadmap.'}
                        </p>
                      </div>
                    ) : (
                      <div className="space-y-3 max-h-[520px] overflow-y-auto pr-1">
                        {/* Items seleccionados por categoría */}
                        {selTech.size > 0 && (
                          <div className="space-y-1.5">
                            <p className="text-[11px] font-bold uppercase tracking-wider text-sky-800 dark:text-sky-300">
                              {i18n.language === 'es' ? 'Técnicas' : 'Technical'} ({selTech.size})
                            </p>
                            {Array.from(selTech).map((item) => (
                              <div
                                key={item}
                                className="flex items-center justify-between gap-2 rounded-lg bg-white px-3 py-2 text-xs shadow-xs dark:bg-white/10 dark:text-white"
                              >
                                <span className="font-medium text-slate-800 dark:text-white">{item}</span>
                                <button
                                  type="button"
                                  onClick={() => toggleItem('tech', item)}
                                  className="text-slate-400 hover:text-red-600 dark:hover:text-red-400"
                                  title={t('common.remove')}
                                >
                                  <X className="h-3.5 w-3.5" />
                                </button>
                              </div>
                            ))}
                          </div>
                        )}

                        {selSoft.size > 0 && (
                          <div className="space-y-1.5">
                            <p className="text-[11px] font-bold uppercase tracking-wider text-violet-800 dark:text-violet-300">
                              {i18n.language === 'es' ? 'No Técnicas / Blandas' : 'Soft'} ({selSoft.size})
                            </p>
                            {Array.from(selSoft).map((item) => (
                              <div
                                key={item}
                                className="flex items-center justify-between gap-2 rounded-lg bg-white px-3 py-2 text-xs shadow-xs dark:bg-white/10 dark:text-white"
                              >
                                <span className="font-medium text-slate-800 dark:text-white">{item}</span>
                                <button
                                  type="button"
                                  onClick={() => toggleItem('soft', item)}
                                  className="text-slate-400 hover:text-red-600 dark:hover:text-red-400"
                                  title={t('common.remove')}
                                >
                                  <X className="h-3.5 w-3.5" />
                                </button>
                              </div>
                            ))}
                          </div>
                        )}

                        {selDept.size > 0 && (
                          <div className="space-y-1.5">
                            <p className="text-[11px] font-bold uppercase tracking-wider text-amber-800 dark:text-amber-300">
                              {i18n.language === 'es' ? 'Procesos del Puesto' : 'Role Processes'} ({selDept.size})
                            </p>
                            {Array.from(selDept).map((item) => (
                              <div
                                key={item}
                                className="flex items-center justify-between gap-2 rounded-lg bg-white px-3 py-2 text-xs shadow-xs dark:bg-white/10 dark:text-white"
                              >
                                <span className="font-medium text-slate-800 dark:text-white">{item}</span>
                                <button
                                  type="button"
                                  onClick={() => toggleItem('dept', item)}
                                  className="text-slate-400 hover:text-red-600 dark:hover:text-red-400"
                                  title={t('common.remove')}
                                >
                                  <X className="h-3.5 w-3.5" />
                                </button>
                              </div>
                            ))}
                          </div>
                        )}

                        {selHr.size > 0 && (
                          <div className="space-y-1.5">
                            <p className="text-[11px] font-bold uppercase tracking-wider text-rose-800 dark:text-rose-300">
                              {i18n.language === 'es' ? 'Procesos y Políticas de RRHH' : 'HR & Compliance'} ({selHr.size})
                            </p>
                            {Array.from(selHr).map((item) => (
                              <div
                                key={item}
                                className="flex items-center justify-between gap-2 rounded-lg bg-white px-3 py-2 text-xs shadow-xs dark:bg-white/10 dark:text-white"
                              >
                                <span className="font-medium text-slate-800 dark:text-white">{item}</span>
                                <button
                                  type="button"
                                  onClick={() => toggleItem('hr', item)}
                                  className="text-slate-400 hover:text-red-600 dark:hover:text-red-400"
                                  title={t('common.remove')}
                                >
                                  <X className="h-3.5 w-3.5" />
                                </button>
                              </div>
                            ))}
                          </div>
                        )}
                      </div>
                    )}

                    <div className="pt-2">
                      <button
                        type="button"
                        onClick={(e) => handleSubmit(e as unknown as FormEvent)}
                        disabled={isSaving}
                        className="flex w-full items-center justify-center gap-2 rounded-xl bg-indigo-600 py-2.5 text-xs font-semibold text-white transition hover:bg-indigo-700 disabled:opacity-60"
                      >
                        <CheckCircle2 className="h-4 w-4" />
                        {isSaving ? t('common.saving') : (i18n.language === 'es' ? 'Guardar Cambios del Plan' : 'Save Plan Changes')}
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            );
          })()}
        </div>
      )}

      {activeTab === 'stayCurrent' && (
        <div className="space-y-6">
          <section className="overflow-hidden rounded-3xl bg-gradient-to-br from-amber-500 via-orange-600 to-rose-600 p-6 text-white shadow-xl shadow-orange-900/10">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div className="max-w-2xl">
                <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-amber-100">
                  <span className="flex h-5 w-5 items-center justify-center rounded-full bg-white/20 text-[10px] font-bold">03</span>
                  <span>{i18n.language === 'es' ? 'Siempre' : 'Always'}</span>
                  <span className="rounded-full bg-amber-300/25 px-2 py-0.5 text-[10px] font-bold text-amber-100">
                    {i18n.language === 'es' ? 'A lo largo de tu carrera' : 'Throughout your career'}
                  </span>
                </div>
                <h2 className="mt-3 text-2xl font-bold sm:text-3xl">
                  {i18n.language === 'es' ? 'Mantente vigente' : 'Stay current'}
                </h2>
                <p className="mt-2 text-sm leading-relaxed text-amber-50 sm:text-base">
                  {i18n.language === 'es'
                    ? 'Actualiza tus capacidades conforme evolucionan tu puesto y tu industria.'
                    : 'Update your capabilities as your role and industry evolve.'}
                </p>
              </div>
              <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-white/15 ring-1 ring-white/25 backdrop-blur">
                <RefreshCw className="h-7 w-7" />
              </div>
            </div>
          </section>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5 space-y-3">
              <div className="flex items-center gap-2 text-xs font-semibold text-amber-600 dark:text-amber-400">
                <Award className="h-4 w-4" />
                <span>{i18n.language === 'es' ? 'Certificaciones & Nuevas Herramientas' : 'Certifications & Tooling'}</span>
              </div>
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {i18n.language === 'es' ? 'Evolución Tecnológica' : 'Tooling & Certifications'}
              </h3>
              <p className="text-xs text-slate-600 dark:text-white/70">
                {form.requiredCertifications || (i18n.language === 'es' ? 'Certificaciones periódicas y actualización en nuevas versiones del stack.' : 'Periodic certifications and stack refreshes.')}
              </p>
              {linesToArray(form.tools).length > 0 && (
                <div className="flex flex-wrap gap-1.5 pt-2">
                  {linesToArray(form.tools).map((t, idx) => (
                    <span key={idx} className="rounded-lg bg-amber-50 px-2 py-1 text-[11px] font-medium text-amber-800 dark:bg-amber-500/10 dark:text-amber-300">
                      {t}
                    </span>
                  ))}
                </div>
              )}
            </div>

            <div className="rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5 space-y-3">
              <div className="flex items-center gap-2 text-xs font-semibold text-orange-600 dark:text-orange-400">
                <Flame className="h-4 w-4" />
                <span>{i18n.language === 'es' ? 'Tendencias de la Industria' : 'Industry Trends'}</span>
              </div>
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {i18n.language === 'es' ? 'Capacidades Emergentes' : 'Emerging Capabilities'}
              </h3>
              <p className="text-xs text-slate-600 dark:text-white/70">
                {i18n.language === 'es'
                  ? 'Revisión periódica de nuevas habilidades recomendadas por IA y cambios del sector.'
                  : 'Continuous scanning of AI-suggested skills and industry shifts to keep this role ahead.'}
              </p>
            </div>
          </div>
        </div>
      )}

      {activeTab === 'masterYourJob' && (
        <div className="space-y-6">
          <section className="overflow-hidden rounded-3xl bg-gradient-to-br from-teal-600 via-cyan-600 to-blue-700 p-6 text-white shadow-xl shadow-cyan-900/10">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div className="max-w-2xl">
                <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-cyan-100">
                  <span className="flex h-5 w-5 items-center justify-center rounded-full bg-white/20 text-[10px] font-bold">04</span>
                  <span>{i18n.language === 'es' ? 'Madurez y Dominio' : 'Seniority & Mastery'}</span>
                  <span className="rounded-full bg-cyan-300/25 px-2 py-0.5 text-[10px] font-bold text-cyan-100">
                    {i18n.language === 'es' ? 'Antes de tu siguiente etapa' : 'Before your next chapter'}
                  </span>
                </div>
                <h2 className="mt-3 text-2xl font-bold sm:text-3xl">
                  {i18n.language === 'es' ? 'Domina tu puesto' : 'Master your job'}
                </h2>
                <p className="mt-2 text-sm leading-relaxed text-cyan-50 sm:text-base">
                  {i18n.language === 'es'
                    ? 'Alcanza el máximo desempeño en tu puesto y prepárate para el liderazgo o la especialización.'
                    : 'Reach peak performance in your role and prepare for leadership or specialization.'}
                </p>
              </div>
              <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-white/15 ring-1 ring-white/25 backdrop-blur">
                <GraduationCap className="h-7 w-7" />
              </div>
            </div>
          </section>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5 space-y-3">
              <div className="flex items-center gap-2 text-xs font-semibold text-cyan-600 dark:text-cyan-400">
                <Award className="h-4 w-4" />
                <span>{i18n.language === 'es' ? 'Liderazgo & Mentoría' : 'Leadership & Mentorship'}</span>
              </div>
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {i18n.language === 'es' ? 'Desarrollo de Otros' : 'Mentoring & Impact'}
              </h3>
              <p className="text-xs text-slate-600 dark:text-white/70">
                {i18n.language === 'es'
                  ? 'Guía a nuevos integrantes, lidera proyectos estratégicos y comparte mejores prácticas.'
                  : 'Guide newcomers, drive cross-functional strategic initiatives, and institutionalize best practices.'}
              </p>
            </div>

            <div className="rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5 space-y-3">
              <div className="flex items-center gap-2 text-xs font-semibold text-teal-600 dark:text-teal-400">
                <Rocket className="h-4 w-4" />
                <span>{i18n.language === 'es' ? 'Siguiente Nivel de Carrera' : 'Next Career Chapter'}</span>
              </div>
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {i18n.language === 'es' ? 'Especialización o Gestión' : 'Specialization & Growth'}
              </h3>
              <p className="text-xs text-slate-600 dark:text-white/70">
                {form.expectedOutcomes || (i18n.language === 'es' ? 'Preparación de carrera para roles senior, liderazgo de equipo o especialización experta.' : 'Career roadmap toward senior roles, management, or deep technical specialization.')}
              </p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
