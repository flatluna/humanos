import { useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import {
  UserCheck,
  Search,
  CheckCircle2,
  Copy,
  Check,
  Download,
  RefreshCw,
  User,
} from 'lucide-react';
import type {
  ProcessStepDto,
  EmployeeStepLearningAssignmentResponseDto,
} from '@/api/processDocumentsApi';
import type { TenantPersonDto } from '@/api/orgStructureApi';

export interface EmployeeAssignmentStepProps {
  step: ProcessStepDto;
  tenantPeople: TenantPersonDto[];
  assignmentResult: EmployeeStepLearningAssignmentResponseDto | null;
  isLoading: boolean;
  onPersonalizeAndAssign: (stepId: string, employeeId: string) => Promise<void>;
  onFinish: () => void;
  onAssignAnother: () => void;
}

export function EmployeeAssignmentStep({
  step,
  tenantPeople,
  assignmentResult,
  isLoading,
  onPersonalizeAndAssign,
  onFinish,
  onAssignAnother,
}: EmployeeAssignmentStepProps) {
  const { t } = useTranslation();

  const [searchTerm, setSearchTerm] = useState('');
  const [selectedRoleId, setSelectedRoleId] = useState<string>('all');
  const [selectedPersonId, setSelectedPersonId] = useState<string>('');
  const [copied, setCopied] = useState(false);

  // Available job roles for filtering
  const availableRoles = useMemo(() => {
    const rolesMap = new Map<string, string>();
    tenantPeople.forEach((p) => {
      if (p.jobRoleId && p.jobRoleTitle) {
        rolesMap.set(p.jobRoleId, p.jobRoleTitle);
      }
    });
    return Array.from(rolesMap.entries()).map(([id, title]) => ({ id, title }));
  }, [tenantPeople]);

  // Filtered employees list
  const filteredPeople = useMemo(() => {
    return tenantPeople.filter((p) => {
      const matchesSearch =
        !searchTerm.trim() ||
        (p.displayName && p.displayName.toLowerCase().includes(searchTerm.toLowerCase())) ||
        (p.email && p.email.toLowerCase().includes(searchTerm.toLowerCase())) ||
        (p.jobRoleTitle && p.jobRoleTitle.toLowerCase().includes(searchTerm.toLowerCase()));

      const matchesRole =
        selectedRoleId === 'all' || p.jobRoleId === selectedRoleId;

      return matchesSearch && matchesRole;
    });
  }, [tenantPeople, searchTerm, selectedRoleId]);

  const handleCopy = async () => {
    if (!assignmentResult?.assignmentMarkdown) return;
    await navigator.clipboard.writeText(assignmentResult.assignmentMarkdown);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleDownload = () => {
    if (!assignmentResult?.assignmentMarkdown) return;
    const blob = new Blob([assignmentResult.assignmentMarkdown], { type: 'text/markdown;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `Asignacion_Empleado_${assignmentResult.employeeId.slice(0, 8)}_Paso_${step.step_number}.md`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  };

  const handleAssign = async () => {
    if (!selectedPersonId) return;
    await onPersonalizeAndAssign(step.step_id, selectedPersonId);
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/40">
        <div>
          <div className="flex items-center gap-2">
            <span className="rounded bg-brand-100 px-2 py-0.5 text-xs font-bold text-brand-700 dark:bg-brand-500/20 dark:text-brand-300">
              Paso {step.step_number}
            </span>
            <h3 className="text-base font-bold text-slate-900 dark:text-white">
              {t('processLearning.employeeStep.title')}
            </h3>
          </div>
          <p className="mt-1 text-xs text-slate-500 dark:text-white/60">
            {t('processLearning.employeeStep.subtitle')}
          </p>
        </div>

        {assignmentResult && (
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={handleCopy}
              className="inline-flex items-center gap-1.5 rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white"
            >
              {copied ? <Check className="h-3.5 w-3.5 text-emerald-600" /> : <Copy className="h-3.5 w-3.5" />}
              <span>{copied ? t('processLearning.assignmentStep.copiedSuccess') : t('processLearning.assignmentStep.copyMarkdown')}</span>
            </button>

            <button
              type="button"
              onClick={handleDownload}
              className="inline-flex items-center gap-1.5 rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white"
            >
              <Download className="h-3.5 w-3.5" />
              <span>{t('processLearning.assignmentStep.downloadMarkdown')}</span>
            </button>

            <button
              type="button"
              onClick={onAssignAnother}
              className="rounded-xl border border-slate-200 bg-white px-3.5 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white"
            >
              {t('processLearning.employeeStep.assignAnotherBtn')}
            </button>

            <button
              type="button"
              onClick={onFinish}
              className="inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-emerald-600 to-teal-700 px-5 py-2 text-xs font-semibold text-white shadow-sm hover:from-emerald-500 hover:to-teal-600"
            >
              <Check className="h-4 w-4" />
              <span>{t('processLearning.employeeStep.finishWizardBtn')}</span>
            </button>
          </div>
        )}
      </div>

      {/* Employee Picker (if not yet assigned or assigning another) */}
      {!assignmentResult ? (
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/40 space-y-5">
          <div className="flex items-center gap-2 border-b border-slate-100 pb-3 dark:border-white/5">
            <User className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
            <h4 className="text-xs font-bold uppercase tracking-wider text-slate-900 dark:text-white">
              {t('processLearning.employeeStep.selectEmployeePlaceholder')}
            </h4>
          </div>

          {/* Search & Filter Bar */}
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 md:grid-cols-3">
            <div className="relative sm:col-span-2">
              <Search className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
              <input
                type="text"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                placeholder={t('processLearning.employeeStep.searchEmployee')}
                className="w-full rounded-xl border border-slate-200 bg-white pl-9 pr-4 py-2 text-xs text-slate-900 shadow-sm focus:border-emerald-500 focus:outline-none dark:border-white/10 dark:bg-white/5 dark:text-white"
              />
            </div>

            <div>
              <select
                value={selectedRoleId}
                onChange={(e) => setSelectedRoleId(e.target.value)}
                className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs text-slate-800 shadow-sm focus:border-emerald-500 focus:outline-none dark:border-white/10 dark:bg-white/5 dark:text-white"
              >
                <option value="all">Todos los puestos</option>
                {availableRoles.map((r) => (
                  <option key={r.id} value={r.id}>{r.title}</option>
                ))}
              </select>
            </div>
          </div>

          {/* Employees List */}
          <div className="max-h-64 overflow-y-auto space-y-1.5 border rounded-xl p-2 dark:border-white/10">
            {filteredPeople.length === 0 ? (
              <p className="p-4 text-center text-xs italic text-slate-400">
                No se encontraron colaboradores que coincidan con la búsqueda.
              </p>
            ) : (
              filteredPeople.map((person) => {
                const isSelected = selectedPersonId === person.personId;
                return (
                  <button
                    key={person.personId}
                    type="button"
                    onClick={() => setSelectedPersonId(person.personId)}
                    className={`flex w-full items-center justify-between rounded-lg p-2.5 text-left text-xs transition ${
                      isSelected
                        ? 'bg-emerald-50 border border-emerald-500 text-emerald-900 font-bold dark:bg-emerald-500/15 dark:border-emerald-400 dark:text-white'
                        : 'hover:bg-slate-50 text-slate-700 dark:text-white/80 dark:hover:bg-white/5'
                    }`}
                  >
                    <div>
                      <p className="font-semibold text-slate-900 dark:text-white">
                        {person.displayName || person.email}
                      </p>
                      <p className="text-[11px] text-slate-400">
                        {person.jobRoleTitle || 'Sin puesto'} • {person.departmentName || 'Sin departamento'}
                      </p>
                    </div>

                    {isSelected && (
                      <span className="rounded-full bg-emerald-100 p-1 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-300">
                        <Check className="h-3.5 w-3.5" />
                      </span>
                    )}
                  </button>
                );
              })
            )}
          </div>

          {/* Action to Personalize & Assign */}
          <div className="flex justify-end pt-2">
            <button
              type="button"
              onClick={handleAssign}
              disabled={!selectedPersonId || isLoading}
              className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-gradient-to-r from-emerald-600 to-teal-700 px-6 py-2.5 text-sm font-semibold text-white shadow-md shadow-emerald-500/20 hover:from-emerald-500 hover:to-teal-600 transition disabled:opacity-50"
            >
              {isLoading ? (
                <>
                  <RefreshCw className="h-4 w-4 animate-spin" />
                  <span>{t('processLearning.employeeStep.personalizingText')}</span>
                </>
              ) : (
                <>
                  <UserCheck className="h-4 w-4" />
                  <span>{t('processLearning.employeeStep.personalizeBtn')}</span>
                </>
              )}
            </button>
          </div>
        </div>
      ) : (
        /* Personalized Assignment Details */
        <div className="space-y-6">
          {/* Status banner */}
          <div
            className={`rounded-2xl border p-5 ${
              assignmentResult.employeeAssignmentStatus === 'assigned'
                ? 'border-emerald-200 bg-emerald-50/50 dark:border-emerald-500/20 dark:bg-emerald-500/5'
                : assignmentResult.employeeAssignmentStatus === 'assigned_with_content_gaps'
                ? 'border-amber-200 bg-amber-50/50 dark:border-amber-500/20 dark:bg-amber-500/5'
                : 'border-rose-200 bg-rose-50/50 dark:border-rose-500/20 dark:bg-rose-500/5'
            }`}
          >
            <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
              <div className="flex items-center gap-3">
                <CheckCircle2 className="h-6 w-6 text-emerald-600 dark:text-emerald-400" />
                <div>
                  <h4 className="text-base font-bold text-slate-900 dark:text-white">
                    {t('processLearning.employeeStep.saveSuccess')}
                  </h4>
                  <p className="text-xs text-slate-500 dark:text-white/60">
                    {t('processLearning.employeeStep.viewSavedId', { id: assignmentResult.assignmentId })}
                  </p>
                </div>
              </div>

              <span
                className={`rounded-full px-3 py-1 text-xs font-bold uppercase tracking-wider self-start sm:self-center ${
                  assignmentResult.employeeAssignmentStatus === 'assigned'
                    ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300'
                    : assignmentResult.employeeAssignmentStatus === 'assigned_with_content_gaps'
                    ? 'bg-amber-100 text-amber-800 dark:bg-amber-500/20 dark:text-amber-300'
                    : 'bg-rose-100 text-rose-800 dark:bg-rose-500/20 dark:text-rose-300'
                }`}
              >
                {assignmentResult.employeeAssignmentStatus}
              </span>
            </div>
          </div>

          {/* Reported Experience Summary */}
          {assignmentResult.reportedExperienceSummary.length > 0 && (
            <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/40 space-y-2">
              <h5 className="text-xs font-bold uppercase tracking-wider text-slate-700 dark:text-white/70">
                {t('processLearning.employeeStep.reportedExperienceTitle')}
              </h5>
              <ul className="list-inside list-disc space-y-1 text-xs text-slate-600 dark:text-white/70">
                {assignmentResult.reportedExperienceSummary.map((item, i) => (
                  <li key={i}>{item}</li>
                ))}
              </ul>
            </div>
          )}

          {/* Assigned Classes & Engrams */}
          <div className="space-y-4">
            <h5 className="text-xs font-bold uppercase tracking-wider text-slate-700 dark:text-white/70">
              Clases y Engramas Personalizados para el Colaborador
            </h5>

            {assignmentResult.assignedClasses.map((ac) => (
              <div
                key={ac.classId}
                className="rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-white/10 dark:bg-slate-900/40 p-5 space-y-3"
              >
                <div className="flex items-center justify-between border-b border-slate-100 pb-2.5 dark:border-white/5">
                  <div>
                    <span className="rounded bg-violet-100 px-2 py-0.5 text-[10px] font-bold text-violet-800 dark:bg-violet-500/20 dark:text-violet-300">
                      {ac.learningPathName}
                    </span>
                    <h5 className="mt-1 text-sm font-bold text-slate-900 dark:text-white">
                      {ac.className}
                    </h5>
                  </div>
                  <span
                    className={`rounded-full px-2.5 py-0.5 text-[10px] font-bold uppercase ${
                      ac.classAssignmentStatus === 'required'
                        ? 'bg-rose-100 text-rose-800 dark:bg-rose-500/20 dark:text-rose-300'
                        : ac.classAssignmentStatus === 'recommended'
                        ? 'bg-blue-100 text-blue-800 dark:bg-blue-500/20 dark:text-blue-300'
                        : 'bg-slate-100 text-slate-700 dark:bg-white/10 dark:text-white/70'
                    }`}
                  >
                    {ac.classAssignmentStatus === 'required'
                      ? t('processLearning.employeeStep.statusRequired')
                      : ac.classAssignmentStatus === 'recommended'
                      ? t('processLearning.employeeStep.statusRecommended')
                      : t('processLearning.employeeStep.statusReferenceOnly')}
                  </span>
                </div>

                <div className="space-y-2">
                  {ac.assignedEngrams.map((ae) => (
                    <div
                      key={ae.engramId}
                      className="rounded-xl border border-slate-100 p-3 bg-slate-50/50 dark:border-white/5 dark:bg-white/[0.02] text-xs space-y-1"
                    >
                      <div className="flex items-center justify-between">
                        <span className="font-bold text-slate-900 dark:text-white">
                          {ae.engramName}
                        </span>
                        <div className="flex items-center gap-1.5">
                          <span className="rounded bg-slate-200/70 px-1.5 py-0.5 text-[9px] font-semibold text-slate-600 dark:bg-white/10 dark:text-white/50">
                            {ae.employeeEvidenceLevel === 'reported'
                              ? t('processLearning.employeeStep.evidenceReportedBadge')
                              : ae.employeeEvidenceLevel === 'completed'
                              ? t('processLearning.employeeStep.evidenceCompletedBadge')
                              : ae.employeeEvidenceLevel === 'demonstrated'
                              ? t('processLearning.employeeStep.evidenceDemonstratedBadge')
                              : t('processLearning.employeeStep.evidenceUnknownBadge')}
                          </span>
                          <span
                            className={`rounded-full px-2 py-0.5 text-[9px] font-bold uppercase ${
                              ae.assignmentStatus === 'required'
                                ? 'bg-rose-100 text-rose-800 dark:bg-rose-500/20 dark:text-rose-300'
                                : ae.assignmentStatus === 'recommended'
                                ? 'bg-blue-100 text-blue-800 dark:bg-blue-500/20 dark:text-blue-300'
                                : 'bg-emerald-100 text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300'
                            }`}
                          >
                            {ae.assignmentStatus}
                          </span>
                        </div>
                      </div>
                      <p className="text-[11px] text-slate-600 dark:text-white/70">
                        {ae.assignmentReason}
                      </p>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>

          {/* Rendered Employee-Facing Markdown Document View */}
          <div className="rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-white/10 dark:bg-slate-900/40 overflow-hidden">
            <div className="border-b border-slate-100 bg-slate-50/80 px-6 py-3.5 flex items-center justify-between dark:border-white/5 dark:bg-white/5">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-600 dark:text-white/60">
                Documento de Asignación Personalizado (Markdown)
              </span>
              <span className="text-xs text-slate-400">
                {t('processLearning.employeeStep.workPerformanceTitle')}
              </span>
            </div>

            <div className="p-6 md:p-8 font-sans text-xs leading-relaxed overflow-x-auto whitespace-pre-wrap dark:text-slate-200">
              {assignmentResult.assignmentMarkdown}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
