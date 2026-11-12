import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { FileText, Users, Server, ArrowRight, Workflow, Building2 } from 'lucide-react';
import type { ProcessDocumentStructureDto, ProcessStepDto } from '@/api/processDocumentsApi';
import { getDepartments, getJobRoles } from '@/api/orgStructureApi';

export interface ProcessClassificationStepProps {
  structure: ProcessDocumentStructureDto;
  excludedStepIds: Set<string>;
  selectedStepId: string | null;
  tenantId?: string;
  onSelectStep: (stepId: string) => void;
  onUpdateStructure: (structure: ProcessDocumentStructureDto) => void;
  onApproveAndContinue: () => void;
}

/** Small labeled stat used in the process-level header (e.g. "8 Pasos", "3 Roles"). */
function HeaderStat({ icon: Icon, label, value }: { icon: typeof Users; label: string; value: string | number }) {
  return (
    <div className="flex items-center gap-2 rounded-lg border border-gray-200 bg-white px-3 py-2">
      <Icon className="h-4 w-4 text-indigo-600" />
      <div>
        <div className="text-sm font-semibold text-gray-900">{value}</div>
        <div className="text-xs text-gray-500">{label}</div>
      </div>
    </div>
  );
}

/**
 * Wizard Step 3 "Resumen del Proceso": a lightweight, read-only executive summary of the whole process
 * (header) plus a one-line-per-step overview. This intentionally avoids any per-step AI analysis calls —
 * it only surfaces data already captured during upload/extraction (Step 1/2), so it loads instantly and
 * is meant for change-management-style review, not for detailed classification (that remains an internal
 * capability used later in Skills Identification / Mapping).
 */
