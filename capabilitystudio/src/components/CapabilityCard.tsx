import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Layers, GitBranch, ImageOff, Baby } from 'lucide-react';
import type { BackendCapability } from '../lib/api/capabilitiesApi';
import { apiImageUrl } from '../lib/api/httpClient';
import { getSubjectGradient, getSubjectIcon } from '../lib/subjectVisuals';

export default function CapabilityCard({ capability }: { capability: BackendCapability }) {
  const { t, i18n } = useTranslation();
  const SubjectIcon = getSubjectIcon(capability.SubjectCode);
  const gradient = getSubjectGradient(capability.SubjectCode);
  const createdLabel = new Date(capability.CreatedDate).toLocaleString(i18n.language, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });

  return (
    <Link
      to={`/capabilities/${capability.CapabilityId}`}
      className="group relative flex flex-col overflow-hidden rounded-3xl border border-white/10 bg-white/[0.03] transition-all hover:-translate-y-1 hover:border-white/20 hover:bg-white/[0.06] hover:shadow-2xl hover:shadow-brand-500/10"
    >
      <div className={`relative h-36 w-full overflow-hidden bg-gradient-to-br ${gradient}`}>
        {capability.HasCoverImage ? (
          <img
            src={apiImageUrl(`/capabilities/${capability.CapabilityId}/cover-image`)}
            alt=""
            className="h-full w-full object-cover opacity-90 transition-transform duration-500 group-hover:scale-105"
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center">
            <SubjectIcon className="h-12 w-12 text-white/70" strokeWidth={1.5} />
          </div>
        )}
        <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-transparent to-transparent" />
        {capability.SubjectCode && (
          <span className="absolute left-3 top-3 flex items-center gap-1 rounded-full bg-black/40 px-2.5 py-1 text-[11px] font-medium text-[#fff] backdrop-blur-sm">
            <SubjectIcon className="h-3 w-3" />
            {capability.SubjectCode}
          </span>
        )}
        {capability.PedagogicalAudience === 'YoungLearner' && (
          <span
            className="absolute left-3 bottom-3 flex items-center gap-1 rounded-full bg-black/40 px-2.5 py-1 text-[11px] font-medium text-[#fff] backdrop-blur-sm"
            title={t('capabilityCard.youngLearnerAudience')}
          >
            <Baby className="h-3 w-3" />
            {t('capabilityCard.youngLearnerBadge')}
          </span>
        )}
        <span
          className="absolute right-3 top-3 rounded-full bg-black/40 px-2.5 py-1 text-[11px] font-semibold uppercase tracking-wide text-white backdrop-blur-sm"
          title={capability.ContentLanguageCode === 'en' ? t('capabilityCard.contentInEnglish') : t('capabilityCard.contentInSpanish')}
        >
          {capability.ContentLanguageCode === 'en' ? 'EN' : 'ES'}
        </span>
      </div>

      <div className="flex flex-1 flex-col gap-2 p-5">
        <h3 className="font-semibold text-white leading-snug line-clamp-2">{capability.Name}</h3>
        {capability.LearningSummary && (
          <p className="text-sm text-slate-400 line-clamp-2">{capability.LearningSummary}</p>
        )}

        <span className="text-[11px] text-slate-500" title={t('capabilityCard.createdOn', { date: createdLabel })}>
          {t('capabilityCard.createdOn', { date: createdLabel })}
        </span>

        <div className="mt-auto flex items-center gap-4 pt-3 text-xs text-slate-500">
          <span className="flex items-center gap-1.5">
            <Layers className="h-3.5 w-3.5" />
            {t('capabilityCard.levels', { count: capability.LevelCount })}
          </span>
          <span className="flex items-center gap-1.5">
            <GitBranch className="h-3.5 w-3.5" />
            {t('capabilityCard.nodes', { count: capability.NodeCount })}
          </span>
          {!capability.IsActive && (
            <span className="ml-auto flex items-center gap-1 text-amber-400">
              <ImageOff className="h-3.5 w-3.5" />
              {t('capabilityCard.inactive')}
            </span>
          )}
        </div>
      </div>
    </Link>
  );
}
