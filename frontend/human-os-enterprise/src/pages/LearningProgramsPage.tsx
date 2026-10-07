import { useEffect, useMemo, useState } from 'react';
import { BookOpen, Check, Loader2, Search, X } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { getJobRoles, type JobRoleDto } from '@/api/orgStructureApi';
import { getSkills, type SkillDto } from '@/api/skillsApi';
import {
  createLearningProgram,
  addStructuredCourseToProgram,
  getLearningCapabilities,
  getLearningProgram,
  getLearningPrograms,
  removeStructuredCourseFromProgram,
  updateLearningProgramCourses,
  updateLearningProgramMetadata,
  type LearningCapability,
  type LearningProgram,
  type LearningProgramCourse,
} from '@/api/learningProgramsApi';

const TABS = [
  { id: 'in-role', label: 'First 90 days', skillFields: ['startInYourRoleTechnicalSkills', 'startInYourRoleSoftSkills', 'startInYourRoleDepartmentProcesses', 'startInYourRoleHrProcesses'] as const },
  { id: 'every-day', label: 'Every day', skillFields: ['technicalSkills', 'softSkills', 'tools'] as const },
] as const;

type TabId = (typeof TABS)[number]['id'];
type CourseMap = Record<TabId, LearningProgramCourse[]>;

const emptyCourses: CourseMap = { 'in-role': [], 'every-day': [] };

function pendingCoursesKey(tenantId: string, roleId: string): string {
  return `learning-program-pending:${tenantId}:${roleId}`;
}

function normalize(value: string): string {
  return value.trim().toLowerCase().replace(/[.\s]+/g, ' ');
}

function categoryLabel(category: string | null): string {
  if (category === 'soft-skills') return 'Soft skill';
  if (category === 'job-skills') return 'Técnica';
  if (category === 'department-processes') return 'Proceso';
  return category ?? 'General';
}

function categoryClasses(category: string | null): string {
  if (category === 'soft-skills') {
    return 'border-fuchsia-200 bg-fuchsia-50 text-fuchsia-800 dark:border-fuchsia-500/30 dark:bg-fuchsia-500/10 dark:text-fuchsia-200';
  }
  if (category === 'job-skills') {
    return 'border-sky-200 bg-sky-50 text-sky-800 dark:border-sky-500/30 dark:bg-sky-500/10 dark:text-sky-200';
  }
  if (category === 'department-processes' || category === 'hr-processes') {
    return 'border-amber-200 bg-amber-50 text-amber-800 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-200';
  }
  return 'border-slate-200 bg-slate-50 text-slate-600 dark:border-white/10 dark:bg-white/5 dark:text-slate-300';
}

function programName(role: JobRoleDto, tab: TabId): string {
  return `${role.title} · ${TABS.find((item) => item.id === tab)?.label}`;
}

