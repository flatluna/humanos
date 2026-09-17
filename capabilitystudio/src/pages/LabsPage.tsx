import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { FlaskConical, Sparkles, Trash2 } from 'lucide-react';
import { deleteLab, listLabs, type LabListItem, type LabStatus } from '../lib/api/labsApi';

const STATUS_STYLES: Record<LabStatus, string> = {
  DRAFT: 'bg-slate-500/15 text-slate-300',
  PUBLISHED: 'bg-emerald-500/15 text-emerald-300',
  RETIRED: 'bg-amber-500/15 text-amber-300',
};

export default function LabsPage() {
  const { t, i18n } = useTranslation();
  const isEn = i18n.language.startsWith('en');
  const [labs, setLabs] = useState<LabListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [confirmingId, setConfirmingId] = useState<string | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  async function handleDelete(idLab: string) {
    setDeletingId(idLab);
    setDeleteError(null);
    try {
      await deleteLab(idLab);
      setLabs((prev) => prev.filter((l) => l.IdLab !== idLab));
      setConfirmingId(null);
    } catch (err) {
      setDeleteError((err as Error).message);
    } finally {
      setDeletingId(null);
    }
  }

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    listLabs()
      .then((response) => {
        if (!cancelled) setLabs(response.Items);
      })
      .catch((err: Error) => {
        if (!cancelled) setError(err.message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <div className="mx-auto max-w-5xl px-4 py-8 sm:px-6">
      <div className="mb-6 flex items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <FlaskConical className="h-7 w-7 text-teal-400" />
          <div>
            <h1 className="text-xl font-semibold text-white">{t('nav.labs')}</h1>
            <p className="text-sm text-slate-400">{t('labs.subtitle')}</p>
          </div>
        </div>
        <Link
          to="/labs/new-ai"
          className="inline-flex items-center gap-2 rounded-lg bg-teal-500 px-3 py-2 text-sm font-medium text-slate-950 hover:bg-teal-400"
        >
          <Sparkles className="h-4 w-4" /> {t('labs.generateWithAi')}
        </Link>
      </div>

      <div className="rounded-2xl border border-white/10 bg-slate-900/60 p-4">
        {loading ? (
          <p className="text-sm text-slate-400">{t('labs.loading')}</p>
        ) : error ? (
          <p className="text-sm text-red-400">{error}</p>
        ) : labs.length === 0 ? (
          <p className="text-sm text-slate-400">{t('labs.empty')}</p>
        ) : (
          <>
          {deleteError && <p className="mb-3 rounded-lg bg-red-500/10 p-2 text-xs text-red-300 border border-red-500/30">{deleteError}</p>}
          <ul className="divide-y divide-white/5">
            {labs.map((lab) => (
              <li key={lab.IdLab} className="flex items-center justify-between gap-4 py-3">
                <div>
                  <p className="text-sm font-medium text-white">{lab.Nombre}</p>
                  <p className="text-xs text-slate-500">
                    {lab.Codigo} · {lab.Dominio}
                  </p>
                </div>
                <div className="flex items-center gap-3">
                  <span className={`rounded-full px-3 py-1 text-xs font-medium ${STATUS_STYLES[lab.Estatus]}`}>
                    {lab.Estatus}
                  </span>
                  <Link
                    to={`/labs/${lab.IdLab}`}
                    className="rounded-lg border border-teal-400/30 px-3 py-1.5 text-xs font-medium text-teal-300 hover:bg-teal-400/10"
                  >
                    {lab.Estatus === 'DRAFT' ? (isEn ? 'Open draft' : 'Abrir borrador') : (isEn ? 'Open' : 'Abrir')}
                  </Link>
                  {confirmingId === lab.IdLab ? (
                    <div className="flex items-center gap-1.5">
                      <span className="text-xs text-red-300">{isEn ? 'Delete permanently?' : '¿Borrar permanentemente?'}</span>
                      <button
                        type="button"
                        disabled={deletingId === lab.IdLab}
                        onClick={() => handleDelete(lab.IdLab)}
                        className="rounded-lg border border-red-400/40 bg-red-500/20 px-2.5 py-1.5 text-xs font-semibold text-red-100 hover:bg-red-500/30 disabled:opacity-50"
                      >
                        {deletingId === lab.IdLab ? (isEn ? 'Deleting...' : 'Borrando...') : (isEn ? 'Yes, delete' : 'Sí, borrar')}
                      </button>
                      <button
                        type="button"
                        disabled={deletingId === lab.IdLab}
                        onClick={() => setConfirmingId(null)}
                        className="rounded-lg border border-white/10 px-2.5 py-1.5 text-xs text-slate-300 hover:bg-white/5"
                      >
                        {isEn ? 'Cancel' : 'Cancelar'}
                      </button>
                    </div>
                  ) : (
                    <button
                      type="button"
                      onClick={() => setConfirmingId(lab.IdLab)}
                      className="rounded-lg border border-white/10 p-1.5 text-slate-400 hover:bg-red-500/10 hover:text-red-300"
                      title={isEn ? 'Permanently delete Lab' : 'Eliminar Lab permanentemente'}
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </button>
                  )}
                </div>
              </li>
            ))}
          </ul>
          </>
        )}
      </div>
    </div>
  );
}
