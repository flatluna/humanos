import {
  Heart,
  Target,
  Clock,
  Sparkles,
  Calendar,
  ArrowRight,
  TrendingUp,
  DollarSign,
  ChevronRight,
  MapPin,
  Flame,
} from 'lucide-react';
import { useLanguage } from '../context/LanguageContext';
import { useFamily } from '../context/FamilyContext';

export function OverviewPage() {
  const { t, lang } = useLanguage();
  const {
    members,
    goals,
    milestones,
    activeCurrency,
    selectedMemberId,
    setSelectedMemberId,
    setIsAiCoachOpen,
  } = useFamily();

  const filteredMembers = selectedMemberId
    ? members.filter((m) => m.id === selectedMemberId)
    : members;

  const filteredMilestones = selectedMemberId
    ? milestones.filter((m) => m.memberId === selectedMemberId)
    : milestones;

  const budgetDisplay =
    activeCurrency === 'USD'
      ? '$18,400 USD / $27,000'
      : '$368,000 MXN / $540,000';

  const kpis = [
    {
      label: t('overview.kpi.familyBalance'),
      value: '9.4 / 10',
      subtitle: t('overview.kpi.familyBalanceSub'),
      icon: Heart,
      accent: 'emerald',
      gradient: 'from-emerald-500 to-teal-600',
      chip: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300',
    },
    {
      label: t('overview.kpi.activeGoals'),
      value: '5',
      subtitle: t('overview.kpi.activeGoalsSub'),
      icon: Target,
      accent: 'indigo',
      gradient: 'from-indigo-500 to-violet-600',
      chip: 'bg-indigo-100 text-indigo-800 dark:bg-indigo-500/20 dark:text-indigo-300',
    },
    {
      label: t('overview.kpi.weeklyHours'),
      value: '34.5 hrs',
      subtitle: t('overview.kpi.weeklyHoursSub'),
      icon: Clock,
      accent: 'amber',
      gradient: 'from-amber-500 to-orange-600',
      chip: 'bg-amber-100 text-amber-900 dark:bg-amber-500/20 dark:text-amber-300',
    },
    {
      label: t('overview.kpi.budget'),
      value: budgetDisplay,
      subtitle: t('overview.kpi.budgetSub'),
      icon: DollarSign,
      accent: 'rose',
      gradient: 'from-rose-500 to-pink-600',
      chip: 'bg-rose-100 text-rose-800 dark:bg-rose-500/20 dark:text-rose-300',
    },
  ];

  return (
    <div className="space-y-8 animate-fade-in">
      {/* Page Heading */}
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-3xl font-extrabold tracking-tight text-warm-900 dark:text-white">
              {t('overview.greeting')}
            </h1>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-100 px-3 py-1 text-xs font-bold text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300">
              <MapPin className="h-3 w-3 text-emerald-600" />
              Austin & CDMX
            </span>
          </div>
          <p className="mt-1.5 text-sm text-warm-600 dark:text-warm-300">
            {t('overview.subgreeting')}
          </p>
        </div>

        {/* Action Button: AI Coach */}
        <button
          type="button"
          onClick={() => setIsAiCoachOpen(true)}
          className="inline-flex items-center gap-2 rounded-2xl bg-gradient-to-r from-emerald-600 via-teal-600 to-indigo-600 px-4 py-2.5 text-xs font-bold text-white shadow-md shadow-emerald-600/20 transition hover:opacity-95 hover:shadow-lg"
        >
          <Sparkles className="h-4 w-4" />
          <span>{lang === 'es' ? 'Consultar Family Coach AI' : 'Consult Family Coach AI'}</span>
        </button>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {kpis.map((kpi, idx) => {
          const Icon = kpi.icon;
          return (
            <div
              key={idx}
              className="relative overflow-hidden rounded-2xl border border-warm-200/80 bg-white p-5 shadow-xs transition hover:-translate-y-0.5 hover:shadow-md dark:border-white/10 dark:bg-[#0f172a]"
            >
              <div className={`absolute inset-x-0 top-0 h-1 bg-gradient-to-r ${kpi.gradient}`} />
              <div className="flex items-center justify-between">
                <span className={`flex h-11 w-11 items-center justify-center rounded-2xl ${kpi.chip}`}>
                  <Icon className="h-5 w-5" />
                </span>
                <span className="flex items-center gap-1 text-[11px] font-semibold text-emerald-600 dark:text-emerald-400">
                  <TrendingUp className="h-3.5 w-3.5" />
                  +12% vs mes ant.
                </span>
              </div>
              <p className="mt-4 text-2xl font-black text-warm-900 dark:text-white tracking-tight">
                {kpi.value}
              </p>
              <p className="text-xs font-semibold text-warm-700 dark:text-warm-200 mt-0.5">
                {kpi.label}
              </p>
              <p className="mt-1 text-[11px] text-warm-500 dark:text-warm-400">
                {kpi.subtitle}
              </p>
            </div>
          );
        })}
      </div>

      {/* AI Family Coach Smart Insight Card */}
      <div className="relative overflow-hidden rounded-3xl border border-emerald-200/80 bg-gradient-to-r from-emerald-50 via-teal-50 to-white p-6 shadow-sm dark:border-emerald-500/20 dark:from-emerald-950/40 dark:via-warm-900 dark:to-warm-900">
        <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
          <div className="flex items-start gap-3.5">
            <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-emerald-600 text-white shadow-md shadow-emerald-600/30">
              <Sparkles className="h-5 w-5" />
            </span>
            <div>
              <div className="flex items-center gap-2">
                <h3 className="text-sm font-bold text-warm-900 dark:text-white">
                  {t('overview.aiInsightTitle')}
                </h3>
                <span className="rounded-full bg-emerald-600/10 px-2 py-0.5 text-[10px] font-bold text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300">
                  {lang === 'es' ? 'Alta Prioridad Bilingüe' : 'High Priority'}
                </span>
              </div>
              <p className="mt-1 text-xs leading-relaxed text-warm-700 dark:text-warm-300 max-w-3xl">
                {t('overview.aiInsightText')}
              </p>
            </div>
          </div>

          <button
            type="button"
            onClick={() => setIsAiCoachOpen(true)}
            className="inline-flex shrink-0 items-center gap-2 rounded-xl bg-white px-4 py-2 text-xs font-bold text-emerald-800 shadow-xs border border-emerald-200 hover:bg-emerald-50 dark:bg-warm-800 dark:border-white/10 dark:text-emerald-300 dark:hover:bg-warm-700"
          >
            <span>{lang === 'es' ? 'Ver Recomendaciones' : 'Review Suggestions'}</span>
            <ChevronRight className="h-3.5 w-3.5" />
          </button>
        </div>
      </div>

      {/* Family Members Snapshot Section */}
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <div>
            <h2 className="text-lg font-bold text-warm-900 dark:text-white">
              {t('overview.membersSnapshot')}
            </h2>
            <p className="text-xs text-warm-500 dark:text-warm-400">
              {lang === 'es'
                ? 'Progreso semanal y enfoque actual de cada integrante del hogar'
                : 'Current week progress and top milestones for each household member'}
            </p>
          </div>

          {selectedMemberId && (
            <button
              type="button"
              onClick={() => setSelectedMemberId(null)}
              className="text-xs font-semibold text-emerald-600 hover:underline dark:text-emerald-400"
            >
              {lang === 'es' ? '← Ver todos los miembros' : '← View all members'}
            </button>
          )}
        </div>

        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
          {filteredMembers.map((member) => (
            <div
              key={member.id}
              className="group relative flex flex-col justify-between rounded-2xl border border-warm-200/80 bg-white p-5 shadow-xs transition hover:-translate-y-0.5 hover:shadow-md dark:border-white/10 dark:bg-[#0f172a]"
            >
              <div>
                <div className="flex items-start justify-between">
                  <div className="flex items-center gap-3">
                    <div className={`flex h-12 w-12 items-center justify-center rounded-2xl bg-gradient-to-br ${member.avatarBg} text-base font-extrabold text-white shadow-md`}>
                      {member.initial}
                    </div>
                    <div>
                      <h4 className="text-sm font-bold text-warm-900 dark:text-white">
                        {member.name}
                      </h4>
                      <p className="text-xs font-medium text-emerald-700 dark:text-emerald-400">
                        {lang === 'es' ? member.roleTitleEs : member.roleTitleEn}
                      </p>
                    </div>
                  </div>

                  <span className="flex items-center gap-1 rounded-full bg-amber-50 px-2 py-0.5 text-[11px] font-bold text-amber-700 dark:bg-amber-500/10 dark:text-amber-300">
                    <Flame className="h-3 w-3 fill-amber-500 text-amber-500" />
                    {member.learningStreakDays}d racha
                  </span>
                </div>

                <div className="mt-4 space-y-2 border-t border-warm-100 pt-3 dark:border-white/10 text-xs">
                  <div>
                    <span className="font-semibold text-warm-500 dark:text-warm-400">
                      {lang === 'es' ? 'Meta Clave:' : 'Key Milestone:'}
                    </span>
                    <p className="font-medium text-warm-800 dark:text-warm-200">
                      {lang === 'es' ? member.targetMilestoneEs : member.targetMilestoneEn}
                    </p>
                  </div>

                  <div className="flex flex-wrap gap-1 pt-1">
                    {(lang === 'es' ? member.topFocusEs : member.topFocusEn).map((tag, i) => (
                      <span
                        key={i}
                        className="rounded-md bg-warm-100 px-2 py-0.5 text-[10px] font-medium text-warm-700 dark:bg-white/5 dark:text-warm-300"
                      >
                        {tag}
                      </span>
                    ))}
                  </div>
                </div>
              </div>

              <div className="mt-5 flex items-center justify-between border-t border-warm-100 pt-3 text-xs text-warm-500 dark:border-white/10 dark:text-warm-400">
                <span className="font-semibold text-warm-700 dark:text-warm-200">
                  {member.weeklyHoursLogged} hrs {lang === 'es' ? 'esta semana' : 'this week'}
                </span>
                <button
                  type="button"
                  onClick={() => setSelectedMemberId(member.id)}
                  className="inline-flex items-center gap-1 text-xs font-bold text-emerald-600 hover:text-emerald-700 dark:text-emerald-400"
                >
                  <span>{lang === 'es' ? 'Ver detalles' : 'Details'}</span>
                  <ArrowRight className="h-3 w-3" />
                </button>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Two-Column section: Upcoming Milestones & Strategic Goals */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* Left: Upcoming Milestones */}
        <div className="rounded-2xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
          <div className="flex items-center justify-between border-b border-warm-100 pb-4 dark:border-white/10">
            <div className="flex items-center gap-2">
              <Calendar className="h-5 w-5 text-emerald-600" />
              <h3 className="text-base font-bold text-warm-900 dark:text-white">
                {t('overview.upcomingMilestones')}
              </h3>
            </div>
            <span className="text-xs font-bold text-warm-400">
              {filteredMilestones.length} {lang === 'es' ? 'eventos' : 'events'}
            </span>
          </div>

          <div className="mt-4 space-y-3">
            {filteredMilestones.map((m) => {
              const member = members.find((x) => x.id === m.memberId);
              return (
                <div
                  key={m.id}
                  className="flex items-start gap-3.5 rounded-xl border border-warm-100 bg-warm-50/50 p-3.5 transition hover:bg-white hover:shadow-xs dark:border-white/5 dark:bg-white/[0.02] dark:hover:bg-white/[0.05]"
                >
                  <div className={`mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br ${member?.avatarBg ?? 'from-warm-600 to-warm-800'} text-xs font-bold text-white shadow-xs`}>
                    {member?.initial ?? 'F'}
                  </div>

                  <div className="flex-1">
                    <div className="flex items-center justify-between">
                      <h4 className="text-xs font-bold text-warm-900 dark:text-white">
                        {lang === 'es' ? m.titleEs : m.titleEn}
                      </h4>
                      <span className="rounded-md bg-warm-200 px-2 py-0.5 text-[10px] font-bold text-warm-700 dark:bg-white/10 dark:text-warm-300">
                        {lang === 'es' ? m.tagEs : m.tagEn}
                      </span>
                    </div>

                    <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px] text-warm-500 dark:text-warm-400">
                      <span>📅 {m.dateStr}</span>
                      <span>⏰ {m.timeStr}</span>
                      <span>📍 {m.location}</span>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* Right: Family Strategic Goals */}
        <div className="rounded-2xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
          <div className="flex items-center justify-between border-b border-warm-100 pb-4 dark:border-white/10">
            <div className="flex items-center gap-2">
              <Target className="h-5 w-5 text-indigo-600" />
              <h3 className="text-base font-bold text-warm-900 dark:text-white">
                {lang === 'es' ? 'Metas Estratégicas del Hogar' : 'Household Strategic Goals'}
              </h3>
            </div>
            <span className="text-xs font-bold text-indigo-600 dark:text-indigo-400">
              78% Global
            </span>
          </div>

          <div className="mt-4 space-y-4">
            {goals.map((g) => (
              <div
                key={g.id}
                className="rounded-xl border border-warm-100 bg-warm-50/40 p-3.5 dark:border-white/5 dark:bg-white/[0.02]"
              >
                <div className="flex items-center justify-between">
                  <h4 className="text-xs font-bold text-warm-900 dark:text-white">
                    {lang === 'es' ? g.titleEs : g.titleEn}
                  </h4>
                  <span className="text-xs font-extrabold text-emerald-600 dark:text-emerald-400">
                    {g.progressPercent}%
                  </span>
                </div>

                <div className="mt-2 h-2 w-full overflow-hidden rounded-full bg-warm-200 dark:bg-white/10">
                  <div
                    className="h-full rounded-full bg-gradient-to-r from-emerald-500 to-teal-500 transition-all duration-500"
                    style={{ width: `${g.progressPercent}%` }}
                  />
                </div>

                <p className="mt-2 text-[11px] leading-relaxed text-warm-600 dark:text-warm-300">
                  {lang === 'es' ? g.descriptionEs : g.descriptionEn}
                </p>

                <div className="mt-2.5 flex items-center justify-between text-[10px] text-warm-400">
                  <span>📅 Meta: {g.dueDate}</span>
                  <div className="flex -space-x-1">
                    {g.assignedMemberIds.map((memId) => {
                      const m = members.find((x) => x.id === memId);
                      return (
                        <span
                          key={memId}
                          title={m?.name}
                          className={`flex h-4 w-4 items-center justify-center rounded-full bg-gradient-to-br ${m?.avatarBg ?? 'from-warm-600 to-warm-800'} text-[8px] font-bold text-white ring-1 ring-white dark:ring-warm-900`}
                        >
                          {m?.initial}
                        </span>
                      );
                    })}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
