import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Layers, Clock, Sparkles } from 'lucide-react';
import type { StructuredLearningDocumentSummary } from '../lib/api/structuredLearningApi';
import { apiImageUrl } from '../lib/api/httpClient';

/**
 * Modern media-rich card for Structured Learning / Engram documents.
 * Full-bleed cover illustration with no letterboxing, clean floating pill badges,
 * and prominent Engram count + estimated duration.
 */
export default function StructuredLearningDocumentCard({
  document,
}: {
  document: StructuredLearningDocumentSummary;
  selected?: boolean;
  onToggle?: () => void;
}) {
  const { t } = useTranslation();
  const engramsCount = document.SectionCount || 0;
  const estimatedMinutes = Math.max(5, engramsCount * 6);

  return (
    <Link
      to={`/structured-learning/${document.StructuredLearningDocumentId}`}
      className="group relative flex flex-col overflow-hidden rounded-2xl border border-white/10 bg-white/[0.03] shadow-md transition-all duration-300 hover:-translate-y-1 hover:border-brand-500/40 hover:bg-white/[0.05] hover:shadow-2xl hover:shadow-brand-500/10"
    >
      <div className="relative aspect-[16/10] w-full overflow-hidden bg-slate-900">
        {document.CoverIllustrationId ? (
          <img
            src={apiImageUrl(`/illustrations/structured-learning/${document.CoverIllustrationId}/image`)}
            alt={document.DocumentName}
            className="h-full w-full object-cover object-center transition-transform duration-500 group-hover:scale-105"
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center bg-gradient-to-br from-brand-900/40 via-slate-900 to-slate-950">
            <Sparkles className="h-12 w-12 text-brand-400/60" strokeWidth={1.5} />
          </div>
        )}
        <div className="absolute inset-0 bg-gradient-to-t from-black/75 via-transparent to-black/20" />

        <div className="absolute inset-x-3 top-3 flex items-center justify-between pointer-events-none">
          <div className="flex items-center gap-1.5 pointer-events-auto">
            <span
              className="inline-flex items-center gap-1 rounded-full border border-white/20 bg-black/60 px-2.5 py-1 text-[11px] font-semibold text-[#fff] backdrop-blur-md shadow-sm"
              title={t(document.IsGlobal ? 'capabilityCard.globalHint' : 'capabilityCard.tenantHint')}
            >
              {t(document.IsGlobal ? 'capabilityCard.globalBadge' : 'capabilityCard.tenantBadge')}
            </span>
          </div>

          <span className="inline-flex items-center gap-1 rounded-full border border-emerald-400/40 bg-black/60 px-2.5 py-1 text-[11px] font-semibold text-[#34d399] backdrop-blur-md shadow-sm">
            <Sparkles className="h-3 w-3 text-emerald-400" />
            Engrams
          </span>
        </div>
      </div>

      <div className="flex flex-1 flex-col gap-2 p-5">
        <h3 className="text-base font-bold text-white leading-snug line-clamp-2 transition-colors group-hover:text-brand-400">
          {document.DocumentName}
        </h3>

        <div className="mt-auto pt-3 border-t border-white/10 flex items-center justify-between text-xs">
          <div className="flex items-center gap-3">
            <span className="inline-flex items-center gap-1.5 font-semibold text-slate-200">
              <Layers className="h-4 w-4 text-brand-400" />
              {engramsCount} {engramsCount === 1 ? 'Engram' : 'Engrams'}
            </span>
            <span className="inline-flex items-center gap-1.5 font-medium text-slate-400">
              <Clock className="h-4 w-4 text-amber-400" />
              ~{estimatedMinutes} min
            </span>
          </div>

          {document.Category && (
            <span className="rounded-full border border-white/10 bg-white/5 px-2.5 py-0.5 text-[10px] font-medium text-slate-400 truncate max-w-[120px]">
              {document.Category}
            </span>
          )}
        </div>
      </div>
    </Link>
  );
}
