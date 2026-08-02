import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useLocation } from 'react-router';
import { GraduationCap, Heart, Building2, ShieldCheck, Plus, Pencil, Trash2, X, RefreshCw } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { getJobRoles } from '@/api/orgStructureApi';
import {
  createSkill,
  deleteSkill,
  getSkills,
  syncSkillsFromJobRoles,
  updateSkill,
  type SaveSkillRequest,
  type SkillDto,
} from '@/api/skillsApi';

/** The 4 career-plan tracks this catalog is organized into — same
 * categories used by JobRoleDto's technicalSkills/softSkills/
 * departmentProcesses/hrProcesses fields and by PersonCareerPlan's 4
 * roadmap tracks. Each is a REAL, separate ROUTE/page —
 * `/skills/job-skills`, `/skills/soft-skills`, `/skills/department-
 * processes`, `/skills/hr-processes` (see routes/index.tsx), mirroring
 * the employee portal's CareerPlanPage.tsx convention (explicit routes
 * per category, not a query-param/hash tab): one page per category, its
 * own list of skills with add/edit/delete, not everything stacked on one
 * shared page. `SkillsPage` is still one shared component (rendered by
 * all 5 routes, including the bare `/skills` index which falls back to
 * the first tab); it derives which category is active from the current
 * URL path, same technique as CareerPlanPage's `pathSegment`. */
const CATEGORY_TABS = [
  {
    id: 'job-skills',
    label: 'Habilidades Técnicas',
    description: 'Conocimientos y competencias específicas del puesto.',
    icon: GraduationCap,
    color: 'amber',
  },
  {
    id: 'soft-skills',
    label: 'Habilidades Blandas',
    description: 'Competencias interpersonales y de comportamiento.',
    icon: Heart,
    color: 'violet',
  },
  {
    id: 'department-processes',
    label: 'Procesos de Departamento',
    description: 'Procedimientos internos propios del área o equipo.',
    icon: Building2,
    color: 'sky',
  },
  {
    id: 'hr-processes',
    label: 'Procesos de RRHH',
    description: 'Políticas y procesos corporativos aplicables a todos (p. ej. inducción, prevención de acoso).',
    icon: ShieldCheck,
    color: 'rose',
  },
] as const;

type CategoryTabId = (typeof CATEGORY_TABS)[number]['id'];

function isCategoryTabId(value: string | undefined): value is CategoryTabId {
  return CATEGORY_TABS.some((tab) => tab.id === value);
}

const COLOR_STYLES: Record<(typeof CATEGORY_TABS)[number]['color'], { chip: string; button: string }> = {
  amber: {
    chip: 'bg-amber-100 text-amber-600 dark:bg-amber-500/15 dark:text-amber-300',
    button: 'bg-gradient-to-r from-amber-500 to-amber-400 shadow-amber-500/30',
  },
  violet: {
    chip: 'bg-violet-100 text-violet-600 dark:bg-violet-500/15 dark:text-violet-300',
    button: 'bg-gradient-to-r from-violet-600 to-violet-500 shadow-violet-500/30',
  },
  sky: {
    chip: 'bg-sky-100 text-sky-600 dark:bg-sky-500/15 dark:text-sky-300',
    button: 'bg-gradient-to-r from-sky-600 to-sky-500 shadow-sky-500/30',
  },
  rose: {
    chip: 'bg-rose-100 text-rose-600 dark:bg-rose-500/15 dark:text-rose-300',
    button: 'bg-gradient-to-r from-rose-600 to-rose-500 shadow-rose-500/30',
  },
};

interface SkillFormState {
  skillId: string | null;
  name: string;
  description: string;
  jobRoleIds: string[];
}

const inputClass =
  'mt-1 w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white';
const labelClass = 'block text-xs font-medium text-slate-500 dark:text-white/50';

