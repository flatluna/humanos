import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import { Users, Pencil, Trash2, Search, GraduationCap } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { deactivatePerson, getDepartments, getTenantPeople } from '@/api/orgStructureApi';

const ORG_ROLE_LABELS: Record<string, string> = {
  Employee: 'Empleado',
  Manager: 'Gerente',
  HRAdmin: 'Administrador RRHH',
};

// PRIVACY: an employee's training plan/progress is their own private
// learning data — HR should not be able to browse into any employee's
// detailed course history from this directory. Until a real
// permission model exists for this (e.g. only the employee's own
// manager, or an explicit opt-in), the "Plan de capacitación" drill-down
// is restricted to this single seeded demo account (Alejandro Díaz) for
// testing/demo purposes. See /memories/repo for the real account behind
// this id (CIAM-linked so it can be used for real end-to-end testing).
const DEMO_CAREER_PLAN_PERSON_ID = '562cb73a-414b-47a8-957e-9f1950ae9622';

export function PeoplePage() {
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const [search, setSearch] = useState('');
  const [departmentFilter, setDepartmentFilter] = useState('');

  const peopleQuery = useQuery({
    queryKey: ['tenant-people', tenantId],
    queryFn: () => getTenantPeople(tenantId),
  });
  const departmentsQuery = useQuery({
    queryKey: ['departments', tenantId],
    queryFn: () => getDepartments(tenantId),
  });

  const deactivateMutation = useMutation({
    mutationFn: (personId: string) => deactivatePerson(personId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['tenant-people', tenantId] });
    },
  });

  const people = peopleQuery.data ?? [];
  const combinedError = deactivateMutation.error;

  const filteredPeople = useMemo(() => {
    const term = search.trim().toLowerCase();
    return people.filter((p) => {
      if (departmentFilter && p.departmentId !== departmentFilter) return false;
      if (!term) return true;
      return (
        (p.displayName ?? '').toLowerCase().includes(term) ||
        (p.email ?? '').toLowerCase().includes(term) ||
        (p.jobRoleTitle ?? '').toLowerCase().includes(term)
      );
    });
  }, [people, search, departmentFilter]);


  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">Personal</h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
          Directorio de empleados: asigna departamento, puesto, gerente y nivel de permisos.
        </p>
      </div>

      <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
        <div className="relative flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Buscar por nombre, correo o puesto…"
            className="w-full rounded-lg border border-slate-200 bg-white py-2 pl-9 pr-3 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
          />
        </div>
        <select
          value={departmentFilter}
          onChange={(e) => setDepartmentFilter(e.target.value)}
          className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
        >
          <option value="">Todos los departamentos</option>
          {departmentsQuery.data?.map((d) => (
            <option key={d.departmentId} value={d.departmentId}>
              {d.name}
            </option>
          ))}
        </select>
      </div>

      {combinedError && (
        <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
          {combinedError instanceof Error ? combinedError.message : 'Ocurrió un error inesperado.'}
        </div>
      )}

      <div className="space-y-2">
        {peopleQuery.isLoading && <p className="text-sm text-slate-400">Cargando…</p>}
        {peopleQuery.isSuccess && filteredPeople.length === 0 && (
          <div className="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-sky-200 bg-sky-50/50 p-8 text-center dark:border-sky-500/20 dark:bg-sky-500/5">
            <span className="flex h-12 w-12 items-center justify-center rounded-full bg-sky-100 text-sky-600 dark:bg-sky-500/15 dark:text-sky-300">
              <Users className="h-6 w-6" />
            </span>
            <p className="text-sm text-slate-500 dark:text-white/40">No se encontraron empleados con esos filtros.</p>
          </div>
        )}

        {filteredPeople.map((person) => {
          return (
            <div
              key={person.personId}
              className="rounded-2xl border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-white/5"
            >
              <div className="group flex items-start gap-3">
                <span className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-sky-100 text-sky-600 dark:bg-sky-500/15 dark:text-sky-300">
                  <Users className="h-4 w-4" />
                </span>
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="text-sm font-semibold text-slate-900 dark:text-white">
                      {person.displayName || person.email || 'Sin nombre'}
                    </p>
                    <span className="rounded-full bg-violet-100 px-2 py-0.5 text-xs font-medium text-violet-700 dark:bg-violet-500/15 dark:text-violet-300">
                      {ORG_ROLE_LABELS[person.orgRole] ?? person.orgRole}
                    </span>
                    {!person.isActive && (
                      <span className="rounded-full bg-slate-200 px-2 py-0.5 text-xs font-medium text-slate-600 dark:bg-white/10 dark:text-white/60">
                        Inactivo
                      </span>
                    )}
                  </div>
                  <p className="text-xs text-slate-400 dark:text-white/40">{person.email}</p>
                  <p className="mt-1 text-xs text-slate-500 dark:text-white/50">
                    {person.jobRoleTitle ?? 'Sin puesto asignado'} · {person.departmentName ?? 'Sin departamento'}
                    {person.managerDisplayName ? ` · Reporta a ${person.managerDisplayName}` : ''}
                  </p>
                </div>
                <div className="flex items-center gap-1 opacity-0 transition group-hover:opacity-100">
                  {person.personId === DEMO_CAREER_PLAN_PERSON_ID ? (
                    <button
                      type="button"
                      title="Plan de capacitación"
                      onClick={() => navigate(`/personal/${person.personId}/plan-capacitacion`)}
                      className="flex h-7 w-7 items-center justify-center rounded-full text-slate-400 hover:bg-indigo-100 hover:text-indigo-600 dark:hover:bg-indigo-500/10"
                    >
                      <GraduationCap className="h-3.5 w-3.5" />
                    </button>
                  ) : (
                    <button
                      type="button"
                      title="Datos privados del colaborador — no disponible desde este directorio"
                      disabled
                      className="flex h-7 w-7 cursor-not-allowed items-center justify-center rounded-full text-slate-200 dark:text-white/10"
                    >
                      <GraduationCap className="h-3.5 w-3.5" />
                    </button>
                  )}
                  <button
                    type="button"
                    title="Editar"
                    onClick={() => navigate(`/personal/${person.personId}`)}
                    className="flex h-7 w-7 items-center justify-center rounded-full text-slate-400 hover:bg-slate-200 dark:hover:bg-white/10"
                  >
                    <Pencil className="h-3.5 w-3.5" />
                  </button>
                  {person.isActive && person.personId !== user!.personId && (
                    <button
                      type="button"
                      title="Desactivar"
                      onClick={() => {
                        if (window.confirm(`¿Desactivar a ${person.displayName || person.email}?`)) {
                          deactivateMutation.mutate(person.personId);
                        }
                      }}
                      className="flex h-7 w-7 items-center justify-center rounded-full text-slate-400 hover:bg-red-100 hover:text-red-600 dark:hover:bg-red-500/10"
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </button>
                  )}
                </div>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