export function LearningProgramsPage() {
  const { user } = useAuth();
  const tenantId = user?.tenantId;
  const [roles, setRoles] = useState<JobRoleDto[]>([]);
  const [programs, setPrograms] = useState<LearningProgram[]>([]);
  const [capabilities, setCapabilities] = useState<LearningCapability[]>([]);
  const [roleSkills, setRoleSkills] = useState<SkillDto[]>([]);
  const [selectedRoleId, setSelectedRoleId] = useState('');
  const [activeTab, setActiveTab] = useState<TabId>('in-role');
  const [courses, setCourses] = useState<CourseMap>(emptyCourses);
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState<'all' | 'soft-skills' | 'job-skills'>('all');
  const [loading, setLoading] = useState(true);
  const [loadingRole, setLoadingRole] = useState(false);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const selectedRole = roles.find((role) => role.jobRoleId === selectedRoleId) ?? null;
  const currentTab = TABS.find((tab) => tab.id === activeTab)!;

  useEffect(() => {
    if (!tenantId) return;
    Promise.all([getJobRoles(tenantId), getLearningCapabilities(), getLearningPrograms(), getSkills(tenantId)])
      .then(([roleList, capabilityList, programList, skillList]) => {
        setRoles(roleList.filter((role) => role.isActive));
        setCapabilities(capabilityList.filter((capability) => capability.isActive));
        setPrograms(programList);
        setRoleSkills(skillList.filter((skill) => skill.isActive));
        setSelectedRoleId((current) => current || roleList[0]?.jobRoleId || '');
      })
      .catch((error) => {
        console.error('Could not load role learning configuration:', error);
        setMessage('No fue posible cargar roles y capabilities.');
      })
      .finally(() => setLoading(false));
  }, [tenantId]);

  const skillsForTab = useMemo(() => {
    if (!selectedRole) return [];
    return Array.from(
      new Set([
        ...selectedRole.technicalSkills,
        ...selectedRole.softSkills,
      ].filter(Boolean)),
    );
  }, [selectedRole]);

  const roleSkillGroups = useMemo(() => {
    if (!selectedRole) return [];
    return Array.from(new Map(
      ([
        ['technicalSkills', selectedRole.technicalSkills],
        ['softSkills', selectedRole.softSkills],
      ] as const).flatMap(([field, fieldSkills]) => fieldSkills.map((name) => [
        name,
        {
          name,
          category: field.toLowerCase().includes('soft') ? 'soft-skills' : 'job-skills',
        },
      ] as const)),
    ).values());
  }, [selectedRole]);

  const roleCourseCapabilities = useMemo<LearningCapability[]>(() => {
    if (!selectedRole) return [];
    const requiredNames = new Set(skillsForTab.map(normalize));
    const requiredSkills = roleSkills.filter((skill) =>
      skill.jobRoles.some((link) => link.jobRoleId === selectedRole.jobRoleId)
      && requiredNames.has(normalize(skill.name)),
    );
    const skillCourses = roleSkillGroups.map((skill) => ({
      capabilityId: `role-skill-${normalize(skill.name).replace(/[^a-z0-9]+/g, '-')}`,
      code: 'ROLE-SKILL',
      name: skill.name,
      description: 'Curso requerido por el rol. El contenido puede crearse después.',
      category: skill.category,
      learningMode: 'RoleSkill',
      contentLanguageCode: 'en',
      targetJobRoleId: selectedRole.jobRoleId,
      targetSkill: skill.name,
      isActive: true,
    }));
    const matchedCourses = requiredSkills.flatMap((skill) => (skill.matchedCourses ?? []).map((course) => ({
      capabilityId: course.courseId,
      code: course.courseId.slice(0, 8),
      name: course.courseName,
      description: null,
      category: skill.category,
      learningMode: course.courseType,
      contentLanguageCode: course.languageCode,
      targetJobRoleId: selectedRole.jobRoleId,
      targetSkill: skill.name,
      isActive: true,
    })));
    return [...skillCourses, ...matchedCourses];
  }, [roleSkillGroups, roleSkills, selectedRole, skillsForTab]);

  const catalogCapabilities = useMemo(() => {
    const byId = new Map<string, LearningCapability>();
    [...roleCourseCapabilities, ...capabilities.filter((capability) => capability.targetJobRoleId === selectedRole?.jobRoleId)].forEach((capability) => byId.set(capability.capabilityId, capability));
    return [...byId.values()];
  }, [capabilities, roleCourseCapabilities, selectedRole]);

  const roleSkillNames = useMemo(() => {
    const capabilitySkills = capabilities
      .filter((capability) => capability.targetSkill)
      .map((capability) => capability.targetSkill!)
      .filter(Boolean);
    const assignedRoleSkills = roleSkills
      .filter((skill) => skill.jobRoles.some((link) => link.jobRoleId === selectedRole?.jobRoleId))
      .filter((skill) => skillsForTab.some((roleSkill) => normalize(roleSkill) === normalize(skill.name)))
      .map((skill) => skill.name);
    return Array.from(new Set([...skillsForTab, ...assignedRoleSkills, ...capabilitySkills]));
  }, [capabilities, roleSkills, selectedRole, skillsForTab]);

  useEffect(() => {
    if (!selectedRole) return;
    let cancelled = false;
    setLoadingRole(true);
    Promise.all(
      TABS.map(async (tab) => {
        const program = programs.find((item) => item.name === programName(selectedRole, tab.id));
        if (!program) return [tab.id, []] as const;
        const detail = await getLearningProgram(program.programId);
        return [tab.id, detail.capabilities ?? []] as const;
      }),
    )
      .then((entries) => {
        if (!cancelled) {
          const loaded = Object.fromEntries(entries) as CourseMap;
          const pendingRaw = window.localStorage.getItem(pendingCoursesKey(tenantId!, selectedRole.jobRoleId));
          if (pendingRaw) {
            try {
              const pending = JSON.parse(pendingRaw) as Partial<CourseMap>;
              TABS.forEach((tab) => {
                const pendingForTab = pending[tab.id] ?? [];
                loaded[tab.id] = [
                  ...loaded[tab.id],
                  ...pendingForTab.filter((course) => !loaded[tab.id].some((item) => item.capabilityId === course.capabilityId)),
                ];
              });
            } catch (error) {
              console.error('Could not restore pending role courses:', error);
            }
          }
          const seen = new Set<string>();
          const exclusive = Object.fromEntries(
            TABS.map((tab) => {
              const uniqueCourses = loaded[tab.id].filter((course) => {
                if (seen.has(course.capabilityId)) return false;
                seen.add(course.capabilityId);
                return true;
              });
              return [tab.id, uniqueCourses];
            }),
          ) as CourseMap;
          setCourses(exclusive);
        }
      })
      .catch((error) => {
        console.error('Could not load role learning programs:', error);
        if (!cancelled) setMessage('No fue posible cargar los cursos de este puesto.');
      })
      .finally(() => {
        if (!cancelled) setLoadingRole(false);
      });
    return () => {
      cancelled = true;
    };
  }, [programs, selectedRoleId]);

  const isSelectedAnywhere = (capabilityId: string) =>
    TABS.some((tab) => courses[tab.id].some((course) => course.capabilityId === capabilityId));

  const availableCapabilities = useMemo(() => {
    if (!selectedRole) return [];
    const skillNames = roleSkillNames.map(normalize);
    const term = normalize(search);
    return catalogCapabilities.filter((capability) => {
      const roleMatch = capability.targetJobRoleId === selectedRole.jobRoleId;
      const skillMatch = capability.targetSkill
        ? skillNames.some((skill) => normalize(capability.targetSkill!).includes(skill) || skill.includes(normalize(capability.targetSkill!)))
        : skillNames.some((skill) => normalize(capability.name).includes(skill));
      const fallbackMatch = false;
      const categoryMatch = category === 'all' || capability.category === category;
      const textMatch = !term || normalize(capability.name).includes(term) || normalize(capability.code).includes(term);
      return (roleMatch || skillMatch || fallbackMatch) && categoryMatch && textMatch && !isSelectedAnywhere(capability.capabilityId);
    });
  }, [catalogCapabilities, category, roleSkillNames, search, selectedRole, courses]);

  const assignCourse = (capability: LearningCapability) => {
    const selectedInOtherTab = TABS.some((tab) =>
      tab.id !== activeTab && courses[tab.id].some((course) => course.capabilityId === capability.capabilityId),
    );
    if (selectedInOtherTab) return;
    setCourses((current) => ({
      ...current,
      [activeTab]: current[activeTab].some((course) => course.capabilityId === capability.capabilityId)
        ? current[activeTab]
        : [...current[activeTab], {
            programCapabilityId: `new-${capability.capabilityId}`,
            capabilityId: capability.capabilityId,
            capabilityCode: capability.code,
            capabilityName: capability.name,
            subjectCode: null,
            sortOrder: current[activeTab].length,
            isRequired: true,
            courseType: capability.learningMode,
          }],
    }));
  };

  const isSelected = (capabilityId: string) =>
    courses[activeTab].some((course) => course.capabilityId === capabilityId);

  const toggleCourse = (capability: LearningCapability) => {
    if (isSelected(capability.capabilityId)) {
      setCourses((current) => ({
        ...current,
        [activeTab]: current[activeTab].filter((course) => course.capabilityId !== capability.capabilityId),
      }));
    } else {
      assignCourse(capability);
    }
  };

  const save = async () => {
    if (!selectedRole) return;
    setSaving(true);
    setMessage(null);
    try {
      const updatedPrograms = [...programs];
      for (const tab of TABS) {
        let program = updatedPrograms.find((item) => item.name === programName(selectedRole, tab.id));
        if (!program) {
          program = await createLearningProgram(programName(selectedRole, tab.id));
          updatedPrograms.push(program);
        }
        const currentCourses = courses[tab.id];
        const pendingForProgram = currentCourses.filter((course) => course.courseType === 'RoleSkill').map((course) => ({
          capabilityId: course.capabilityId,
          capabilityName: course.capabilityName,
          capabilityCode: course.capabilityCode,
          courseType: 'RoleSkill',
          sortOrder: course.sortOrder,
        }));
        await updateLearningProgramMetadata(program.programId, {
          name: program.name,
          requirements: JSON.stringify({ scheduledRoleSkills: pendingForProgram }),
        });
        await updateLearningProgramCourses(program.programId, currentCourses.filter((course) => course.courseType !== 'StructuredLearning' && course.courseType !== 'RoleSkill').map((course, index) => ({
          capabilityId: course.capabilityId,
          sortOrder: index,
        })));
        const previous = await getLearningProgram(program.programId);
        const previousStructured = previous.capabilities.filter((course) => course.courseType === 'StructuredLearning');
        await Promise.all([
          ...currentCourses
            .filter((course) => course.courseType === 'StructuredLearning' && !previousStructured.some((item) => item.capabilityId === course.capabilityId))
            .map((course) => addStructuredCourseToProgram(course.capabilityId, program!.programId)),
          ...previousStructured
            .filter((course) => !currentCourses.some((item) => item.courseType === 'StructuredLearning' && item.capabilityId === course.capabilityId))
            .map((course) => removeStructuredCourseFromProgram(course.capabilityId, program!.programId)),
        ]);
      }
      setPrograms(updatedPrograms);
      const pendingCourses: CourseMap = {
        'in-role': courses['in-role'].filter((course) => course.courseType === 'RoleSkill'),
        'every-day': courses['every-day'].filter((course) => course.courseType === 'RoleSkill'),
      };
      window.localStorage.setItem(
        pendingCoursesKey(tenantId!, selectedRole.jobRoleId),
        JSON.stringify(pendingCourses),
      );
      const pendingRoleSkills = TABS.reduce(
        (count, tab) => count + courses[tab.id].filter((course) => course.courseType === 'RoleSkill').length,
        0,
      );
      setMessage(
        pendingRoleSkills > 0
          ? `Programación guardada para ${selectedRole.title}. ${pendingRoleSkills} skills aún esperan que se cree su capability para poder persistirse como cursos.`
          : `Programación guardada para ${selectedRole.title}.`,
      );
    } catch (error) {
      console.error('Could not save role learning configuration:', error);
      setMessage('No fue posible guardar la programación.');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return <div className="flex items-center gap-2 p-8 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" /> Cargando puestos y catálogo…</div>;
  }

  return (
    <div className="mx-auto max-w-7xl space-y-6">
      <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="flex items-center gap-3">
          <span className="flex h-11 w-11 items-center justify-center rounded-2xl bg-violet-100 text-violet-600 dark:bg-violet-500/15 dark:text-violet-300"><BookOpen className="h-5 w-5" /></span>
          <div><h1 className="text-2xl font-semibold text-slate-900 dark:text-white">Programar cursos por puesto</h1><p className="mt-1 text-sm text-slate-500 dark:text-white/50">Elige un rol y define qué capabilities aplican en cada momento.</p></div>
        </div>
        <button type="button" onClick={save} disabled={!selectedRole || saving} className="inline-flex items-center justify-center gap-2 rounded-xl bg-violet-600 px-4 py-2 text-sm font-semibold text-white shadow-sm disabled:opacity-50"><Check className="h-4 w-4" /> {saving ? 'Guardando…' : 'Guardar programación'}</button>
      </header>

      {message && <div className="rounded-xl border border-violet-200 bg-violet-50 px-4 py-3 text-sm text-violet-800 dark:border-violet-500/20 dark:bg-violet-500/10 dark:text-violet-200">{message}</div>}

      <section className="rounded-2xl border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-slate-900/40">
        <label className="block text-xs font-semibold uppercase tracking-wider text-slate-400">1. Selecciona el puesto / rol</label>
        <select value={selectedRoleId} onChange={(event) => setSelectedRoleId(event.target.value)} className="mt-2 w-full rounded-xl border border-slate-200 bg-transparent px-3 py-3 text-sm font-medium outline-none focus:border-violet-500 dark:border-white/10 sm:max-w-xl">
          <option value="">Selecciona un rol…</option>
          {roles.map((role) => <option key={role.jobRoleId} value={role.jobRoleId}>{role.title}{role.levelLabel ? ` · ${role.levelLabel}` : ''}</option>)}
        </select>
      </section>

      {selectedRole && (
        <section className="space-y-4">
          <div className="flex gap-2 overflow-x-auto border-b border-slate-200 dark:border-white/10">
            {TABS.map((tab) => <button key={tab.id} type="button" onClick={() => setActiveTab(tab.id)} className={`whitespace-nowrap border-b-2 px-4 py-3 text-sm font-semibold ${activeTab === tab.id ? 'border-violet-600 text-violet-700 dark:text-violet-300' : 'border-transparent text-slate-500'}`}>{tab.label}</button>)}
          </div>

          <div className="space-y-4">
            <p className="text-sm text-slate-500">Selecciona cursos en la columna izquierda o arrástralos a la columna derecha.</p>
            <div className="rounded-2xl border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-slate-900/40">
              <div className="mb-2 flex items-center justify-between gap-3">
                <h3 className="text-sm font-semibold text-slate-800 dark:text-slate-100">Competencias de {selectedRole.title}</h3>
                <span className="text-xs text-slate-400">{roleSkillGroups.length} en este tab</span>
              </div>
              <div className="flex max-h-36 flex-wrap gap-2 overflow-y-auto">
                {roleSkillGroups.map((skill) => <span key={skill.name} className={`rounded-full border px-2.5 py-1 text-xs font-medium ${categoryClasses(skill.category)}`}>{skill.name}</span>)}
                {roleSkillGroups.length === 0 && <span className="text-xs text-slate-500">Este tab todavía no tiene competencias configuradas.</span>}
              </div>
            </div>
            <div className="flex flex-wrap items-center gap-2 text-xs">
              <span className="font-semibold text-slate-500">Tipo de competencia:</span>
              <span className={`rounded-full border px-2.5 py-1 font-medium ${categoryClasses('job-skills')}`}>Técnicas</span>
              <span className={`rounded-full border px-2.5 py-1 font-medium ${categoryClasses('soft-skills')}`}>Soft skills</span>
              <span className={`rounded-full border px-2.5 py-1 font-medium ${categoryClasses('department-processes')}`}>Procesos</span>
              <span className={`rounded-full border px-2.5 py-1 font-medium ${categoryClasses(null)}`}>General</span>
            </div>
            <div className="flex flex-col gap-4 lg:grid lg:grid-cols-2">
              <div
                className="min-h-[360px] rounded-2xl border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-slate-900/40"
                onDragOver={(event) => event.preventDefault()}
              >
                <div className="mb-3 flex items-center justify-between"><div><h3 className="font-semibold">Disponibles</h3><p className="text-xs text-slate-500">{availableCapabilities.filter((item) => !isSelected(item.capabilityId)).length} cursos</p></div><span className="text-xs text-slate-400">Catálogo</span></div>
                <div className="mb-3 flex flex-col gap-2 sm:flex-row">
                  <div className="relative flex-1"><Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" /><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Buscar curso…" className="w-full rounded-xl border border-slate-200 bg-transparent py-2 pl-9 pr-3 text-sm outline-none focus:border-violet-500 dark:border-white/10" /></div>
                  <select value={category} onChange={(event) => setCategory(event.target.value as typeof category)} className="rounded-xl border border-slate-200 bg-transparent px-3 py-2 text-sm dark:border-white/10"><option value="all">Todas</option><option value="soft-skills">Soft skills</option><option value="job-skills">Técnicas</option></select>
                </div>
                <div className="space-y-2">
                  {availableCapabilities.filter((item) => !isSelected(item.capabilityId)).map((capability) => <label key={capability.capabilityId} draggable onDragStart={(event) => event.dataTransfer.setData('capability-id', capability.capabilityId)} className={`flex cursor-grab items-center gap-3 rounded-xl border p-3 text-left hover:border-violet-400 ${categoryClasses(capability.category)}`}><input type="checkbox" checked={false} onChange={() => toggleCourse(capability)} className="accent-violet-600" /><span className="min-w-0 flex-1"><span className="block truncate text-sm font-medium">{capability.name}</span><span className="block text-xs opacity-75">{capability.code} · {categoryLabel(capability.category)}</span></span></label>)}
                  {availableCapabilities.filter((item) => !isSelected(item.capabilityId)).length === 0 && <p className="py-8 text-center text-sm text-slate-500">No hay cursos disponibles con este filtro.</p>}
                </div>
              </div>
              <div
                className="min-h-[360px] rounded-2xl border-2 border-dashed border-violet-300 bg-violet-50/40 p-4 dark:border-violet-500/30 dark:bg-violet-500/5"
                onDragOver={(event) => event.preventDefault()}
                onDrop={(event) => {
                  const capability = catalogCapabilities.find((item) => item.capabilityId === event.dataTransfer.getData('capability-id'));
                  if (capability) assignCourse(capability);
                }}
              >
                <div className="mb-3 flex items-center justify-between"><div><h3 className="font-semibold">Seleccionados</h3><p className="text-xs text-slate-500">{courses[activeTab].length} cursos para {currentTab.label}</p></div>{loadingRole && <Loader2 className="h-4 w-4 animate-spin text-violet-500" />}</div>
                <div className="space-y-2">
                  {courses[activeTab].map((course, index) => {
                    const courseCategory = catalogCapabilities.find((capability) => capability.capabilityId === course.capabilityId)?.category ?? null;
                    return <div key={course.capabilityId} draggable onDragStart={(event) => event.dataTransfer.setData('capability-id', course.capabilityId)} className={`flex items-center gap-3 rounded-xl border px-3 py-3 ${categoryClasses(courseCategory)}`}><span className="w-5 text-xs opacity-60">{index + 1}</span><span className="flex-1"><span className="block text-sm font-medium">{course.capabilityName}</span><span className="block text-xs opacity-75">{categoryLabel(courseCategory)}</span></span><button type="button" onClick={() => toggleCourse({ capabilityId: course.capabilityId } as LearningCapability)} aria-label="Quitar curso"><X className="h-4 w-4 opacity-60 hover:text-rose-500" /></button></div>;
                  })}
                  {courses[activeTab].length === 0 && <p className="py-12 text-center text-sm text-violet-700/70 dark:text-violet-300/70">Arrastra aquí los cursos o selecciónalos con el checkbox.</p>}
                </div>
              </div>
            </div>
          </div>
        </section>
      )}
    </div>
  );
}
