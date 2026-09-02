import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useLocation, useNavigate } from 'react-router';
import { ArrowLeft, Briefcase, Building2, CheckCircle2, Plus, Sparkles, Trash2 } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import {
  applyOrgChart,
  type ApplyOrgChartDepartment,
  type ApplyOrgChartRequest,
  type OrgChartExtractionResult,
} from '@/api/orgStructureApi';

interface EditableRole {
  id: string;
  title: string;
  levelLabel: string;
  detectedPersonName: string;
}

interface EditableDepartment {
  id: string;
  name: string;
  parentId: string | null;
  roles: EditableRole[];
}

const newId = () => crypto.randomUUID();

/** Translates the numeric hierarchy depth (0 = top of the chart) the AI
 *  agent returns into a free-text seniority label, matching the same
 *  convention used by RolesPage's "Nivel / Seniority" field. */
function levelLabelFromDepth(level: number, t: (key: string) => string): string {
  if (level <= 0) return t('orgChartImport.seniorityLevels.executive');
  if (level === 1) return t('orgChartImport.seniorityLevels.director');
  if (level === 2) return t('orgChartImport.seniorityLevels.manager');
  if (level === 3) return t('orgChartImport.seniorityLevels.coordinator');
  return t('orgChartImport.seniorityLevels.specialist');
}

/** Builds the editable department/role tree from the raw AI extraction —
 *  reconciling department names (matching people to their DepartmentName)
 *  and falling back to a single "Organización General" bucket for people
 *  the image didn't group under any department box. */
function buildEditableState(result: OrgChartExtractionResult, t: (key: string) => string): EditableDepartment[] {
  const departments: EditableDepartment[] = result.departments.map((d) => ({
    id: newId(),
    name: d.name,
    parentId: null,
    roles: [],
  }));

  const idByName = new Map<string, string>();
  for (const dept of departments) {
    idByName.set(dept.name.trim().toLowerCase(), dept.id);
  }

  // Resolve ParentDepartmentName -> parentId now that every department has an id.
  result.departments.forEach((raw, index) => {
    if (raw.parentDepartmentName) {
      const parentId = idByName.get(raw.parentDepartmentName.trim().toLowerCase());
      if (parentId) {
        departments[index].parentId = parentId;
      }
    }
  });

  let fallbackDepartment: EditableDepartment | null = null;
  function departmentFor(name: string | null): EditableDepartment {
    if (name) {
      const key = name.trim().toLowerCase();
      const existingId = idByName.get(key);
      if (existingId) {
        return departments.find((d) => d.id === existingId)!;
      }
      const created: EditableDepartment = { id: newId(), name: name.trim(), parentId: null, roles: [] };
      departments.push(created);
      idByName.set(key, created.id);
      return created;
    }
    if (!fallbackDepartment) {
      fallbackDepartment = { id: newId(), name: t('orgChartImport.seniorityLevels.generalOrg'), parentId: null, roles: [] };
      departments.push(fallbackDepartment);
    }
    return fallbackDepartment;
  }

  for (const person of result.people) {
    const department = departmentFor(person.departmentName);
    department.roles.push({
      id: newId(),
      title: person.position?.trim() || t('orgChartImport.seniorityLevels.untitledRole'),
      levelLabel: levelLabelFromDepth(person.level, t),
      detectedPersonName: person.name?.trim() ?? '',
    });
  }

  return departments;
}

