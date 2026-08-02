import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Briefcase, Trash2, Pencil } from 'lucide-react';
import { useNavigate } from 'react-router';
import { useAuth } from '@/auth/AuthContext';
import { getDepartments, deleteJobRole, getJobRoles } from '@/api/orgStructureApi';

export function RolesPage() {
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const departmentsQuery = useQuery({
    queryKey: ['departments', tenantId],
    queryFn: () => getDepartments(tenantId),
  });
  const jobRolesQuery = useQuery({
    queryKey: ['job-roles', tenantId],
    queryFn: () => getJobRoles(tenantId),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteJobRole(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId] });
    },
  });

  const hasNoDepartments = departmentsQuery.isSuccess && (departmentsQuery.data?.length ?? 0) === 0;

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">Roles y Puestos</h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
            Descripciones de puesto completas: responsabilidades, requisitos, condiciones de trabajo y más.
          </p>
        </div>
        <button
          type="button"
          disabled={hasNoDepartments}
          onClick={() => navigate('/roles/nuevo')}
          className="inline-flex items-center gap-2 rounded-full bg-gradient-to-r from-amber-500 to-amber-400 px-4 py-2.5 text-sm font-medium text-white shadow-md shadow-amber-500/30 transition hover:brightness-105 disabled:opacity-50 disabled:shadow-none"
        >
          <Plus className="h-4 w-4" />
          Nuevo puesto
        </button>
      </div>

      {hasNoDepartments && (
        <div className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-700 dark:border-amber-500/20 dark:bg-amber-500/10 dark:text-amber-300">
          Crea primero un departamento en <a href="/organizacion" className="underline">Organización</a> antes de
          definir puestos.
        </div>
      )}

      {deleteMutation.error && (
        <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
          {deleteMutation.error instanceof Error ? deleteMutation.error.message : 'Ocurrió un error inesperado.'}
        </div>
      )}

      <div className="space-y-2">
        {jobRolesQuery.isLoading && <p className="text-sm text-slate-400">Cargando…</p>}
        {jobRolesQuery.isSuccess && jobRolesQuery.data.length === 0 && (
          <div className="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-amber-200 bg-amber-50/50 p-8 text-center dark:border-amber-500/20 dark:bg-amber-500/5">
            <span className="flex h-12 w-12 items-center justify-center rounded-full bg-amber-100 text-amber-600 dark:bg-amber-500/15 dark:text-amber-300">
              <Briefcase className="h-6 w-6" />
            </span>
            <p className="text-sm text-slate-500 dark:text-white/40">Aún no hay puestos definidos.</p>
          </div>
        )}
        {jobRolesQuery.data?.map((role) => (
          <div
            key={role.jobRoleId}
            className="group flex items-start gap-3 rounded-2xl border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-white/5"
          >
            <span className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-amber-100 text-amber-600 dark:bg-amber-500/15 dark:text-amber-300">
              <Briefcase className="h-4 w-4" />
            </span>
            <div className="min-w-0 flex-1 cursor-pointer" onClick={() => navigate(`/roles/${role.jobRoleId}`)}>
              <div className="flex flex-wrap items-center gap-2">
                <p className="text-sm font-semibold text-slate-900 dark:text-white">{role.title}</p>
                {role.levelLabel && (
                  <span className="rounded-full bg-violet-100 px-2 py-0.5 text-xs font-medium text-violet-700 dark:bg-violet-500/15 dark:text-violet-300">
                    {role.levelLabel}
                  </span>
                )}
                {role.reportsToJobRoleTitle && (
                  <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-600 dark:bg-white/10 dark:text-white/60">
                    Reporta a {role.reportsToJobRoleTitle}
                  </span>
                )}
              </div>
              <p className="text-xs text-slate-400 dark:text-white/40">
                {role.departmentName} · {role.personCount} persona(s)
                {role.hasDirectReports ? ' · con personal a cargo' : ''}
              </p>
              {role.purpose && <p className="mt-2 text-sm text-slate-600 dark:text-white/60">{role.purpose}</p>}
            </div>
            <div className="flex items-center gap-1 opacity-0 transition group-hover:opacity-100">
              <button
                type="button"
                title="Editar"
                onClick={() => navigate(`/roles/${role.jobRoleId}`)}
                className="flex h-7 w-7 items-center justify-center rounded-full text-slate-400 hover:bg-slate-200 dark:hover:bg-white/10"
              >
                <Pencil className="h-3.5 w-3.5" />
              </button>
              <button
                type="button"
                title="Desactivar"
                onClick={() => deleteMutation.mutate(role.jobRoleId)}
                className="flex h-7 w-7 items-center justify-center rounded-full text-slate-400 hover:bg-red-100 hover:text-red-600 dark:hover:bg-red-500/10"
              >
                <Trash2 className="h-3.5 w-3.5" />
              </button>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
