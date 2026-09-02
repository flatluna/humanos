import { useEffect, useState, type FormEvent, type ChangeEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useParams, useSearchParams } from 'react-router';
import {
  ArrowLeft,
  GraduationCap,
  Heart,
  Building2,
  ShieldCheck,
  FileText,
  Upload,
  Trash2,
  ExternalLink,
  X,
  Loader2,
  CheckCircle2,
  PlayCircle,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { getJobRoles } from '@/api/orgStructureApi';
import {
  createSkill,
  getSkills,
  updateSkill,
  uploadSkillDocument,
  deleteSkillDocument,
  type SaveSkillRequest,
  type SkillCategory,
  type SkillDto,
} from '@/api/skillsApi';

const CATEGORY_OPTIONS: { id: SkillCategory; labelKey: string; icon: typeof GraduationCap }[] = [
  { id: 'job-skills', labelKey: 'skills.categoryTabs.job-skills.label', icon: GraduationCap },
  { id: 'soft-skills', labelKey: 'skills.categoryTabs.soft-skills.label', icon: Heart },
  { id: 'department-processes', labelKey: 'skills.categoryTabs.department-processes.label', icon: Building2 },
  { id: 'hr-processes', labelKey: 'skills.categoryTabs.hr-processes.label', icon: ShieldCheck },
];

const inputClass =
  'mt-1 w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white';
const labelClass = 'block text-xs font-medium text-slate-500 dark:text-white/50';

export function SkillDetailPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const navigate = useNavigate();
  const params = useParams<{ skillId?: string }>();
  const [searchParams] = useSearchParams();
  const queryClient = useQueryClient();

  const skillId = params.skillId;
  const initialCategory = (searchParams.get('category') as SkillCategory) || 'job-skills';

  const [category, setCategory] = useState<SkillCategory>(initialCategory);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [jobRoleIds, setJobRoleIds] = useState<string[]>([]);
  const [currentSkill, setCurrentSkill] = useState<SkillDto | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // PDF Upload Modal State
  const [uploadModalOpen, setUploadModalOpen] = useState(false);
  const [selectedPdf, setSelectedPdf] = useState<File | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [uploadSuccess, setUploadSuccess] = useState<string | null>(null);

  const skillsQuery = useQuery({
    queryKey: ['skills', tenantId],
    queryFn: () => getSkills(tenantId),
  });

  const jobRolesQuery = useQuery({
    queryKey: ['job-roles', tenantId],
    queryFn: () => getJobRoles(tenantId),
  });

  useEffect(() => {
    if (skillId && skillsQuery.data) {
      const skill = skillsQuery.data.find((s) => s.skillId === skillId);
      if (skill) {
        setCurrentSkill(skill);
        setCategory(skill.category);
        setName(skill.name);
        setDescription(skill.description ?? '');
        setJobRoleIds(skill.jobRoles.map((r) => r.jobRoleId));
      }
    }
  }, [skillId, skillsQuery.data]);

  const saveMutation = useMutation({
    mutationFn: (body: SaveSkillRequest) =>
      skillId ? updateSkill(skillId, body) : createSkill(tenantId, body),
    onSuccess: (savedSkill) => {
      void queryClient.invalidateQueries({ queryKey: ['skills', tenantId] });
      void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId] });
      if (!skillId) {
        // Redirect to edit page so user can upload document if creating new
        navigate(`/skills/${savedSkill.skillId}/edit`);
      } else {
        navigate(`/skills/${category}`);
      }
    },
    onError: (error: unknown) => {
      setErrorMessage(error instanceof Error ? error.message : t('common.unexpectedError'));
    },
  });

  const uploadPdfMutation = useMutation({
    mutationFn: ({ id, file }: { id: string; file: File }) => uploadSkillDocument(id, file),
    onSuccess: (updatedSkill) => {
      setCurrentSkill(updatedSkill);
      void queryClient.invalidateQueries({ queryKey: ['skills', tenantId] });
      setUploadModalOpen(false);
      setSelectedPdf(null);
      setUploadError(null);
      setUploadSuccess('El documento PDF fue subido correctamente al Data Lake.');
      setTimeout(() => setUploadSuccess(null), 4000);
    },
    onError: (error: unknown) => {
      setUploadError(error instanceof Error ? error.message : 'No se pudo subir el archivo PDF.');
    },
  });

  const deletePdfMutation = useMutation({
    mutationFn: (id: string) => deleteSkillDocument(id),
    onSuccess: (updatedSkill) => {
      setCurrentSkill(updatedSkill);
      void queryClient.invalidateQueries({ queryKey: ['skills', tenantId] });
    },
    onError: (error: unknown) => {
      setErrorMessage(error instanceof Error ? error.message : 'No se pudo eliminar el archivo.');
    },
  });

  function handleFileChange(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.type !== 'application/pdf' && !file.name.toLowerCase().endsWith('.pdf')) {
      setUploadError('Por favor selecciona únicamente archivos en formato PDF (.pdf).');
      setSelectedPdf(null);
      return;
    }

    setUploadError(null);
    setSelectedPdf(file);
  }

  function handlePdfUploadSubmit() {
    if (!skillId || !selectedPdf) return;
    setUploadError(null);
    uploadPdfMutation.mutate({ id: skillId, file: selectedPdf });
  }

  function toggleJobRole(jobRoleId: string) {
    setJobRoleIds((prev) =>
      prev.includes(jobRoleId) ? prev.filter((id) => id !== jobRoleId) : [...prev, jobRoleId]
    );
  }

  function toggleSelectAllJobRoles() {
    const allIds = (jobRolesQuery.data ?? []).map((r) => r.jobRoleId);
    const allSelected = allIds.length > 0 && allIds.every((id) => jobRoleIds.includes(id));
    setJobRoleIds(allSelected ? [] : allIds);
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!name.trim()) return;

    saveMutation.mutate({
      category,
      name: name.trim(),
      description: description.trim() || null,
      jobRoleIds,
    });
  }

  const allJobRoles = jobRolesQuery.data ?? [];
  const allSelected = allJobRoles.length > 0 && allJobRoles.every((r) => jobRoleIds.includes(r.jobRoleId));

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div className="flex items-center gap-3">
        <button
          type="button"
          onClick={() => navigate(`/skills/${category}`)}
          className="flex h-9 w-9 items-center justify-center rounded-full border border-slate-200 bg-white text-slate-600 shadow-sm transition hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white/70 dark:hover:bg-white/10"
        >
          <ArrowLeft className="h-4 w-4" />
        </button>
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">
              {skillId ? t('skills.editSkill') : t('skills.newSkill')}
            </h1>
            {skillId && (
              <span className="rounded-full bg-slate-100 px-3 py-1 font-mono text-xs text-slate-600 dark:bg-white/10 dark:text-white/60">
                ID: {skillId}
              </span>
            )}
          </div>
          <p className="text-xs text-slate-500 dark:text-white/50">
            {t('skills.title')}
          </p>
        </div>
      </div>

      {uploadSuccess && (
        <div className="flex items-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-xs text-emerald-800 dark:border-emerald-500/20 dark:bg-emerald-500/10 dark:text-emerald-300">
          <CheckCircle2 className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
          <span>{uploadSuccess}</span>
        </div>
      )}

      {currentSkill?.matchedCapabilityId && (
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 rounded-2xl border border-emerald-200 bg-emerald-50 p-4 dark:border-emerald-500/20 dark:bg-emerald-500/10">
          <div className="flex items-center gap-3">
            <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-emerald-600 text-white">
              <PlayCircle className="h-5 w-5" />
            </span>
            <div>
              <p className="text-xs font-semibold text-emerald-950 dark:text-emerald-200">
                Curso creado en Capability Studio: {currentSkill.matchedCapabilityName || currentSkill.name}
              </p>
              <p className="text-[11px] text-emerald-700 dark:text-emerald-300">
                Esta habilidad ya cuenta con un curso interactivo generado en Studio.
              </p>
            </div>
          </div>
          <a
            href={
              currentSkill.matchedCourseType === 'graph'
                ? `http://localhost:3010/capabilities/${currentSkill.matchedCapabilityId}`
                : `http://localhost:3010/structured-learning/${currentSkill.matchedCapabilityId}`
            }
            target="_blank"
            rel="noreferrer"
            className="inline-flex shrink-0 items-center gap-1.5 self-start sm:self-auto rounded-full bg-emerald-600 px-4 py-2 text-xs font-semibold text-white shadow-sm transition hover:bg-emerald-500"
          >
            <PlayCircle className="h-4 w-4" />
            Abrir curso en Studio
            <ExternalLink className="h-3 w-3" />
          </a>
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-white/5">
        {errorMessage && (
          <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-xs text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
            {errorMessage}
          </div>
        )}

        <div>
          <label className={labelClass}>Categoría</label>
          <div className="mt-2 grid grid-cols-2 gap-2 sm:grid-cols-4">
            {CATEGORY_OPTIONS.map((cat) => {
              const Icon = cat.icon;
              const isSelected = category === cat.id;
              return (
                <button
                  key={cat.id}
                  type="button"
                  onClick={() => setCategory(cat.id)}
                  className={`flex items-center gap-2 rounded-xl border p-3 text-xs font-medium transition ${
                    isSelected
                      ? 'border-brand-500 bg-brand-50 text-brand-700 dark:border-brand-500/50 dark:bg-brand-500/10 dark:text-brand-300'
                      : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white/70 dark:hover:bg-white/10'
                  }`}
                >
                  <Icon className="h-4 w-4 shrink-0" />
                  <span className="truncate">{t(cat.labelKey as any)}</span>
                </button>
              );
            })}
          </div>
        </div>

        <div>
          <label className={labelClass}>{t('skills.nameLabel')}</label>
          <input
            className={inputClass}
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            autoFocus
          />
        </div>

        <div>
          <label className={labelClass}>{t('skills.descriptionLabel')}</label>
          <textarea
            className={inputClass}
            rows={3}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />
        </div>

        {/* PDF Document Attachment Section */}
        <div className="rounded-xl border border-slate-200 bg-slate-50/50 p-4 dark:border-white/10 dark:bg-white/5">
          <div className="flex items-center justify-between">
            <div>
              <label className="text-xs font-semibold text-slate-800 dark:text-white/90">Documento PDF en Data Lake</label>
              <p className="text-[11px] text-slate-500 dark:text-white/50">
                Almacenado en contenedor <span className="font-mono text-slate-700 dark:text-white/70">{tenantId}</span> bajo <span className="font-mono text-slate-700 dark:text-white/70">skills/{skillId ?? '...'} / Documents/ document.pdf</span>
              </p>
            </div>
            {skillId && (
              <button
                type="button"
                onClick={() => {
                  setSelectedPdf(null);
                  setUploadError(null);
                  setUploadModalOpen(true);
                }}
                className="inline-flex items-center gap-1.5 rounded-full border border-brand-500/30 bg-brand-500/10 px-3 py-1.5 text-xs font-medium text-brand-600 hover:bg-brand-500/20 dark:text-brand-300"
              >
                <Upload className="h-3.5 w-3.5" />
                {currentSkill?.documentStoragePath ? 'Reemplazar PDF' : 'Subir PDF'}
              </button>
            )}
          </div>

          {!skillId ? (
            <p className="mt-3 text-xs text-amber-600 dark:text-amber-400">
              Guarda primero la skill para habilitar la carga del documento PDF al Data Lake.
            </p>
          ) : currentSkill?.documentStoragePath ? (
            <div className="mt-3 flex items-center justify-between gap-3 rounded-lg border border-slate-200 bg-white p-3 dark:border-white/10 dark:bg-white/5">
              <div className="flex items-center gap-2.5 min-w-0">
                <FileText className="h-5 w-5 shrink-0 text-red-500" />
                <div className="min-w-0">
                  <p className="truncate text-xs font-medium text-slate-900 dark:text-white">
                    {currentSkill.documentFileName || 'document.pdf'}
                  </p>
                  <p className="truncate font-mono text-[10px] text-slate-400 dark:text-white/40">
                    {currentSkill.documentStoragePath}
                  </p>
                </div>
              </div>
              <div className="flex items-center gap-2 shrink-0">
                <a
                  href={`http://localhost:7071/api/skills/${skillId}/document`}
                  target="_blank"
                  rel="noreferrer"
                  className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs font-medium text-slate-700 hover:bg-slate-100 dark:border-white/10 dark:bg-white/10 dark:text-white/80 dark:hover:bg-white/20"
                >
                  <ExternalLink className="h-3 w-3" />
                  Ver PDF
                </a>
                <button
                  type="button"
                  onClick={() => deletePdfMutation.mutate(skillId)}
                  disabled={deletePdfMutation.isPending}
                  className="inline-flex items-center gap-1 rounded-lg border border-red-200 bg-red-50 px-2.5 py-1 text-xs font-medium text-red-600 hover:bg-red-100 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300"
                >
                  <Trash2 className="h-3 w-3" />
                  Eliminar
                </button>
              </div>
            </div>
          ) : (
            <p className="mt-2 text-xs text-slate-400 dark:text-white/40">
              No hay ningún documento PDF adjunto actualmente para esta skill.
            </p>
          )}
        </div>

        <div>
          <div className="flex items-center justify-between">
            <label className={labelClass}>{t('skills.linkedRolesLabel')}</label>
            <button
              type="button"
              onClick={toggleSelectAllJobRoles}
              className="text-xs font-medium text-brand-600 hover:underline dark:text-brand-400"
            >
              {allSelected ? t('skills.selectNone') : t('skills.selectAll')}
            </button>
          </div>
          <div className="mt-2 max-h-60 space-y-1 overflow-y-auto rounded-xl border border-slate-200 p-3 dark:border-white/10">
            {allJobRoles.length === 0 && (
              <p className="px-1 py-1 text-xs text-slate-400">{t('skills.noRolesYet')}</p>
            )}
            {allJobRoles.map((role) => (
              <label
                key={role.jobRoleId}
                className="flex cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 text-sm text-slate-700 hover:bg-slate-50 dark:text-white/70 dark:hover:bg-white/5"
              >
                <input
                  type="checkbox"
                  checked={jobRoleIds.includes(role.jobRoleId)}
                  onChange={() => toggleJobRole(role.jobRoleId)}
                  className="h-4 w-4 rounded border-slate-300 text-brand-600 focus:ring-brand-500"
                />
                <span>
                  {role.title} <span className="text-xs text-slate-400">· {role.departmentName}</span>
                </span>
              </label>
            ))}
          </div>
        </div>

        <div className="flex justify-end gap-3 pt-4 border-t border-slate-200 dark:border-white/10">
          <button
            type="button"
            onClick={() => navigate(`/skills/${category}`)}
            className="rounded-full border border-slate-200 px-5 py-2 text-sm font-medium text-slate-600 hover:bg-slate-50 dark:border-white/10 dark:text-white/60 dark:hover:bg-white/5"
          >
            {t('common.cancel')}
          </button>
          <button
            type="submit"
            disabled={saveMutation.isPending}
            className="rounded-full bg-gradient-to-r from-brand-600 to-brand-500 px-5 py-2 text-sm font-medium text-white shadow-md shadow-brand-500/30 transition hover:brightness-105 disabled:opacity-50"
          >
            {saveMutation.isPending ? t('common.saving') : t('common.save')}
          </button>
        </div>
      </form>

      {/* Modal Dialog for PDF Upload */}
      {uploadModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 backdrop-blur-sm dark:bg-black/70">
          <div className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-2xl dark:bg-[#0b0c12]">
            <div className="flex items-center justify-between border-b border-slate-100 pb-4 dark:border-white/10">
              <div className="flex items-center gap-2.5">
                <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-red-100 text-red-600 dark:bg-red-500/10 dark:text-red-400">
                  <FileText className="h-5 w-5" />
                </span>
                <div>
                  <h3 className="text-base font-semibold text-slate-900 dark:text-white">
                    Subir documento PDF en Data Lake
                  </h3>
                  <p className="text-xs text-slate-500 dark:text-white/50">
                    Únicamente archivos en formato PDF (.pdf)
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setUploadModalOpen(false)}
                className="flex h-7 w-7 items-center justify-center rounded-full text-slate-400 hover:bg-slate-100 dark:hover:bg-white/10"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <div className="my-5 space-y-4">
              <div className="rounded-xl border border-dashed border-slate-300 bg-slate-50/50 p-6 text-center dark:border-white/20 dark:bg-white/5">
                <Upload className="mx-auto h-8 w-8 text-slate-400 dark:text-white/40" />
                <p className="mt-2 text-xs font-medium text-slate-700 dark:text-white/80">
                  Selecciona el archivo PDF de referencia para esta skill
                </p>
                <p className="mt-1 text-[11px] text-slate-400 dark:text-white/40">
                  Se guardará en contenedor: <span className="font-mono text-brand-600 dark:text-brand-300">{tenantId}</span>
                </p>
                <p className="text-[11px] font-mono text-slate-500 dark:text-white/50">
                  Ruta: skills/{skillId}/Documents/{selectedPdf ? selectedPdf.name : 'document.pdf'}
                </p>

                <input
                  type="file"
                  accept=".pdf,application/pdf"
                  onChange={handleFileChange}
                  className="mt-4 block w-full text-xs text-slate-500 file:mr-4 file:rounded-full file:border-0 file:bg-brand-50 file:px-4 file:py-2 file:text-xs file:font-semibold file:text-brand-700 hover:file:bg-brand-100 dark:file:bg-brand-500/20 dark:file:text-brand-300"
                />
              </div>

              {uploadError && (
                <div className="rounded-xl border border-red-200 bg-red-50 p-3 text-xs text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
                  {uploadError}
                </div>
              )}

              {selectedPdf && (
                <div className="flex items-center justify-between rounded-lg border border-emerald-200 bg-emerald-50/50 p-3 text-xs dark:border-emerald-500/20 dark:bg-emerald-500/10">
                  <div className="flex items-center gap-2 truncate">
                    <FileText className="h-4 w-4 text-red-500 shrink-0" />
                    <span className="font-medium text-slate-800 dark:text-white/90 truncate">{selectedPdf.name}</span>
                    <span className="text-slate-400 text-[11px]">({(selectedPdf.size / 1024).toFixed(1)} KB)</span>
                  </div>
                  <span className="rounded bg-emerald-200/50 px-2 py-0.5 text-[10px] font-semibold text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300">
                    PDF Válido
                  </span>
                </div>
              )}
            </div>

            <div className="flex justify-end gap-2 border-t border-slate-100 pt-4 dark:border-white/10">
              <button
                type="button"
                onClick={() => setUploadModalOpen(false)}
                className="rounded-full border border-slate-200 px-4 py-2 text-xs font-medium text-slate-600 hover:bg-slate-50 dark:border-white/10 dark:text-white/60 dark:hover:bg-white/5"
              >
                Cancelar
              </button>
              <button
                type="button"
                onClick={handlePdfUploadSubmit}
                disabled={!selectedPdf || uploadPdfMutation.isPending}
                className="inline-flex items-center gap-1.5 rounded-full bg-gradient-to-r from-brand-600 to-brand-500 px-5 py-2 text-xs font-medium text-white shadow-md shadow-brand-500/30 transition hover:brightness-105 disabled:opacity-50"
              >
                {uploadPdfMutation.isPending ? (
                  <>
                    <Loader2 className="h-3.5 w-3.5 animate-spin" />
                    Subiendo al Data Lake...
                  </>
                ) : (
                  <>
                    <Upload className="h-3.5 w-3.5" />
                    Subir a Data Lake
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
