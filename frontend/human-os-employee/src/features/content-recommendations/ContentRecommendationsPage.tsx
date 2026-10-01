import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Sparkles, Mail, TrendingUp, Send, Loader2 } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import {
  generateContentRecommendations,
  getContentRecommendation,
  listContentRecommendations,
  sendContentRecommendationEmail,
  type ContentRecommendationProposal,
} from './api/contentRecommendationsApi';

/** One entry in the on-screen results history: a generated/reopened
 * recommendation report plus its saved-search id (for "send by email"). */
interface ResultEntry {
  key: string;
  savedSearchId: string | null;
  proposal: ContentRecommendationProposal;
}

/** "Recomienda contenido relevante" (2026-09-29) — abreviado en el sidebar como
 *  "Recomendaciones". Genera contenido relevante para el estudiante según sus
 *  gustos, intereses, desempeño y tendencias de aprendizaje, apoyado en
 *  búsqueda web en tiempo real (Perplexity), puede enviarse por correo en un
 *  formato guardable, y guarda un historial de búsquedas pasadas. Cada nueva
 *  pregunta se AGREGA arriba del historial en pantalla — nunca reemplaza ni
 *  borra lo que una búsqueda anterior ya respondió. */
export function ContentRecommendationsPage() {
  const { user } = useAuth();
  const personId = user?.personId ?? '';
  const queryClient = useQueryClient();
  const [prompt, setPrompt] = useState('');
  const [sendEmailOnGenerate, setSendEmailOnGenerate] = useState(false);
  const [results, setResults] = useState<ResultEntry[]>([]);

  const savedSearchesQuery = useQuery({
    queryKey: ['content-recommendations', personId],
    queryFn: () => listContentRecommendations(personId),
    enabled: Boolean(personId),
  });

  const generateMutation = useMutation({
    mutationFn: () => generateContentRecommendations(personId, prompt.trim() || undefined, 'es', sendEmailOnGenerate),
    onSuccess: (result) => {
      const { savedSearchId, ...proposal } = result;
      setResults((prev) => [{ key: crypto.randomUUID(), savedSearchId, proposal }, ...prev]);
      setPrompt('');
      queryClient.invalidateQueries({ queryKey: ['content-recommendations', personId] });
    },
  });

  const openSavedMutation = useMutation({
    mutationFn: (savedSearchId: string) => getContentRecommendation(personId, savedSearchId),
    onSuccess: (proposal, savedSearchId) => {
      // Reopening a saved search also just adds it to the top of the
      // on-screen history instead of replacing whatever is already shown.
      setResults((prev) => [{ key: crypto.randomUUID(), savedSearchId, proposal }, ...prev]);
    },
  });

  const sendEmailMutation = useMutation({
    mutationFn: (savedSearchId: string) => sendContentRecommendationEmail(personId, savedSearchId),
  });
  const [sentKeys, setSentKeys] = useState<Set<string>>(new Set());

  return (
    <div className="mx-auto grid max-w-6xl gap-6 p-6 lg:grid-cols-[280px_1fr]">
      <aside className="lg:sticky lg:top-6 lg:self-start">
        <Card className="p-4">
          <h2 className="mb-3 text-sm font-semibold text-slate-700 dark:text-white/80">Búsquedas guardadas</h2>
          {savedSearchesQuery.isLoading && <p className="text-xs text-slate-400">Cargando...</p>}
          {savedSearchesQuery.data?.length === 0 && (
            <p className="text-xs text-slate-400 dark:text-white/40">Aún no tienes recomendaciones guardadas.</p>
          )}
          <ul className="space-y-1.5">
            {savedSearchesQuery.data?.map((item) => (
              <li key={item.contentRecommendationSavedSearchId}>
                <button
                  type="button"
                  onClick={() => openSavedMutation.mutate(item.contentRecommendationSavedSearchId)}
                  className="w-full rounded-lg px-3 py-2 text-left text-xs text-slate-600 transition hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/5"
                >
                  <span className="block font-medium">{item.topic}</span>
                  <span className="block text-[11px] text-slate-400 dark:text-white/40">
                    {new Date(item.createdDate).toLocaleDateString()}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </Card>
      </aside>

      <section className="space-y-6">
        <Card className="p-6">
          <div className="mb-4 flex items-center gap-2">
            <Sparkles className="h-5 w-5 text-fuchsia-500" />
            <h1 className="text-lg font-semibold text-slate-800 dark:text-white">Recomienda contenido relevante</h1>
          </div>
          <p className="mb-4 text-sm text-slate-500 dark:text-white/60">
            Recomendaciones según tus gustos, intereses, desempeño y las últimas tendencias de tu área.
          </p>
          <textarea
            value={prompt}
            onChange={(e) => setPrompt(e.target.value)}
            placeholder="Opcional: cuéntame qué te interesa ahora mismo..."
            className="mb-3 w-full rounded-lg border border-slate-200 p-3 text-sm dark:border-white/10 dark:bg-white/5"
            rows={2}
          />
          <div className="mb-3 flex items-center gap-2">
            <input
              id="sendEmailOnGenerate"
              type="checkbox"
              checked={sendEmailOnGenerate}
              onChange={(e) => setSendEmailOnGenerate(e.target.checked)}
              className="h-3.5 w-3.5"
            />
            <label htmlFor="sendEmailOnGenerate" className="text-xs text-slate-500 dark:text-white/60">
              Enviarme una copia por correo (formato guardable)
            </label>
          </div>
          <button
            type="button"
            disabled={generateMutation.isPending || !personId}
            onClick={() => generateMutation.mutate()}
            className="flex items-center gap-2 rounded-lg bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50"
          >
            {generateMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Sparkles className="h-4 w-4" />}
            Generar recomendaciones
          </button>
          {generateMutation.isError && (
            <p className="mt-2 text-xs text-rose-500">No se pudieron generar recomendaciones. Intenta de nuevo.</p>
          )}
        </Card>

        {results.map((entry) => (
          <Card key={entry.key} className="space-y-4 p-6">
            <div>
              <h2 className="text-base font-semibold text-slate-800 dark:text-white">{entry.proposal.title}</h2>
              <p className="mt-1 text-sm text-slate-600 dark:text-white/70">{entry.proposal.summary}</p>
            </div>

            <div className="rounded-lg bg-amber-50 p-3 dark:bg-amber-500/10">
              <div className="mb-1 flex items-center gap-1.5 text-xs font-semibold text-amber-700 dark:text-amber-300">
                <TrendingUp className="h-3.5 w-3.5" /> Tendencias del área
              </div>
              <p className="text-xs text-amber-800 dark:text-amber-200/80">{entry.proposal.trendsSummary}</p>
            </div>

            <ul className="space-y-3">
              {entry.proposal.recommendedItems.map((item, index) => (
                <li key={index} className="rounded-lg border border-slate-100 p-3 dark:border-white/10">
                  <span className="mb-1 inline-block rounded-full bg-slate-100 px-2 py-0.5 text-[10px] font-medium uppercase text-slate-500 dark:bg-white/10 dark:text-white/50">
                    {item.type}
                  </span>
                  <h3 className="text-sm font-semibold text-slate-800 dark:text-white">{item.title}</h3>
                  <p className="text-xs text-slate-500 dark:text-white/60">{item.reason}</p>
                  {item.url && (
                    <a href={item.url} target="_blank" rel="noreferrer" className="text-xs text-brand-600 hover:underline">
                      {item.url}
                    </a>
                  )}
                </li>
              ))}
            </ul>

            <div className="flex items-center gap-3 border-t border-slate-100 pt-3 dark:border-white/10">
              <button
                type="button"
                disabled={!entry.savedSearchId || sendEmailMutation.isPending}
                onClick={() =>
                  entry.savedSearchId &&
                  sendEmailMutation.mutate(entry.savedSearchId, {
                    onSuccess: () => setSentKeys((prev) => new Set(prev).add(entry.key)),
                  })
                }
                className="flex items-center gap-2 rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-medium text-slate-600 hover:bg-slate-50 disabled:opacity-50 dark:border-white/10 dark:text-white/70 dark:hover:bg-white/5"
              >
                {sendEmailMutation.isPending ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Send className="h-3.5 w-3.5" />}
                Enviar por correo
              </button>
              {sentKeys.has(entry.key) && (
                <span className="flex items-center gap-1 text-xs text-emerald-600">
                  <Mail className="h-3.5 w-3.5" /> Enviado
                </span>
              )}
              {!entry.savedSearchId && (
                <span className="text-xs text-slate-400 dark:text-white/40">
                  Esta recomendación no se pudo guardar, así que no se puede reenviar por correo.
                </span>
              )}
            </div>
          </Card>
        ))}
      </section>
    </div>
  );
}

