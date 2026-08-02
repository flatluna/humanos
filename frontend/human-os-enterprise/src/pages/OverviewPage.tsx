import { useQuery } from '@tanstack/react-query';
import { Building2, Briefcase, Users, ShieldCheck } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { getDepartments, getJobRoles, getTenantPeople } from '@/api/orgStructureApi';

export function OverviewPage() {
  const { user } = useAuth();
  const tenantId = user!.tenantId;

  const departmentsQuery = useQuery({
    queryKey: ['departments', tenantId],
    queryFn: () => getDepartments(tenantId),
  });
  const jobRolesQuery = useQuery({
    queryKey: ['job-roles', tenantId],
    queryFn: () => getJobRoles(tenantId),
  });
  const peopleQuery = useQuery({
    queryKey: ['tenant-people', tenantId],
    queryFn: () => getTenantPeople(tenantId),
  });

  const cards = [
    {
      label: 'Departamentos / Áreas',
      value: departmentsQuery.data?.length,
      icon: Building2,
      href: '/organizacion',
      chip: 'bg-violet-100 text-violet-600 dark:bg-violet-500/15 dark:text-violet-300',
      bar: 'from-violet-500 to-violet-400',
    },
    {
      label: 'Roles y Puestos',
      value: jobRolesQuery.data?.length,
      icon: Briefcase,
      href: '/roles',
      chip: 'bg-amber-100 text-amber-600 dark:bg-amber-500/15 dark:text-amber-300',
      bar: 'from-amber-500 to-amber-400',
    },
    {
      label: 'Personas',
      value: peopleQuery.data?.length,
      icon: Users,
      href: '/roles',
      chip: 'bg-brand-100 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300',
      bar: 'from-brand-600 to-brand-400',
    },
    {
      label: 'Sin Puesto asignado',
      value: peopleQuery.data?.filter((p) => !p.jobRoleId).length,
      icon: ShieldCheck,
      href: '/roles',
      chip: 'bg-rose-100 text-rose-600 dark:bg-rose-500/15 dark:text-rose-300',
      bar: 'from-rose-500 to-rose-400',
    },
  ] as const;

  return (
    <div className="mx-auto max-w-6xl space-y-8">
      <div>
        <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">
          Hola, {user?.name?.split(' ')[0]} 👋
        </h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
          Este es el estado de capacitación de {user?.tenantName ?? 'tu organización'}.
        </p>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {cards.map(({ label, value, icon: Icon, href, chip, bar }) => (
          <a
            key={label}
            href={href}
            className="group relative overflow-hidden rounded-2xl border border-slate-200 bg-white p-5 transition hover:-translate-y-0.5 hover:shadow-lg hover:shadow-slate-200/60 dark:border-white/10 dark:bg-white/5 dark:hover:shadow-none"
          >
            <span className={`absolute inset-x-0 top-0 h-1 bg-gradient-to-r ${bar}`} />
            <span className={`flex h-10 w-10 items-center justify-center rounded-xl ${chip}`}>
              <Icon className="h-5 w-5" />
            </span>
            <p className="mt-3 text-2xl font-semibold text-slate-900 dark:text-white">
              {value ?? <span className="text-slate-300 dark:text-white/20">—</span>}
            </p>
            <p className="mt-1 text-sm text-slate-500 dark:text-white/50">{label}</p>
          </a>
        ))}
      </div>

      <div className="rounded-2xl border border-dashed border-slate-200 bg-gradient-to-br from-white to-slate-50 p-6 dark:border-white/10 dark:from-white/5 dark:to-white/[0.02]">
        <h2 className="text-sm font-semibold text-slate-900 dark:text-white">Próximamente en esta consola</h2>
        <ul className="mt-3 space-y-2 text-sm text-slate-500 dark:text-white/50">
          <li>• Requisitos de capacitación y certificaciones por Rol (Role ↔ Capability).</li>
          <li>• Rutas de carrera y criterios de promoción entre Roles.</li>
          <li>• Reportes de brechas de capacidad por equipo/departamento.</li>
          <li>• Catálogo privado de la empresa en Capability Studio.</li>
        </ul>
      </div>
    </div>
  );
}