export function ProcessClassificationStep({
  structure,
  excludedStepIds,
  selectedStepId,
  tenantId,
  onSelectStep,
  onUpdateStructure,
  onApproveAndContinue,
}: ProcessClassificationStepProps) {
  const { t } = useTranslation();

  const { data: departments = [] } = useQuery({
    queryKey: ['departments', tenantId],
    queryFn: () => getDepartments(tenantId!),
    enabled: !!tenantId,
  });

  const { data: jobRoles = [] } = useQuery({
    queryKey: ['jobRoles', tenantId],
    queryFn: () => getJobRoles(tenantId!),
    enabled: !!tenantId,
  });

  const appliesToAll = structure.applies_to_all_departments;
  const selectedDepartmentIds = useMemo(() => new Set(structure.department_ids), [structure.department_ids]);
  const selectedJobRoleIds = useMemo(() => new Set(structure.job_role_ids), [structure.job_role_ids]);

  const toggleAppliesToAll = () => {
    onUpdateStructure({ ...structure, applies_to_all_departments: !appliesToAll });
  };

  const toggleDepartment = (departmentId: string) => {
    const next = new Set(selectedDepartmentIds);
    if (next.has(departmentId)) next.delete(departmentId);
    else next.add(departmentId);
    onUpdateStructure({ ...structure, department_ids: Array.from(next) });
  };

  const toggleJobRole = (jobRoleId: string) => {
    const next = new Set(selectedJobRoleIds);
    if (next.has(jobRoleId)) next.delete(jobRoleId);
    else next.add(jobRoleId);
    onUpdateStructure({ ...structure, job_role_ids: Array.from(next) });
  };

  const allSteps: ProcessStepDto[] = useMemo(
    () => structure.chapters.flatMap((c) => c.steps),
    [structure.chapters],
  );
  const includedSteps = useMemo(
    () => allSteps.filter((s) => !excludedStepIds.has(s.step_id)),
    [allSteps, excludedStepIds],
  );

  const uniqueRoles = useMemo(() => {
    const roles = new Set<string>();
    structure.participating_roles.forEach((r) => r.trim() && roles.add(r.trim()));
    includedSteps.forEach((s) => {
      const role = s.primary_responsible_role || s.owner_role;
      if (role && role.trim()) roles.add(role.trim());
    });
    return Array.from(roles);
  }, [structure.participating_roles, includedSteps]);

  const uniqueSystems = useMemo(() => {
    const systems = new Set<string>();
    structure.systems_used.forEach((s) => s.trim() && systems.add(s.trim()));
    includedSteps.forEach((s) => {
      (s.systems_and_tools || []).forEach((sys) => sys.trim() && systems.add(sys.trim()));
    });
    return Array.from(systems);
  }, [structure.systems_used, includedSteps]);

  const placeholder = t('processLearning.classifyStep.notInSource', 'No especificado en el documento');

  return (
    <div className="space-y-6">
      {/* ===== Process-level executive summary header ===== */}
      <div className="rounded-xl border border-indigo-100 bg-indigo-50/40 p-5">
        <div className="flex items-center gap-2">
          <Workflow className="h-5 w-5 text-indigo-600" />
          <h2 className="text-lg font-semibold text-gray-900">
            {t('processLearning.classifyStep.title', 'Resumen del Proceso')}
          </h2>
        </div>
        <p className="mt-1 text-sm text-gray-600">
          {t(
            'processLearning.classifyStep.subtitle',
            'Vista ejecutiva del proceso completo para revisión y aprobación de change management.',
          )}
        </p>

        <div className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
          <HeaderStat icon={FileText} label={t('processLearning.classifyStep.steps', 'Pasos')} value={includedSteps.length} />
          <HeaderStat icon={Users} label={t('processLearning.classifyStep.roles', 'Roles')} value={uniqueRoles.length} />
          <HeaderStat icon={Server} label={t('processLearning.classifyStep.systems', 'Sistemas')} value={uniqueSystems.length} />
          <HeaderStat
            icon={Workflow}
            label={t('processLearning.classifyStep.chapters', 'Capítulos')}
            value={structure.chapters.length}
          />
        </div>

        {structure.executive_summary?.trim() && (
          <div className="mt-4 rounded-lg border border-indigo-200 bg-white p-4">
            <h3 className="text-xs font-bold uppercase tracking-wider text-indigo-700">
              {t('processLearning.classifyStep.executiveSummary', 'Resumen Ejecutivo')}
            </h3>
            <p className="mt-1.5 text-sm leading-relaxed text-gray-800">
              {structure.executive_summary.trim()}
            </p>
          </div>
        )}

        <dl className="mt-4 space-y-2 text-sm">
          <div>
            <dt className="font-medium text-gray-700">{t('processLearning.classifyStep.purpose', 'Propósito')}</dt>
            <dd className="text-gray-600">{structure.purpose?.trim() || placeholder}</dd>
          </div>
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
            <div>
              <dt className="font-medium text-gray-700">{t('processLearning.classifyStep.processOwner', 'Process Owner')}</dt>
              <dd className="text-gray-600">{structure.process_owner?.trim() || placeholder}</dd>
            </div>
            <div>
              <dt className="font-medium text-gray-700">{t('processLearning.classifyStep.trigger', 'Trigger')}</dt>
              <dd className="text-gray-600">{structure.trigger?.trim() || placeholder}</dd>
            </div>
            <div>
              <dt className="font-medium text-gray-700">{t('processLearning.classifyStep.scope', 'Alcance')}</dt>
              <dd className="text-gray-600">{structure.scope?.trim() || placeholder}</dd>
            </div>
            <div>
              <dt className="font-medium text-gray-700">{t('processLearning.classifyStep.finalOutput', 'Resultado final')}</dt>
              <dd className="text-gray-600">{structure.final_output?.trim() || placeholder}</dd>
            </div>
          </div>
        </dl>
      </div>

      {/* ===== Scope: which Departments/Job Roles must follow this process ===== */}
      <div className="rounded-xl border border-gray-200 p-5">
        <div className="flex items-center gap-2">
          <Building2 className="h-5 w-5 text-indigo-600" />
          <h3 className="text-sm font-semibold text-gray-900">
            {t('processLearning.classifyStep.scopeTitle', '¿Quién debe seguir este proceso?')}
          </h3>
        </div>
        <p className="mt-1 text-sm text-gray-600">
          {t(
            'processLearning.classifyStep.scopeSubtitle',
            'Selecciona los departamentos y/o roles que deben cumplir este proceso, o márcalo como aplicable a toda la empresa.',
          )}
        </p>

        <label className="mt-3 flex items-center gap-2 rounded-lg border border-gray-200 bg-gray-50 px-3 py-2 text-sm font-medium text-gray-800">
          <input
            type="checkbox"
            checked={appliesToAll}
            onChange={toggleAppliesToAll}
            className="h-4 w-4 rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
          />
          {t('processLearning.classifyStep.appliesToAllDepartments', 'Aplica a toda la empresa')}
        </label>

        {!appliesToAll && (
          <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <h4 className="text-xs font-bold uppercase tracking-wider text-gray-500">
                {t('processLearning.classifyStep.departments', 'Departamentos')}
              </h4>
              <div className="mt-2 max-h-56 space-y-1 overflow-y-auto rounded-lg border border-gray-200 p-2">
                {departments.length === 0 && (
                  <p className="px-1 py-1 text-xs text-gray-400">
                    {t('processLearning.classifyStep.noDepartments', 'No hay departamentos registrados.')}
                  </p>
                )}
                {departments.map((d) => (
                  <label
                    key={d.departmentId}
                    className="flex items-center gap-2 rounded px-1 py-1 text-sm text-gray-700 hover:bg-gray-50"
                  >
                    <input
                      type="checkbox"
                      checked={selectedDepartmentIds.has(d.departmentId)}
                      onChange={() => toggleDepartment(d.departmentId)}
                      className="h-4 w-4 rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                    />
                    {d.name}
                  </label>
                ))}
              </div>
            </div>

            <div>
              <h4 className="text-xs font-bold uppercase tracking-wider text-gray-500">
                {t('processLearning.classifyStep.jobRoles', 'Roles / Puestos')}
              </h4>
              <div className="mt-2 max-h-56 space-y-1 overflow-y-auto rounded-lg border border-gray-200 p-2">
                {jobRoles.length === 0 && (
                  <p className="px-1 py-1 text-xs text-gray-400">
                    {t('processLearning.classifyStep.noJobRoles', 'No hay roles registrados.')}
                  </p>
                )}
                {jobRoles.map((r) => (
                  <label
                    key={r.jobRoleId}
                    className="flex items-center gap-2 rounded px-1 py-1 text-sm text-gray-700 hover:bg-gray-50"
                  >
                    <input
                      type="checkbox"
                      checked={selectedJobRoleIds.has(r.jobRoleId)}
                      onChange={() => toggleJobRole(r.jobRoleId)}
                      className="h-4 w-4 rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                    />
                    {r.title}
                    {r.departmentName && <span className="text-xs text-gray-400">({r.departmentName})</span>}
                  </label>
                ))}
              </div>
            </div>
          </div>
        )}
      </div>

      {/* ===== Lightweight per-step overview (no AI calls, no coded classification) ===== */}
      <div className="rounded-xl border border-gray-200">
        <div className="border-b border-gray-200 px-4 py-3">
          <h3 className="text-sm font-semibold text-gray-900">
            {t('processLearning.classifyStep.stepsOverview', 'Pasos del proceso')}
          </h3>
        </div>
        <ul className="divide-y divide-gray-100">
          {includedSteps.map((s) => {
            const responsible = s.primary_responsible_role || s.owner_role;
            const primarySystem = s.systems_and_tools?.[0];
            return (
              <li
                key={s.step_id}
                onClick={() => onSelectStep(s.step_id)}
                className={`cursor-pointer px-4 py-3 transition-colors hover:bg-gray-50 ${
                  selectedStepId === s.step_id ? 'bg-indigo-50/60' : ''
                }`}
              >
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="text-sm font-medium text-gray-900">
                      {s.step_number}. {s.step_name}
                    </div>
                    {s.objective && <div className="mt-0.5 text-xs text-gray-500">{s.objective}</div>}
                  </div>
                  <div className="shrink-0 text-right text-xs">
                    <div className="text-gray-700">
                      {responsible?.trim() || t('processLearning.classifyStep.pendingOwner', 'Pendiente de confirmar')}
                    </div>
                    {primarySystem && <div className="text-gray-400">{primarySystem}</div>}
                  </div>
                </div>
              </li>
            );
          })}
        </ul>
      </div>

      <div className="flex justify-end">
        <button
          type="button"
          onClick={onApproveAndContinue}
          className="inline-flex items-center gap-2 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-700"
        >
          {t('processLearning.classifyStep.approveAndContinue', 'Aprobar y Continuar')}
          <ArrowRight className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}
