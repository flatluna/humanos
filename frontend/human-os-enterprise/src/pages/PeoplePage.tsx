import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import { Users, Pencil, Trash2, Search, GraduationCap, UserPlus } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import {
  createTenantInvitation,
  deactivatePerson,
  deleteTenantInvitation,
  getDepartments,
  getTenantInvitations,
  getTenantPeople,
} from '@/api/orgStructureApi';

// PRIVACY: an employee's training plan/progress is only visible to HR
// (who administers the whole tenant) or to that employee's own direct
// manager (person.managerId === current user's personId) — anyone else
// (including other managers) sees the disabled icon below.
function canViewCareerPlan(currentUserOrgRole: string, currentUserPersonId: string, person: { managerId: string | null }): boolean {
  if (currentUserOrgRole === 'HRAdmin') return true;
  return currentUserOrgRole === 'Manager' && person.managerId === currentUserPersonId;
}

export function PeoplePage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const [search, setSearch] = useState('');
  const [departmentFilter, setDepartmentFilter] = useState('');
  const [inviteEmail, setInviteEmail] = useState('');
  const [inviteFirstName, setInviteFirstName] = useState('');
  const [inviteLastName, setInviteLastName] = useState('');
  const [inviteOrgRole, setInviteOrgRole] = useState<'Employee' | 'Manager' | 'HRAdmin'>('Employee');

  const peopleQuery = useQuery({
    queryKey: ['tenant-people', tenantId],
    queryFn: () => getTenantPeople(tenantId),
  });
  const departmentsQuery = useQuery({
    queryKey: ['departments', tenantId],
    queryFn: () => getDepartments(tenantId),
  });
  const invitationsQuery = useQuery({
    queryKey: ['tenant-invitations', tenantId],
    queryFn: () => getTenantInvitations(tenantId),
  });

  const deactivateMutation = useMutation({
    mutationFn: (personId: string) => deactivatePerson(personId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['tenant-people', tenantId] });
    },
  });
  const inviteMutation = useMutation({
    mutationFn: () =>
      createTenantInvitation(tenantId, {
        email: inviteEmail,
        firstName: inviteFirstName || null,
        lastName: inviteLastName || null,
        orgRole: inviteOrgRole,
      }),
    onSuccess: () => {
      setInviteEmail('');
      setInviteFirstName('');
      setInviteLastName('');
      setInviteOrgRole('Employee');
      void queryClient.invalidateQueries({ queryKey: ['tenant-invitations', tenantId] });
    },
  });
  const deleteInvitationMutation = useMutation({
    mutationFn: (tenantInvitationId: string) => deleteTenantInvitation(tenantId, tenantInvitationId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['tenant-invitations', tenantId] });
    },
  });

  const people = peopleQuery.data ?? [];
  const combinedError = deactivateMutation.error ?? inviteMutation.error ?? deleteInvitationMutation.error;

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
        <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">{t('people.title')}</h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
          {t('people.subtitle')}
        </p>
      </div>

      <form
        className="rounded-2xl border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-white/5"
        onSubmit={(e) => {
          e.preventDefault();
          inviteMutation.mutate();
        }}
      >
        <div className="mb-3 flex items-center gap-2">
          <UserPlus className="h-4 w-4 text-brand-600 dark:text-brand-300" />
          <h2 className="text-sm font-semibold text-slate-900 dark:text-white">{t('people.inviteTitle')}</h2>
        </div>
        <div className="grid gap-3 md:grid-cols-4">
          <input
            type="email"
            required
            value={inviteEmail}
            onChange={(e) => setInviteEmail(e.target.value)}
            placeholder={t('people.inviteEmailPlaceholder')}
            className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
          />
          <input
            type="text"
            value={inviteFirstName}
            onChange={(e) => setInviteFirstName(e.target.value)}
            placeholder={t('people.inviteFirstNamePlaceholder')}
            className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
          />
          <input
            type="text"
            value={inviteLastName}
            onChange={(e) => setInviteLastName(e.target.value)}
            placeholder={t('people.inviteLastNamePlaceholder')}
            className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
          />
          <div className="flex gap-2">
            <select
              value={inviteOrgRole}
              onChange={(e) => setInviteOrgRole(e.target.value as 'Employee' | 'Manager' | 'HRAdmin')}
              className="flex-1 rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
            >
              <option value="Employee">{t('roles.Employee')}</option>
              <option value="Manager">{t('roles.Manager')}</option>
              <option value="HRAdmin">{t('roles.HRAdmin')}</option>
            </select>
            <button
              type="submit"
              disabled={inviteMutation.isPending}
              className="rounded-lg bg-brand-600 px-3 py-2 text-sm font-medium text-white transition hover:bg-brand-700 disabled:opacity-50"
            >
              {inviteMutation.isPending ? t('people.inviting') : t('people.inviteButton')}
            </button>
          </div>
        </div>
      </form>

      <div className="rounded-2xl border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-white/5">
        <h2 className="text-sm font-semibold text-slate-900 dark:text-white">{t('people.invitationsTitle')}</h2>
        {invitationsQuery.isLoading && <p className="mt-2 text-sm text-slate-400">{t('people.loadingInvitations')}</p>}
        {invitationsQuery.isSuccess && invitationsQuery.data.length === 0 && (
          <p className="mt-2 text-sm text-slate-500 dark:text-white/50">{t('people.noInvitations')}</p>
        )}
        {invitationsQuery.isSuccess && invitationsQuery.data.length > 0 && (
          <div className="mt-2 space-y-1">
            {invitationsQuery.data.slice(0, 10).map((invite) => (
              <div key={invite.tenantInvitationId} className="flex items-center justify-between rounded-lg bg-slate-50 px-3 py-2 text-sm dark:bg-white/5">
                <span className="text-slate-700 dark:text-white/80">{invite.email}</span>
                <div className="flex items-center gap-3">
                  <span className="text-xs text-slate-500 dark:text-white/50">
                    {invite.status} · {t(`roles.${invite.orgRole}` as const, { defaultValue: invite.orgRole })}
                  </span>
                  {invite.status === 'Pending' && (
                    <button
                      type="button"
                      onClick={() => deleteInvitationMutation.mutate(invite.tenantInvitationId)}
                      disabled={deleteInvitationMutation.isPending}
                      className="rounded-md border border-slate-200 px-2 py-1 text-xs font-medium text-slate-600 transition hover:border-red-300 hover:text-red-600 disabled:opacity-50 dark:border-white/15 dark:text-white/70 dark:hover:border-red-400/40 dark:hover:text-red-300"
                    >
                      {t('people.deleteInvitation')}
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
        <div className="relative flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t('people.searchPlaceholder')}
            className="w-full rounded-lg border border-slate-200 bg-white py-2 pl-9 pr-3 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
          />
        </div>
        <select
          value={departmentFilter}
          onChange={(e) => setDepartmentFilter(e.target.value)}
          className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
        >
          <option value="">{t('people.allDepartmentsFilter')}</option>
          {departmentsQuery.data?.map((d) => (
            <option key={d.departmentId} value={d.departmentId}>
              {d.name}
            </option>
          ))}
        </select>
      </div>

      {combinedError && (
        <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
          {combinedError instanceof Error ? combinedError.message : t('common.unexpectedError')}
        </div>
      )}

      <div className="space-y-2">
        {peopleQuery.isLoading && <p className="text-sm text-slate-400">{t('common.loading')}</p>}
        {peopleQuery.isSuccess && filteredPeople.length === 0 && (
          <div className="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-sky-200 bg-sky-50/50 p-8 text-center dark:border-sky-500/20 dark:bg-sky-500/5">
            <span className="flex h-12 w-12 items-center justify-center rounded-full bg-sky-100 text-sky-600 dark:bg-sky-500/15 dark:text-sky-300">
              <Users className="h-6 w-6" />
            </span>
            <p className="text-sm text-slate-500 dark:text-white/40">{t('people.emptyFiltered')}</p>
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
                      {person.displayName || person.email || t('people.noName')}
                    </p>
                    <span className="rounded-full bg-violet-100 px-2 py-0.5 text-xs font-medium text-violet-700 dark:bg-violet-500/15 dark:text-violet-300">
                      {t(`roles.${person.orgRole}` as const, { defaultValue: person.orgRole })}
                    </span>
                    {!person.isActive && (
                      <span className="rounded-full bg-slate-200 px-2 py-0.5 text-xs font-medium text-slate-600 dark:bg-white/10 dark:text-white/60">
                        {t('people.inactiveBadge')}
                      </span>
                    )}
                  </div>
                  <p className="text-xs text-slate-400 dark:text-white/40">{person.email}</p>
                  <p className="mt-1 text-xs text-slate-500 dark:text-white/50">
                    {person.jobRoleTitle ?? t('people.unassignedRole')} · {person.departmentName ?? t('people.unassignedDept')}
                    {person.managerDisplayName ? ` · ${t('people.reportsTo', { manager: person.managerDisplayName })}` : ''}
                  </p>
                </div>
                <div className="flex items-center gap-1 opacity-0 transition group-hover:opacity-100">
                  {canViewCareerPlan(user!.orgRole, user!.personId, person) ? (
                    <button
                      type="button"
                      title={t('people.careerPlanTooltip')}
                      onClick={() => navigate(`/personal/${person.personId}/plan-capacitacion`)}
                      className="flex h-7 w-7 items-center justify-center rounded-full text-slate-400 hover:bg-indigo-100 hover:text-indigo-600 dark:hover:bg-indigo-500/10"
                    >
                      <GraduationCap className="h-3.5 w-3.5" />
                    </button>
                  ) : (
                    <button
                      type="button"
                      title={t('people.careerPlanPrivateTooltip')}
                      disabled
                      className="flex h-7 w-7 cursor-not-allowed items-center justify-center rounded-full text-slate-200 dark:text-white/10"
                    >
                      <GraduationCap className="h-3.5 w-3.5" />
                    </button>
                  )}
                  <button
                    type="button"
                    title={t('people.editTooltip')}
                    onClick={() => navigate(`/personal/${person.personId}`)}
                    className="flex h-7 w-7 items-center justify-center rounded-full text-slate-400 hover:bg-slate-200 dark:hover:bg-white/10"
                  >
                    <Pencil className="h-3.5 w-3.5" />
                  </button>
                  {person.isActive && person.personId !== user!.personId && (
                    <button
                      type="button"
                      title={t('people.deactivateTooltip')}
                      onClick={() => {
                        const targetName = person.displayName || person.email || t('people.noName');
                        if (window.confirm(t('people.deactivateConfirm', { name: targetName }))) {
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
