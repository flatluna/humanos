import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BrainCircuit, FileUp, Sparkles, Loader2, RefreshCw } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { getMyCqiProfile, uploadCqiReport, type CqiProfileData } from './api/cqiApi';

const SECTION_ORDER: { key: keyof CqiProfileData; title: { en: string; es: string }; list?: boolean }[] = [
  { key: 'profileSnapshot', title: { en: 'Profile Snapshot', es: 'Resumen de Perfil' } },
  { key: 'howTheyTendToWork', title: { en: 'How This Person Tends To Work', es: 'Cómo Suele Trabajar' } },
  { key: 'thinkingAndDecisions', title: { en: 'Thinking and Decisions', es: 'Pensamiento y Decisiones' } },
  { key: 'communicationAndCollaboration', title: { en: 'Communication and Collaboration', es: 'Comunicación y Colaboración' } },
  { key: 'learningAndWorkingPreferences', title: { en: 'Learning and Working Preferences', es: 'Preferencias de Aprendizaje y Trabajo' } },
  { key: 'naturalStrengths', title: { en: 'Natural Strengths', es: 'Fortalezas Naturales' }, list: true },
  { key: 'patternsWorthNoticing', title: { en: 'Patterns Worth Noticing', es: 'Patrones a Observar' }, list: true },
  { key: 'workingEffectivelyWithAi', title: { en: 'Working Effectively With AI', es: 'Trabajar Efectivamente con IA' } },
  { key: 'whatHelpsThemThrive', title: { en: 'What Helps Them Thrive', es: 'Qué les Ayuda a Prosperar' } },
  { key: 'whatStrainCanLookLike', title: { en: 'What Strain Can Look Like', es: 'Cómo Puede Verse el Estrés' } },
  { key: 'growthDirection', title: { en: 'Growth Direction', es: 'Dirección de Crecimiento' } },
];

/** "CQI Workspace" (HermanScience) — upload a CQI assessment report PDF,
 * have CqiReportExtractionAgent pull out its structured insights, and show
 * both the shareable-report-style preview and the anchor foundation. */
