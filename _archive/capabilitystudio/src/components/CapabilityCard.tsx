import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Layers, ImageOff, Baby, BookOpen, Clock } from 'lucide-react';
import type { BackendCapability } from '../lib/api/capabilitiesApi';
import { apiImageUrl } from '../lib/api/httpClient';
import { getSubjectGradient, getSubjectIcon } from '../lib/subjectVisuals';

export default function CapabilityCard({ capability }: {
  capability: BackendCapability;
  selected?: boolean;
  onToggle?: () => void;
}) {
  const { t } = useTranslation();
  const SubjectIcon = getSubjectIcon(capability.SubjectCode);
  const gradient = getSubjectGradient(capability.SubjectCode);
  const isTraditional = capability.LearningMode === 'TraditionalChapters';
  const isTenantOwned = capability.OwnerTenantId !== null;
  const isPersonal = !isTenantOwned && capability.OwnerPersonId !== null;
  const estMinutes = isTraditional
    ? Math.max(10, (capability.LevelCount || 1) * 12)
    : Math.max(10, ((capability.LevelCount || 1) + (capability.NodeCount || 1)) * 5);

  return (
    <Link
      to={isTraditional ? `/capabilities/${capability.CapabilityId}/traditional-course` : `/capabilities/${capability.CapabilityId}`}
      className="group relative flex flex-col overflow-hidden rounded-2xl border border-white/10 bg-white/[0.03] shadow-md transition-all duration-300 hover:-translate-y-1 hover:border-brand-500/40 hover:bg-white/[0.05] hover:shadow-2xl hover:shadow-brand-500/10"
    >
      <div className={`relative aspect-[16/10] w-full overflow-hidden bg-slate-900`}>
        {capability.HasCoverImage ? (
          <img
            src={apiImageUrl(`/capabilities/${capability.CapabilityId}/cover-image`)}
            alt={capability.Name}
            className="h-full w-full object-cover object-center transition-transform duration-500 group-hover:scale-105"
          />
        ) : (
          <div className={`flex h-full w-full items-center justify-center bg-gradient-to-br ${gradient}`}>
            <SubjectIcon className="h-12 w-12 text-white/70" strokeWidth={1.5} />
          </div>
        )}
        <div className="absolute inset-0 bg-gradient-to-t from-black/75 via-transparent to-black/20" />

        <div className="absolute inset-x-3 top-3 flex items-center justify-between pointer-events-none">
          <div className="flex items-center gap-1.5 pointer-events-auto">
            {capability.SubjectCode && (
              <span className="flex items-center gap-1 rounded-full border border-white/20 bg-black/60 px-2.5 py-1 text-[11px] font-semibold text-[#fff] backdrop-blur-md shadow-sm">
                <SubjectIcon className="h-3 w-3" />
                {capability.SubjectCode}
              </span>
            )}
            <span
              className="flex items-center gap-1 rounded-full border border-white/20 bg-black/60 px-2.5 py-1 text-[11px] font-semibold text-[#fff] backdrop-blur-md shadow-sm"
              title={t(isTenantOwned ? 'capabilityCard.tenantHint' : isPersonal ? 'capabilityCard.personalHint' : 'capabilityCard.globalHint')}
            >
              {t(isTenantOwned ? 'capabilityCard.tenantBadge' : isPersonal ? 'capabilityCard.personalBadge' : 'capabilityCard.globalBadge')}
            </span>
          </div>

          <div className="flex items-center gap-1 pointer-events-auto">
            <span
              className="rounded-full border border-white/20 bg-black/60 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-[#fff] backdrop-blur-md shadow-sm"
              title={capability.ContentLanguageCode === 'en' ? t('capabilityCard.contentInEnglish') : t('capabilityCard.contentInSpanish')}
            >
              {capability.ContentLanguageCode === 'en' ? 'EN' : 'ES'}
            </span>
          </div>
        </div>

        {capability.PedagogicalAudience === 'YoungLearner' && (
          <span
            className="absolute left-3 bottom-3 flex items-center gap-1 rounded-full border border-white/15 bg-black/50 px-2.5 py-1 text-[11px] font-medium text-white backdrop-blur-md shadow-sm"
            title={t('capabilityCard.youngLearnerAudience')}
          >
            <Baby className="h-3 w-3" />
            {t('capabilityCard.youngLearnerBadge')}
          </span>
        )}
      </div>

      <div className="flex flex-1 flex-col gap-2 p-5">
        <h3 className="text-base font-bold text-white leading-snug line-clamp-2 transition-colors group-hover:text-brand-400">
          {capability.Name}
        </h3>
        {capability.LearningSummary && (
          <p className="text-sm text-slate-400 line-clamp-2">{capability.LearningSummary}</p>
        )}

        <div className="mt-auto pt-3 border-t border-white/10 flex items-center justify-between text-xs">
          <div className="flex items-center gap-3">
            {isTraditional ? (
              <span className="inline-flex items-center gap-1.5 font-semibold text-slate-200">
                <BookOpen className="h-4 w-4 text-brand-400" />
                {capability.LevelCount || 0} {capability.LevelCount === 1 ? 'Capítulo' : 'Capítulos'}
              </span>
            ) : (
              <span className="inline-flex items-center gap-1.5 font-semibold text-slate-200">
                <Layers className="h-4 w-4 text-brand-400" />
                {capability.NodeCount || capability.LevelCount || 0} Nodos
              </span>
            )}
            <span className="inline-flex items-center gap-1.5 font-medium text-slate-400">
              <Clock className="h-4 w-4 text-amber-400" />
              ~{estMinutes} min
            </span>
          </div>

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
