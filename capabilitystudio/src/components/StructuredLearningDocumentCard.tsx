import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { FileText, Layers } from 'lucide-react';
import type { StructuredLearningDocumentSummary } from '../lib/api/structuredLearningApi';
import { apiImageUrl } from '../lib/api/httpClient';

/** Mirrors CapabilityCard's visual style (2026-08-10) — Structured
 * Learning documents have no Capability record yet (see repo memory), so
 * this is a separate lightweight card, purely additive to the catalog. */
export default function StructuredLearningDocumentCard({ document }: { document: StructuredLearningDocumentSummary }) {
  const { t, i18n } = useTranslation();
  const createdLabel = new Date(document.CreatedDate).toLocaleString(i18n.language, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });

  return (
    <Link
      to={`/structured-learning/${document.StructuredLearningDocumentId}`}
      className="group relative flex flex-col overflow-hidden rounded-3xl border border-white/10 bg-white/[0.03] transition-all hover:-translate-y-1 hover:border-white/20 hover:bg-white/[0.06] hover:shadow-2xl hover:shadow-brand-500/10"
    >
      <div className="relative h-36 w-full overflow-hidden bg-gradient-to-br from-teal-600 to-emerald-700">
        {document.CoverIllustrationId ? (
          <img
            src={apiImageUrl(`/illustrations/structured-learning/${document.CoverIllustrationId}/image`)}
            alt=""
            className="h-full w-full object-contain object-center opacity-90 transition-transform duration-500 group-hover:scale-105"
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center">
            <FileText className="h-12 w-12 text-white/70" strokeWidth={1.5} />
          </div>
        )}
        <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-transparent to-transparent" />
        <span className="absolute right-3 top-3 flex items-center gap-1 rounded-full bg-black/40 px-2.5 py-1 text-[11px] font-medium text-white backdrop-blur-sm">
          <FileText className="h-3 w-3" />
          {t('capabilityCard.structuredLearningBadge')}
        </span>
      </div>

      <div className="flex flex-1 flex-col gap-2 p-5">
        <h3 className="font-semibold text-white leading-snug line-clamp-2">{document.DocumentName}</h3>

        <span className="text-[11px] text-slate-500" title={t('capabilityCard.createdOn', { date: createdLabel })}>
          {t('capabilityCard.createdOn', { date: createdLabel })}
        </span>

        <div className="mt-auto flex items-center gap-4 pt-3 text-xs text-slate-500">
          <span className="flex items-center gap-1.5">
            <Layers className="h-3.5 w-3.5" />
            {t('capabilityCard.sections', { count: document.SectionCount })}
          </span>
        </div>
      </div>
    </Link>
  );
}