export function SkillsPage() {
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const queryClient = useQueryClient();
  const location = useLocation();

  const pathSegment = location.pathname.split('/').filter(Boolean).pop();
  const activeCategory: CategoryTabId = isCategoryTabId(pathSegment) ? pathSegment : 'job-skills';
  const activeTab = CATEGORY_TABS.find((tab) => tab.id === activeCategory)!;
  const activeStyles = COLOR_STYLES[activeTab.color];

  const [form, setForm] = useState<SkillFormState | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const skillsQuery = useQuery({
    queryKey: ['skills', tenantId, activeCategory],
    queryFn: () => getSkills(tenantId, activeCategory),
  });

  const jobRolesQuery = useQuery({
    queryKey: ['job-roles', tenantId],
    queryFn: () => getJobRoles(tenantId),
  });

  const closeForm = () => {
    setForm(null);
    setErrorMessage(null);
  };

  const saveMutation = useMutation({
    mutationFn: (body: SaveSkillRequest) =>
      form?.skillId ? updateSkill(form.skillId, body) : createSkill(tenantId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['skills', tenantId] });
      void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId] });
      closeForm();
    },
    onError: (error: unknown) => {
      setErrorMessage(error instanceof Error ? error.message : 'Ocurrió un error inesperado.');
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (skillId: string) => deleteSkill(skillId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['skills', tenantId] });
      void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId] });
    },
  });

  const syncMutation = useMutation({
    mutationFn: () => syncSkillsFromJobRoles(tenantId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['skills', tenantId] });
      void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId] });
    },
  });

  function openCreateForm() {
    setErrorMessage(null);
    setForm({ skillId: null, name: '', description: '', jobRoleIds: [] });
  }

  function openEditForm(skill: SkillDto) {
    setErrorMessage(null);
    setForm({
      skillId: skill.skillId,
      name: skill.name,
      description: skill.description ?? '',
      jobRoleIds: skill.jobRoles.map((r) => r.jobRoleId),
    });
  }

  function toggleJobRole(jobRoleId: string) {
    setForm((f) => {
      if (!f) return f;
      const isSelected = f.jobRoleIds.includes(jobRoleId);
      return {
        ...f,
        jobRoleIds: isSelected ? f.jobRoleIds.filter((id) => id !== jobRoleId) : [...f.jobRoleIds, jobRoleId],
      };
    });
  }

  function toggleSelectAllJobRoles() {
    setForm((f) => {
      if (!f) return f;
      const allIds = (jobRolesQuery.data ?? []).map((r) => r.jobRoleId);
      const allSelected = allIds.length > 0 && allIds.every((id) => f.jobRoleIds.includes(id));
      return { ...f, jobRoleIds: allSelected ? [] : allIds };
    });
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!form || !form.name.trim()) return;

    saveMutation.mutate({
      category: activeCategory,
      name: form.name.trim(),
      description: form.description.trim() || null,
      jobRoleIds: form.jobRoleIds,
    });
  }

  const allJobRoles = jobRolesQuery.data ?? [];
  const allSelected = form ? allJobRoles.length > 0 && allJobRoles.every((r) => form.jobRoleIds.includes(r.jobRoleId)) : false;
  const skills = skillsQuery.data ?? [];
  const ActiveIcon = activeTab.icon;

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">Catálogo de Skills</h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
          Define una skill, proceso o política una sola vez y vincúlala a uno o varios puestos — cambios aquí se
          reflejan automáticamente en las descripciones de puesto y planes de capacitación.
        </p>
      </div>

      <div className="flex items-center justify-between gap-3 rounded-2xl border border-dashed border-slate-200 bg-slate-50/60 px-4 py-3 dark:border-white/10 dark:bg-white/5">
        <p className="text-xs text-slate-500 dark:text-white/50">
          ¿No ves las skills que ya definiste en Roles y Puestos? Sincroniza el catálogo para traerlas todas desde ahí.
        </p>
        <button
          type="button"
          onClick={() => syncMutation.mutate()}
          disabled={syncMutation.isPending}
          className="inline-flex shrink-0 items-center gap-1.5 rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-600 shadow-sm transition hover:bg-slate-50 disabled:opacity-50 dark:border-white/10 dark:bg-white/5 dark:text-white/70 dark:hover:bg-white/10"
        >
          <RefreshCw className={`h-3.5 w-3.5 ${syncMutation.isPending ? 'animate-spin' : ''}`} />
          {syncMutation.isPending ? 'Sincronizando…' : 'Sincronizar desde Roles y Puestos'}
        </button>
      </div>

      {syncMutation.isSuccess && (
        <div className="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-2.5 text-xs text-emerald-700 dark:border-emerald-500/20 dark:bg-emerald-500/10 dark:text-emerald-300">
          Sincronizado con {syncMutation.data.syncedJobRoleCount} puesto(s).
        </div>
      )}

      <div className="flex flex-wrap gap-2 border-b border-slate-200 pb-4 dark:border-white/10">
        {CATEGORY_TABS.map((tab) => {
          const Icon = tab.icon;
          const isActive = tab.id === activeCategory;
          const tabStyles = COLOR_STYLES[tab.color];
          return (
            <Link
              key={tab.id}
              to={`/skills/${tab.id}`}
              className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-medium transition ${
                isActive ? `text-white shadow-md ${tabStyles.button}` : `${tabStyles.chip} hover:brightness-95`
              }`}
            >
              <Icon className="h-3.5 w-3.5" />
              {tab.label}
            </Link>
          );
        })}
      </div>

      <section className="space-y-3 rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5">
        <div className="flex items-start justify-between gap-3">
          <div className="flex items-start gap-3">
            <span className={`mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${activeStyles.chip}`}>
              <ActiveIcon className="h-4 w-4" />
            </span>
            <div>
              <h2 className="text-sm font-semibold text-slate-900 dark:text-white">{activeTab.label}</h2>
              <p className="text-xs text-slate-400 dark:text-white/40">{activeTab.description}</p>
            </div>
          </div>
          <button
            type="button"
            onClick={openCreateForm}
            className={`inline-flex shrink-0 items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-medium text-white shadow-md transition hover:brightness-105 ${activeStyles.button}`}
          >
            <Plus className="h-3.5 w-3.5" />
            Nueva
          </button>
        </div>

        {skillsQuery.isLoading && <p className="pl-12 text-xs text-slate-400 dark:text-white/40">Cargando…</p>}

        {skillsQuery.isSuccess && skills.length === 0 && (
          <p className="pl-12 text-xs text-slate-400 dark:text-white/40">Aún no hay skills en esta categoría.</p>
        )}

        {skills.length > 0 && (
          <div className="space-y-2 pl-12">
            {skills.map((skill) => (
              <div
                key={skill.skillId}
                className="group flex items-start justify-between gap-3 rounded-xl border border-slate-200 bg-slate-50/50 px-4 py-3 dark:border-white/10 dark:bg-white/5"
              >
                <div className="min-w-0">
                  <p className="text-sm font-medium text-slate-900 dark:text-white">{skill.name}</p>
                  {skill.description && (
                    <p className="mt-0.5 text-xs text-slate-500 dark:text-white/50">{skill.description}</p>
                  )}
                  <div className="mt-1.5 flex flex-wrap gap-1">
                    {skill.jobRoles.length === 0 ? (
                      <span className="rounded-full bg-slate-100 px-2 py-0.5 text-[11px] text-slate-400 dark:bg-white/10 dark:text-white/30">
                        Sin puestos vinculados
                      </span>
                    ) : (
                      skill.jobRoles.map((role) => (
                        <span
                          key={role.jobRoleId}
                          className="rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-600 dark:bg-white/10 dark:text-white/60"
                        >
                          {role.jobRoleTitle}
                        </span>
                      ))
                    )}
                  </div>
                </div>
                <div className="flex shrink-0 items-center gap-1 opacity-0 transition group-hover:opacity-100">
                  <button
                    type="button"
                    title="Editar"
                    onClick={() => openEditForm(skill)}
                    className="flex h-7 w-7 items-center justify-center rounded-full text-slate-400 hover:bg-slate-200 dark:hover:bg-white/10"
                  >
                    <Pencil className="h-3.5 w-3.5" />
                  </button>
                  <button
                    type="button"
                    title="Eliminar"
                    onClick={() => deleteMutation.mutate(skill.skillId)}
                    className="flex h-7 w-7 items-center justify-center rounded-full text-slate-400 hover:bg-red-100 hover:text-red-600 dark:hover:bg-red-500/10"
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      {form && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4 dark:bg-black/60">
          <div className="max-h-[85vh] w-full max-w-lg overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl dark:bg-[#0b0c12]">
            <div className="mb-4 flex items-center justify-between">
              <h3 className="text-base font-semibold text-slate-900 dark:text-white">
                {form.skillId ? 'Editar skill' : 'Nueva skill'} · {activeTab.label}
              </h3>
              <button
                type="button"
                onClick={closeForm}
                className="flex h-7 w-7 items-center justify-center rounded-full text-slate-400 hover:bg-slate-100 dark:hover:bg-white/10"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {errorMessage && (
              <div className="mb-4 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
                {errorMessage}
              </div>
            )}

            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label className={labelClass}>Nombre</label>
                <input
                  className={inputClass}
                  value={form.name}
                  onChange={(e) => setForm((f) => (f ? { ...f, name: e.target.value } : f))}
                  required
                  autoFocus
                />
              </div>
              <div>
                <label className={labelClass}>Descripción (opcional)</label>
                <textarea
                  className={inputClass}
                  rows={2}
                  value={form.description}
                  onChange={(e) => setForm((f) => (f ? { ...f, description: e.target.value } : f))}
                />
              </div>
              <div>
                <div className="flex items-center justify-between">
                  <label className={labelClass}>Puestos vinculados</label>
                  <button
                    type="button"
                    onClick={toggleSelectAllJobRoles}
                    className="text-xs font-medium text-brand-600 hover:underline dark:text-brand-400"
                  >
                    {allSelected ? 'Ninguno' : 'Seleccionar todos'}
                  </button>
                </div>
                <div className="mt-1 max-h-48 space-y-1 overflow-y-auto rounded-lg border border-slate-200 p-2 dark:border-white/10">
                  {allJobRoles.length === 0 && (
                    <p className="px-1 py-1 text-xs text-slate-400">No hay puestos definidos aún.</p>
                  )}
                  {allJobRoles.map((role) => (
                    <label
                      key={role.jobRoleId}
                      className="flex cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 text-sm text-slate-700 hover:bg-slate-50 dark:text-white/70 dark:hover:bg-white/5"
                    >
                      <input
                        type="checkbox"
                        checked={form.jobRoleIds.includes(role.jobRoleId)}
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

              <div className="flex justify-end gap-2 pt-2">
                <button
                  type="button"
                  onClick={closeForm}
                  className="rounded-full border border-slate-200 px-4 py-2 text-sm font-medium text-slate-600 hover:bg-slate-50 dark:border-white/10 dark:text-white/60 dark:hover:bg-white/5"
                >
                  Cancelar
                </button>
                <button
                  type="submit"
                  disabled={saveMutation.isPending}
                  className="rounded-full bg-gradient-to-r from-brand-600 to-brand-500 px-4 py-2 text-sm font-medium text-white shadow-md shadow-brand-500/30 transition hover:brightness-105 disabled:opacity-50"
                >
                  {saveMutation.isPending ? 'Guardando…' : 'Guardar'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