export function OrgChartImportPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const navigate = useNavigate();
  const location = useLocation();
  const queryClient = useQueryClient();

  const extraction = (location.state as { result?: OrgChartExtractionResult } | null)?.result ?? null;

  const [departments, setDepartments] = useState<EditableDepartment[]>(() =>
    extraction ? buildEditableState(extraction, (k) => t(k as any)) : [],
  );
  const [createTestPersonRecords, setCreateTestPersonRecords] = useState(false);

  const detectedPeopleCount = useMemo(
    () => departments.reduce((total, d) => total + d.roles.length, 0),
    [departments],
  );

  const applyMutation = useMutation({
    mutationFn: (body: ApplyOrgChartRequest) => applyOrgChart(tenantId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['departments', tenantId] });
      void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId] });
      void queryClient.invalidateQueries({ queryKey: ['tenant-people', tenantId] });
      navigate('/organizacion');
    },
  });

  function updateDepartment(id: string, patch: Partial<EditableDepartment>) {
    setDepartments((prev) => prev.map((d) => (d.id === id ? { ...d, ...patch } : d)));
  }

  function removeDepartment(id: string) {
    setDepartments((prev) =>
      prev
        .filter((d) => d.id !== id)
        .map((d) => (d.parentId === id ? { ...d, parentId: null } : d)),
    );
  }

  function addDepartment() {
    setDepartments((prev) => [
      ...prev,
      { id: newId(), name: t('orgChartImport.seniorityLevels.newDepartment'), parentId: null, roles: [] },
    ]);
  }

  function updateRole(departmentId: string, roleId: string, patch: Partial<EditableRole>) {
    setDepartments((prev) =>
      prev.map((d) =>
        d.id === departmentId
          ? { ...d, roles: d.roles.map((r) => (r.id === roleId ? { ...r, ...patch } : r)) }
          : d,
      ),
    );
  }

  function removeRole(departmentId: string, roleId: string) {
    setDepartments((prev) =>
      prev.map((d) => (d.id === departmentId ? { ...d, roles: d.roles.filter((r) => r.id !== roleId) } : d)),
    );
  }

  function addRole(departmentId: string) {
    setDepartments((prev) =>
      prev.map((d) =>
        d.id === departmentId
          ? {
              ...d,
              roles: [
                ...d.roles,
                { id: newId(), title: t('orgChartImport.seniorityLevels.newRole'), levelLabel: '', detectedPersonName: '' },
              ],
            }
          : d,
      ),
    );
  }

  function handleConfirm() {
    const nameById = new Map(departments.map((d) => [d.id, d.name.trim()]));
    const payload: ApplyOrgChartRequest = {
      departments: departments
        .filter((d) => d.name.trim().length > 0)
        .map<ApplyOrgChartDepartment>((d) => ({
          name: d.name.trim(),
          parentName: d.parentId ? nameById.get(d.parentId) ?? null : null,
          jobRoles: d.roles
            .filter((r) => r.title.trim().length > 0)
            .map((r) => ({
              title: r.title.trim(),
              levelLabel: r.levelLabel.trim() || null,
              detectedPersonName: r.detectedPersonName.trim() || null,
            })),
        })),
      createTestPersonRecords,
    };
    applyMutation.mutate(payload);
  }

  if (!extraction) {
    return (
      <div className="mx-auto max-w-2xl space-y-4 text-center">
        <div className="rounded-2xl border border-dashed border-slate-200 bg-white p-10 dark:border-white/10 dark:bg-white/5">
          <p className="text-sm text-slate-500 dark:text-white/50">
            {t('orgChartImport.noPendingExtraction')}
          </p>
          <button
            type="button"
            onClick={() => navigate('/organizacion')}
            className="mt-4 inline-flex items-center gap-2 rounded-full bg-gradient-to-r from-violet-600 to-violet-500 px-4 py-2.5 text-sm font-medium text-white shadow-md shadow-violet-500/30 transition hover:brightness-110"
          >
            <ArrowLeft className="h-4 w-4" />
            {t('orgChartImport.returnToOrganization')}
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <button
          type="button"
          onClick={() => navigate('/organizacion')}
          className="inline-flex items-center gap-1 text-sm text-slate-500 hover:text-slate-700 dark:text-white/50 dark:hover:text-white"
        >
          <ArrowLeft className="h-3.5 w-3.5" />
          {t('orgChartImport.backToOrganization')}
        </button>
        <div className="mt-2 flex items-center gap-3">
          <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-violet-600 to-violet-500 text-white shadow-md shadow-violet-500/30">
            <Sparkles className="h-5 w-5" />
          </span>
          <div>
            <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">{t('orgChartImport.title')}</h1>
            <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
              {t('orgChartImport.subtitle', {
                deptCount: departments.length,
                deptPlural: departments.length === 1 ? '' : 's',
                peopleCount: detectedPeopleCount,
                peoplePlural: detectedPeopleCount === 1 ? '' : 's',
                tenant: user?.tenantName ?? '',
              })}
            </p>
          </div>
        </div>
      </div>

      {applyMutation.error && (
        <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
          {applyMutation.error instanceof Error ? applyMutation.error.message : t('common.unexpectedError')}
        </div>
      )}

      <div className="space-y-4">
        {departments.map((dept) => (
          <div
            key={dept.id}
            className="rounded-2xl border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-white/5"
          >
            <div className="flex flex-wrap items-center gap-3">
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-violet-100 text-violet-600 dark:bg-violet-500/15 dark:text-violet-300">
                <Building2 className="h-4 w-4" />
              </span>
              <input
                value={dept.name}
                onChange={(e) => updateDepartment(dept.id, { name: e.target.value })}
                className="min-w-[160px] flex-1 rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm font-medium text-slate-900 outline-none focus:border-violet-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
                placeholder={t('orgChartImport.departmentNamePlaceholder')}
              />
              <select
                value={dept.parentId ?? ''}
                onChange={(e) => updateDepartment(dept.id, { parentId: e.target.value || null })}
                className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-violet-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
              >
                <option value="">{t('orgChartImport.topLevelArea')}</option>
                {departments
                  .filter((d) => d.id !== dept.id)
                  .map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name}
                    </option>
                  ))}
              </select>
              <button
                type="button"
                title={t('orgChartImport.deleteDepartmentTitle')}
                onClick={() => removeDepartment(dept.id)}
                className="flex h-8 w-8 items-center justify-center rounded-full text-slate-400 hover:bg-red-100 hover:text-red-600 dark:hover:bg-red-500/10"
              >
                <Trash2 className="h-4 w-4" />
              </button>
            </div>

            <div className="mt-3 space-y-2 border-t border-slate-100 pt-3 dark:border-white/10">
              {dept.roles.map((role) => (
                <div key={role.id} className="flex flex-wrap items-center gap-2 rounded-lg bg-slate-50 p-2 dark:bg-white/5">
                  <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-lg bg-amber-100 text-amber-600 dark:bg-amber-500/15 dark:text-amber-300">
                    <Briefcase className="h-3.5 w-3.5" />
                  </span>
                  <input
                    value={role.title}
                    onChange={(e) => updateRole(dept.id, role.id, { title: e.target.value })}
                    placeholder={t('orgChartImport.roleTitlePlaceholder')}
                    className="min-w-[140px] flex-1 rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-sm outline-none focus:border-amber-500 dark:border-white/10 dark:bg-white/10 dark:text-white"
                  />
                  <input
                    value={role.levelLabel}
                    onChange={(e) => updateRole(dept.id, role.id, { levelLabel: e.target.value })}
                    placeholder={t('orgChartImport.roleLevelPlaceholder')}
                    className="w-40 rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-sm outline-none focus:border-amber-500 dark:border-white/10 dark:bg-white/10 dark:text-white"
                  />
                  <input
                    value={role.detectedPersonName}
                    onChange={(e) => updateRole(dept.id, role.id, { detectedPersonName: e.target.value })}
                    placeholder={t('orgChartImport.detectedPersonPlaceholder')}
                    className="w-48 rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-sm text-slate-500 outline-none focus:border-amber-500 dark:border-white/10 dark:bg-white/10 dark:text-white/60"
                  />
                  <button
                    type="button"
                    title={t('orgChartImport.deleteRoleTitle')}
                    onClick={() => removeRole(dept.id, role.id)}
                    className="ml-auto flex h-7 w-7 items-center justify-center rounded-full text-slate-400 hover:bg-red-100 hover:text-red-600 dark:hover:bg-red-500/10"
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                  </button>
                </div>
              ))}
              <button
                type="button"
                onClick={() => addRole(dept.id)}
                className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium text-amber-700 hover:bg-amber-50 dark:text-amber-300 dark:hover:bg-amber-500/10"
              >
                <Plus className="h-3.5 w-3.5" />
                {t('orgChartImport.addRole')}
              </button>
            </div>
          </div>
        ))}

        <button
          type="button"
          onClick={addDepartment}
          className="inline-flex items-center gap-2 rounded-full border border-dashed border-violet-300 px-4 py-2 text-sm font-medium text-violet-700 hover:bg-violet-50 dark:border-violet-500/30 dark:text-violet-300 dark:hover:bg-violet-500/10"
        >
          <Plus className="h-4 w-4" />
          {t('orgChartImport.addDepartment')}
        </button>
      </div>

      <label className="flex cursor-pointer items-start gap-3 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm dark:border-amber-500/20 dark:bg-amber-500/10">
        <input
          type="checkbox"
          checked={createTestPersonRecords}
          onChange={(e) => setCreateTestPersonRecords(e.target.checked)}
          className="mt-0.5 h-4 w-4 rounded border-slate-300 text-amber-600 focus:ring-amber-500"
        />
        <span className="text-amber-800 dark:text-amber-200">
          {t('orgChartImport.createTestPersonsCheckbox')}
        </span>
      </label>

      <div className="flex items-center justify-end gap-3 border-t border-slate-100 pt-4 dark:border-white/10">
        <button
          type="button"
          onClick={() => navigate('/organizacion')}
          className="rounded-full px-4 py-2.5 text-sm font-medium text-slate-500 hover:bg-slate-100 dark:text-white/50 dark:hover:bg-white/10"
        >
          {t('common.cancel')}
        </button>
        <button
          type="button"
          disabled={applyMutation.isPending || departments.length === 0}
          onClick={handleConfirm}
          className="inline-flex items-center gap-2 rounded-full bg-gradient-to-r from-emerald-600 to-emerald-500 px-5 py-2.5 text-sm font-medium text-white shadow-md shadow-emerald-500/30 transition hover:brightness-110 disabled:opacity-50 disabled:shadow-none"
        >
          <CheckCircle2 className="h-4 w-4" />
          {applyMutation.isPending ? t('orgChartImport.creating') : t('orgChartImport.confirmAndCreate')}
        </button>
      </div>
    </div>
  );
}
