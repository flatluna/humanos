import { BarChart3 } from 'lucide-react';

export function ReportsPage() {
  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">Reportes de Brechas</h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
          Visibilidad de qué capacidades/certificaciones le faltan a cada persona frente a los requisitos de su Rol.
        </p>
      </div>

      <div className="rounded-2xl border border-dashed border-rose-200 bg-gradient-to-br from-rose-50/60 to-white p-8 text-center dark:border-rose-500/20 dark:from-rose-500/5 dark:to-transparent">
        <span className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-gradient-to-br from-rose-500 to-rose-400 text-white shadow-md shadow-rose-500/30">
          <BarChart3 className="h-6 w-6" />
        </span>
        <p className="mt-4 text-sm font-semibold text-slate-900 dark:text-white">Próximamente</p>
        <p className="mx-auto mt-1 max-w-md text-sm text-slate-500 dark:text-white/50">
          Este dashboard necesita primero el modelo Role↔Capability requirement (fase 2) para poder comparar
          capacidades requeridas vs. capacidades reales de cada persona.
        </p>
      </div>
    </div>
  );
}