export function CqiWorkspacePage() {
  const { language } = useI18n();
  const isEn = language === 'en';
  const { user } = useAuth();
  const personId = user?.personId ?? '';
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);

  const profileQuery = useQuery({
    queryKey: ['my-cqi-profile', personId],
    queryFn: () => getMyCqiProfile(personId),
    enabled: !!personId,
    // Always hit the backend (which reads PersonCqiProfile straight from
    // SQL) instead of trusting a cached copy — this is what "Refresh"
    // below, and every fresh page load, actually re-runs.
    staleTime: 0,
    gcTime: 0,
  });

  const uploadMutation = useMutation({
    mutationFn: (file: File) => uploadCqiReport(personId, file),
    onSuccess: (result) => {
      queryClient.setQueryData(['my-cqi-profile', personId], result);
      setError(null);
    },
    onError: (err) => setError(err instanceof Error ? err.message : 'Upload failed'),
  });

  function handleFileChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (file) uploadMutation.mutate(file);
  }

  const profile = profileQuery.data?.profile ?? null;

  return (
    <div className="mx-auto max-w-5xl space-y-6 p-4 pb-16 sm:p-8">
      <div className="overflow-hidden rounded-3xl border border-slate-200 bg-gradient-to-br from-violet-600 via-fuchsia-600 to-rose-500 p-6 text-white shadow-lg shadow-violet-600/20 dark:border-white/10 sm:p-8">
        <div className="flex items-center gap-4">
          <span className="flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl bg-white/15 ring-1 ring-white/25 backdrop-blur">
            <BrainCircuit className="h-8 w-8 text-white" />
          </span>
          <div>
            <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-white/80">
              <Sparkles className="h-3.5 w-3.5" />
              HermanScience
            </p>
            <h1 className="mt-1 text-2xl font-bold text-white">CQI Workspace</h1>
            <p className="mt-1 max-w-2xl text-sm text-white/85">
              {isEn
                ? 'Understand how you currently think, learn, communicate, make decisions, and work — turning your CQI assessment results into practical insights for learning, collaboration, and capability development.'
                : 'Entiende cómo piensas, aprendes, te comunicas, decides y trabajas actualmente — convirtiendo los resultados de tu evaluación CQI en información práctica para el aprendizaje, la colaboración y el desarrollo de capacidades.'}
            </p>
          </div>
        </div>
      </div>

      <Card className="flex flex-col items-center justify-between gap-4 p-6 sm:flex-row">
        <div>
          <h2 className="text-base font-semibold text-slate-900 dark:text-white">
            {isEn ? 'Upload your CQI report' : 'Sube tu reporte CQI'}
          </h2>
          <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
            {isEn ? 'Upload the PDF you received from HermanScience — we will extract your profile automatically.' : 'Sube el PDF que recibiste de HermanScience — extraeremos tu perfil automáticamente.'}
          </p>
          {profileQuery.data?.sourceFileName && (
            <p className="mt-1 text-xs text-slate-400 dark:text-white/40">
              {isEn ? 'Last uploaded: ' : 'Última carga: '}
              {profileQuery.data.sourceFileName}
            </p>
          )}
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => void profileQuery.refetch()}
            disabled={profileQuery.isFetching}
            title={isEn ? 'Reload from the database' : 'Recargar desde la base de datos'}
            className="inline-flex items-center gap-2 rounded-xl border border-slate-200 px-4 py-2.5 text-sm font-semibold text-slate-600 transition hover:bg-slate-100 disabled:opacity-60 dark:border-white/10 dark:text-white/70 dark:hover:bg-white/5"
          >
            <RefreshCw className={`h-4 w-4 ${profileQuery.isFetching ? 'animate-spin' : ''}`} />
            {isEn ? 'Refresh' : 'Actualizar'}
          </button>
          <label className="inline-flex cursor-pointer items-center gap-2 rounded-xl bg-gradient-to-r from-violet-600 to-fuchsia-600 px-5 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:opacity-90">
            {uploadMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <FileUp className="h-4 w-4" />}
            {uploadMutation.isPending ? (isEn ? 'Processing…' : 'Procesando…') : isEn ? 'Upload PDF' : 'Subir PDF'}
            <input type="file" accept="application/pdf" className="hidden" onChange={handleFileChange} disabled={uploadMutation.isPending} />
          </label>
        </div>
      </Card>

      {error && (
        <div className="rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
          {error}
        </div>
      )}

      {!profile && !profileQuery.isLoading && (
        <Card className="flex flex-col items-center justify-center p-12 text-center border-dashed">
          <BrainCircuit className="h-10 w-10 text-slate-300 dark:text-white/20" />
          <h3 className="mt-3 text-base font-semibold text-slate-900 dark:text-white">
            {isEn ? 'No CQI report uploaded yet' : 'Aún no has subido un reporte CQI'}
          </h3>
        </Card>
      )}

      {profile && (
        <>
          <Card className="p-6">
            <p className="text-xs font-semibold uppercase tracking-wide text-violet-600 dark:text-violet-300">
              {isEn ? 'Leading Pattern' : 'Patrón Predominante'}
            </p>
            <h2 className="mt-1 text-xl font-bold text-slate-900 dark:text-white">
              {profile.leadingPatternType} — {profile.leadingPatternName}
            </h2>
            {profile.wingInfluence && (
              <p className="mt-1 text-sm text-slate-500 dark:text-white/50">{profile.wingInfluence}</p>
            )}
          </Card>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            {SECTION_ORDER.map(({ key, title, list }, index) => {
              const value = profile[key];
              if (!value || (Array.isArray(value) && value.length === 0)) return null;
              return (
                <Card key={key} className="p-5">
                  <p className="text-[11px] font-bold uppercase tracking-wider text-slate-400 dark:text-white/40">
                    {String(index + 1).padStart(2, '0')}
                  </p>
                  <h3 className="mt-1 text-sm font-bold text-slate-900 dark:text-white">
                    {isEn ? title.en : title.es}
                  </h3>
                  {list ? (
                    <ul className="mt-2 list-disc space-y-1 pl-4 text-sm text-slate-600 dark:text-white/70">
                      {(value as string[]).map((item) => (
                        <li key={item}>{item}</li>
                      ))}
                    </ul>
                  ) : (
                    <p className="mt-2 text-sm leading-relaxed text-slate-600 dark:text-white/70">{value as string}</p>
                  )}
                </Card>
              );
            })}
          </div>

          <Card className="space-y-4 p-6">
            <h2 className="text-lg font-bold text-slate-900 dark:text-white">
              {isEn ? 'Anchor Foundation' : 'Fundamento de Anclaje'}
            </h2>
            {profile.anchorTypeName && (
              <p className="text-base font-semibold text-violet-700 dark:text-violet-300">{profile.anchorTypeName}</p>
            )}
            {profile.anchorDescription && <p className="text-sm text-slate-600 dark:text-white/70">{profile.anchorDescription}</p>}

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              {profile.whatMayDrawAttention && (
                <AnchorField label={isEn ? 'What may draw attention' : 'Qué puede captar la atención'} value={profile.whatMayDrawAttention} />
              )}
              {profile.howChoicesMayTakeShape && (
                <AnchorField label={isEn ? 'How choices may take shape' : 'Cómo pueden tomar forma las decisiones'} value={profile.howChoicesMayTakeShape} />
              )}
              {profile.workAndCommunication && (
                <AnchorField label={isEn ? 'Work and communication' : 'Trabajo y comunicación'} value={profile.workAndCommunication} />
              )}
              {profile.whatMaySupportBalance && (
                <AnchorField label={isEn ? 'What may support balance' : 'Qué puede apoyar el equilibrio'} value={profile.whatMaySupportBalance} />
              )}
            </div>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              {profile.potentialStrengths.length > 0 && (
                <ListField label={isEn ? 'Potential strengths' : 'Fortalezas potenciales'} items={profile.potentialStrengths} />
              )}
              {profile.patternsToWatchUnderPressure.length > 0 && (
                <ListField label={isEn ? 'Patterns to watch under pressure' : 'Patrones a observar bajo presión'} items={profile.patternsToWatchUnderPressure} />
              )}
            </div>
          </Card>

          <details className="rounded-2xl border border-slate-200 bg-white p-4 text-xs dark:border-white/10 dark:bg-white/[0.03]">
            <summary className="cursor-pointer font-semibold text-slate-500 dark:text-white/50">
              {isEn ? 'Raw JSON' : 'JSON sin procesar'}
            </summary>
            <pre className="mt-3 overflow-auto whitespace-pre-wrap text-slate-600 dark:text-white/60">
              {JSON.stringify(profile, null, 2)}
            </pre>
          </details>
        </>
      )}
    </div>
  );
}

function AnchorField({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-xs font-semibold text-slate-500 dark:text-white/50">{label}</p>
      <p className="mt-1 text-sm text-slate-600 dark:text-white/70">{value}</p>
    </div>
  );
}

function ListField({ label, items }: { label: string; items: string[] }) {
  return (
    <div>
      <p className="text-xs font-semibold text-slate-500 dark:text-white/50">{label}</p>
      <ul className="mt-1 list-disc space-y-1 pl-4 text-sm text-slate-600 dark:text-white/70">
        {items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
    </div>
  );
}
