import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  ListTree,
  Plus,
  Search,
  BookOpen,
  BarChart3,
  Trash2,
  Calendar,
  Layers,
  GraduationCap,
  ArrowRight,
  CheckCircle2,
  Clock,
} from 'lucide-react';
import {
  getProcessDocuments,
  deleteProcessDocument,
  type ProcessDocumentListItemDto,
} from '@/api/processDocumentsApi';

export interface ProcessDocumentsListViewProps {
  tenantId?: string;
  onCreateNew: () => void;
  onEditProcess: (documentId: string) => void;
  onViewSummary: (documentId: string) => void;
}

export function ProcessDocumentsListView({
  tenantId,
  onCreateNew,
  onEditProcess,
  onViewSummary,
}: ProcessDocumentsListViewProps) {
  const queryClient = useQueryClient();
  const [searchTerm, setSearchTerm] = useState('');

  const { data: processes = [], isLoading, isError, refetch } = useQuery({
    queryKey: ['process-documents', tenantId],
    queryFn: () => getProcessDocuments(tenantId),
    enabled: !!tenantId,
  });

  const deleteMutation = useMutation({
    mutationFn: (docId: string) => deleteProcessDocument(docId, tenantId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['process-documents', tenantId] });
    },
    onError: (err: any) => {
      alert(`Error al eliminar el proceso: ${err?.response?.data?.message || err?.message}`);
    },
  });

  const handleDelete = (doc: ProcessDocumentListItemDto, e: React.MouseEvent) => {
    e.stopPropagation();
    const ok = window.confirm(
      `¿Estás seguro de eliminar el proceso "${doc.title}"?\nEsta acción eliminará sus pasos y la matriz de habilidades asociada en Azure SQL.`
    );
    if (ok) {
      deleteMutation.mutate(doc.processDocumentId);
    }
  };

  const filteredProcesses = processes.filter((p) => {
    if (!searchTerm.trim()) return true;
    const q = searchTerm.toLowerCase();
    return (
      p.title.toLowerCase().includes(q) ||
      (p.code && p.code.toLowerCase().includes(q)) ||
      (p.status && p.status.toLowerCase().includes(q))
    );
  });

  return (
    <div className="space-y-6 animate-in fade-in duration-200">
      {/* Top Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-2.5">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-brand-500/10 text-brand-600 dark:bg-brand-500/20 dark:text-brand-400">
              <ListTree className="h-5 w-5" />
            </span>
            <div>
              <h1 className="text-xl font-bold text-slate-900 dark:text-white">
                Procesos Operativos y Aprendizaje
              </h1>
              <p className="text-xs text-slate-500 dark:text-white/60">
                Gestiona manuales de trabajo, SOPs, políticas y su Matriz de Habilidades conectada a cursos en Studio.
              </p>
            </div>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={onCreateNew}
            className="inline-flex items-center gap-2 rounded-xl bg-brand-600 px-4 py-2.5 text-xs font-bold text-white shadow-md shadow-brand-500/20 transition-all hover:bg-brand-500 hover:shadow-brand-500/30 active:scale-95"
          >
            <Plus className="h-4 w-4" />
            <span>Nuevo Proceso</span>
          </button>
        </div>
      </div>

      {/* Filter / Search Bar */}
      <div className="flex flex-col gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm dark:border-white/10 dark:bg-white/5 sm:flex-row sm:items-center sm:justify-between">
        <div className="relative flex-1 max-w-md">
          <Search className="absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
          <input
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder="Buscar por nombre, código o estado..."
            className="w-full rounded-xl border border-slate-200 bg-slate-50 py-2 pl-9 pr-4 text-xs text-slate-800 placeholder-slate-400 focus:border-brand-500 focus:bg-white focus:outline-none dark:border-white/10 dark:bg-white/5 dark:text-white"
          />
        </div>

        <div className="flex items-center gap-2 text-xs font-medium text-slate-500 dark:text-white/60">
          <span>Total:</span>
          <span className="rounded-md bg-slate-100 px-2 py-0.5 font-bold text-slate-800 dark:bg-white/10 dark:text-white">
            {filteredProcesses.length}
          </span>
          <span>{filteredProcesses.length === 1 ? 'proceso' : 'procesos'}</span>
        </div>
      </div>

      {/* Loading State */}
      {isLoading && (
        <div className="flex min-h-[300px] items-center justify-center rounded-2xl border border-slate-200 bg-white p-8 dark:border-white/10 dark:bg-white/5">
          <div className="flex flex-col items-center gap-3">
            <div className="h-8 w-8 animate-spin rounded-full border-2 border-brand-500 border-t-transparent" />
            <p className="text-xs text-slate-500 dark:text-white/60">Cargando procesos registrados...</p>
          </div>
        </div>
      )}

      {/* Error State */}
      {!isLoading && isError && (
        <div className="rounded-2xl border border-rose-200 bg-rose-50/60 p-6 text-center dark:border-rose-500/20 dark:bg-rose-500/5">
          <p className="text-xs font-semibold text-rose-800 dark:text-rose-300">
            No se pudieron cargar los procesos. Verifica la conexión con el servidor.
          </p>
          <button
            type="button"
            onClick={() => refetch()}
            className="mt-3 rounded-lg bg-rose-600 px-3 py-1.5 text-xs font-bold text-white hover:bg-rose-500"
          >
            Reintentar
          </button>
        </div>
      )}

      {/* Empty State */}
      {!isLoading && !isError && processes.length === 0 && (
        <div className="flex min-h-[360px] flex-col items-center justify-center rounded-2xl border border-dashed border-slate-300 bg-white p-10 text-center dark:border-white/15 dark:bg-white/[0.02]">
          <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-brand-50 text-brand-600 dark:bg-brand-500/10 dark:text-brand-400">
            <BookOpen className="h-8 w-8" />
          </div>
          <h2 className="mt-4 text-base font-bold text-slate-900 dark:text-white">
            Aún no has registrado ningún proceso
          </h2>
          <p className="mt-1 max-w-md text-xs text-slate-500 dark:text-white/60">
            Sube un manual de operaciones, SOP o política corporativa en PDF. El asistente extraerá los pasos operativos y generará la Matriz de Habilidades conectada a cursos.
          </p>
          <button
            type="button"
            onClick={onCreateNew}
            className="mt-6 inline-flex items-center gap-2 rounded-xl bg-brand-600 px-5 py-2.5 text-xs font-bold text-white shadow-md shadow-brand-500/20 hover:bg-brand-500"
          >
            <Plus className="h-4 w-4" />
            <span>Crear Primer Proceso con el Asistente</span>
          </button>
        </div>
      )}

      {/* Search No Matches State */}
      {!isLoading && !isError && processes.length > 0 && filteredProcesses.length === 0 && (
        <div className="rounded-2xl border border-slate-200 bg-white p-8 text-center dark:border-white/10 dark:bg-white/5">
          <p className="text-xs text-slate-500 dark:text-white/60">
            No se encontraron procesos que coincidan con "{searchTerm}".
          </p>
        </div>
      )}

      {/* Cards Grid */}
      {!isLoading && !isError && filteredProcesses.length > 0 && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {filteredProcesses.map((proc) => {
            const isAnalyzed = proc.status === 'Analyzed' || proc.status === 'Approved' || proc.status === 'Published';
            const formattedDate = proc.createdDate
              ? new Date(proc.createdDate).toLocaleDateString('es-ES', {
                  day: 'numeric',
                  month: 'short',
                  year: 'numeric',
                })
              : '—';

            return (
              <div
                key={proc.processDocumentId}
                onClick={() => onEditProcess(proc.processDocumentId)}
                className="group relative flex flex-col justify-between rounded-2xl border border-slate-200 bg-white p-5 shadow-sm transition-all hover:border-brand-500 hover:shadow-md hover:shadow-brand-500/10 dark:border-white/10 dark:bg-white/5 dark:hover:border-brand-400 cursor-pointer"
              >
                <div>
                  {/* Top card metadata */}
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex items-center gap-1.5 flex-wrap">
                      <span
                        className={`inline-flex items-center gap-1 rounded-md px-2 py-0.5 text-[10px] font-bold ${
                          isAnalyzed
                            ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-300'
                            : 'bg-amber-50 text-amber-700 dark:bg-amber-500/20 dark:text-amber-300'
                        }`}
                      >
                        {isAnalyzed ? <CheckCircle2 className="h-3 w-3" /> : <Clock className="h-3 w-3" />}
                        {proc.status || 'Borrador'}
                      </span>
                      {proc.version && (
                        <span className="rounded-md bg-slate-100 px-1.5 py-0.5 text-[10px] font-semibold text-slate-600 dark:bg-white/10 dark:text-white/60">
                          v{proc.version}
                        </span>
                      )}
                      {proc.code && (
                        <span className="rounded-md bg-violet-50 px-1.5 py-0.5 text-[10px] font-semibold text-violet-700 dark:bg-violet-500/20 dark:text-violet-300">
                          {proc.code}
                        </span>
                      )}
                    </div>

                    <button
                      type="button"
                      title="Eliminar proceso"
                      onClick={(e) => handleDelete(proc, e)}
                      className="rounded-lg p-1.5 text-slate-400 transition hover:bg-rose-50 hover:text-rose-600 dark:hover:bg-rose-500/20 dark:hover:text-rose-300"
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>

                  {/* Title & Description */}
                  <div className="mt-3">
                    <h2 className="text-sm font-bold text-slate-900 group-hover:text-brand-600 dark:text-white dark:group-hover:text-brand-400 transition-colors line-clamp-2">
                      {proc.title}
                    </h2>
                    <p className="mt-1 text-xs text-slate-500 dark:text-white/60 line-clamp-2">
                      Manual operativo registrado con pasos estructurados y competencias de trabajo.
                    </p>
                  </div>

                  {/* Metrics Pills */}
                  <div className="mt-4 grid grid-cols-2 gap-2 border-t border-slate-100 pt-3 dark:border-white/5">
                    <div className="flex items-center gap-2 rounded-xl bg-slate-50 p-2 dark:bg-white/[0.03]">
                      <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-brand-100 text-brand-700 dark:bg-brand-500/20 dark:text-brand-300">
                        <Layers className="h-3.5 w-3.5" />
                      </span>
                      <div>
                        <p className="text-xs font-bold text-slate-900 dark:text-white">
                          {proc.totalStepsCount || 0}
                        </p>
                        <p className="text-[10px] text-slate-400">Pasos</p>
                      </div>
                    </div>

                    <div className="flex items-center gap-2 rounded-xl bg-slate-50 p-2 dark:bg-white/[0.03]">
                      <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-teal-100 text-teal-700 dark:bg-teal-500/20 dark:text-teal-300">
                        <GraduationCap className="h-3.5 w-3.5" />
                      </span>
                      <div>
                        <p className="text-xs font-bold text-slate-900 dark:text-white">
                          {proc.uniqueSkillsCount || 0}
                        </p>
                        <p className="text-[10px] text-slate-400">Skills Matriz</p>
                      </div>
                    </div>
                  </div>
                </div>

                {/* Bottom Actions */}
                <div className="mt-5 flex items-center justify-between border-t border-slate-100 pt-3 dark:border-white/5">
                  <div className="flex items-center gap-1.5 text-[10px] text-slate-400">
                    <Calendar className="h-3 w-3" />
                    <span>{formattedDate}</span>
                  </div>

                  <div className="flex items-center gap-2">
                    <button
                      type="button"
                      onClick={(e) => {
                        e.stopPropagation();
                        onViewSummary(proc.processDocumentId);
                      }}
                      className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-2.5 py-1 text-xs font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white dark:hover:bg-white/10"
                    >
                      <BarChart3 className="h-3.5 w-3.5 text-teal-600 dark:text-teal-400" />
                      <span>Matriz</span>
                    </button>

                    <button
                      type="button"
                      onClick={(e) => {
                        e.stopPropagation();
                        onEditProcess(proc.processDocumentId);
                      }}
                      className="inline-flex items-center gap-1 rounded-lg bg-brand-600 px-3 py-1 text-xs font-bold text-white shadow-sm hover:bg-brand-500 transition-colors"
                    >
                      <span>Abrir y Editar</span>
                      <ArrowRight className="h-3 w-3" />
                    </button>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
