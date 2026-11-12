import { Link } from 'react-router';
import { useState } from 'react';
import {
  Users,
  Globe2,
  Plus,
  Loader2,
  UserCheck,
  Trash2,
} from 'lucide-react';
import { useLanguage } from '../context/LanguageContext';
import { useFamily } from '../context/FamilyContext';
import { familyApi } from '../api/familyApi';

export function FamilyPage() {
  const { t, lang } = useLanguage();
  const { members, family, householdName, householdCities, isLoading, selectedMemberId, refreshFamily } = useFamily();
  const isSpanish = lang === 'es';
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  async function handleDeleteMember(memberId: string, memberName: string) {
    const confirmed = window.confirm(
      isSpanish
        ? `¿Seguro que quieres eliminar a "${memberName}" de la familia? Esta acción no se puede deshacer desde la app.`
        : `Are you sure you want to remove "${memberName}" from the family? This cannot be undone from the app.`
    );
    if (!confirmed) return;

    setDeleteError(null);
    setDeletingId(memberId);
    try {
      await familyApi.deleteMember(memberId);
      await refreshFamily();
    } catch (err) {
      setDeleteError(err instanceof Error ? err.message : String(err));
    } finally {
      setDeletingId(null);
    }
  }

  const visibleMembers = selectedMemberId
    ? members.filter((m) => m.id === selectedMemberId)
    : members;

  return (
    <div className="space-y-8 animate-fade-in">
      {/* Page Header */}
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-3xl font-extrabold tracking-tight text-warm-900 dark:text-white">
              {t('family.title')}
            </h1>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-indigo-100 px-3 py-1 text-xs font-bold text-indigo-800 dark:bg-indigo-950/60 dark:text-indigo-300">
              <Users className="h-3 w-3" />
              {members.length} {isSpanish ? (members.length === 1 ? 'Integrante' : 'Integrantes') : (members.length === 1 ? 'Member' : 'Members')}
            </span>
          </div>
          <p className="mt-1.5 text-sm text-warm-600 dark:text-warm-300">
            {t('family.subtitle')}
          </p>
        </div>

        <Link
          to="/family/new"
          className="inline-flex items-center gap-2 rounded-2xl bg-gradient-to-r from-indigo-600 to-violet-600 px-5 py-3 text-xs font-bold text-white shadow-md shadow-indigo-600/30 transition hover:from-indigo-700 hover:to-violet-700 hover:shadow-lg"
        >
          <Plus className="h-4 w-4" />
          <span>{isSpanish ? '+ Nuevo Integrante Familiar' : '+ New Family Member'}</span>
        </Link>
      </div>

      {/* Household Identity Banner (Datos Reales desde SQL) */}
      <div className="rounded-3xl border border-warm-200/80 bg-gradient-to-br from-indigo-900 via-indigo-950 to-warm-950 p-6 text-white shadow-lg">
        <div className="flex flex-col md:flex-row items-start md:items-center justify-between gap-6">
          <div className="space-y-2">
            <span className="inline-flex items-center gap-2 rounded-full bg-white/10 px-3 py-1 text-xs font-semibold backdrop-blur-sm text-indigo-200">
              <Globe2 className="h-3.5 w-3.5 text-indigo-300" />
              {family?.householdType
                ? `${isSpanish ? 'Hogar' : 'Household'}: ${family.householdType}`
                : (isSpanish ? 'Hogar Familiar' : 'Family Household')}
            </span>
            <h2 className="text-2xl font-black">{householdName}</h2>
            {family?.familyMissionStatement ? (
              <p className="text-xs text-indigo-200 max-w-2xl leading-relaxed">
                {family.familyMissionStatement}
              </p>
            ) : null}
          </div>

          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            <div className="rounded-2xl bg-white/10 p-3 backdrop-blur-xs text-center">
              <p className="text-lg font-bold text-white">{members.length}</p>
              <p className="text-[10px] text-indigo-200 font-medium">
                {isSpanish ? (members.length === 1 ? 'Integrante registrado' : 'Integrantes registrados') : (members.length === 1 ? 'Registered member' : 'Registered members')}
              </p>
            </div>
            <div className="rounded-2xl bg-white/10 p-3 backdrop-blur-xs text-center">
              <p className="text-lg font-bold text-white">
                {family?.isBilingualHousehold ? '100%' : 'N/A'}
              </p>
              <p className="text-[10px] text-indigo-200 font-medium">
                {isSpanish ? 'Hogar Bilingüe' : 'Dual Language'}
              </p>
            </div>
            <div className="col-span-2 sm:col-span-1 rounded-2xl bg-white/10 p-3 backdrop-blur-xs text-center">
              <p className="text-lg font-bold text-white truncate max-w-[140px] mx-auto">
                {family?.primaryCity ? `${family.primaryCity}, ${family.primaryState || ''}` : (householdCities || '—')}
              </p>
              <p className="text-[10px] text-indigo-200 font-medium">
                {isSpanish ? 'Sede' : 'Location'}
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* Members Cards */}
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-bold text-warm-900 dark:text-white">
            {t('family.membersList')}
          </h2>
          {selectedMemberId && (
            <span className="text-xs font-medium text-indigo-600 dark:text-indigo-400">
              {isSpanish ? 'Filtro activo por integrante' : 'Filtered by member'}
            </span>
          )}
        </div>

        {deleteError && (
          <p className="rounded-xl bg-rose-50 px-3 py-2 text-xs font-semibold text-rose-700 dark:bg-rose-950/40 dark:text-rose-300">
            {deleteError}
          </p>
        )}

        {isLoading ? (
          <div className="flex items-center justify-center py-16 text-warm-500">
            <Loader2 className="h-6 w-6 animate-spin mr-2 text-indigo-600" />
            <span className="text-sm">{isSpanish ? 'Cargando integrantes reales...' : 'Loading members...'}</span>
          </div>
        ) : visibleMembers.length === 0 ? (
          <div className="rounded-3xl border border-warm-200/80 bg-white p-12 text-center shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
            <UserCheck className="mx-auto h-12 w-12 text-warm-400 dark:text-warm-500 mb-3" />
            <p className="text-base font-bold text-warm-900 dark:text-white">
              {isSpanish ? 'No hay integrantes registrados en esta familia.' : 'No members registered for this family yet.'}
            </p>
            <p className="mt-1 text-xs text-warm-500 dark:text-warm-400">
              {isSpanish ? 'Haz clic en "+ Nuevo Integrante Familiar" para añadir personas al hogar.' : 'Click "+ New Family Member" to add members.'}
            </p>
          </div>
        ) : (
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
            {visibleMembers.map((member) => (
              <div
                key={member.id}
                className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]"
              >
                <div className="flex items-start justify-between gap-4">
                  <div className="flex items-center gap-4">
                    <div className={`flex h-16 w-16 items-center justify-center rounded-2xl bg-gradient-to-br ${member.avatarBg} text-xl font-black text-white shadow-md`}>
                      {member.initial}
                    </div>
                    <div>
                      <Link
                        to={`/family/members/${member.id}`}
                        className="text-base font-bold text-warm-900 hover:text-indigo-600 dark:text-white dark:hover:text-indigo-400 transition"
                      >
                        {member.name}
                      </Link>
                      <p className="text-xs font-semibold text-indigo-600 dark:text-indigo-400">
                        {isSpanish ? member.roleTitleEs : member.roleTitleEn}
                      </p>
                      <p className="text-[11px] text-warm-500 dark:text-warm-400">
                        {member.primaryLocation ? `📍 ${member.primaryLocation}` : ''}
                        {member.primaryLocation && member.age ? ' · ' : ''}
                        {member.age ? `${member.age} ${isSpanish ? 'años' : 'years old'}` : ''}
                      </p>
                    </div>
                  </div>

                  {member.weeklyHoursLogged > 0 ? (
                    <span className="rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-bold text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300">
                      {member.weeklyHoursLogged} hrs/sem
                    </span>
                  ) : null}
                </div>

                {/* MSAL account status badge (age-based rule: 12+ own login, <12 managed by parent) */}
                <div className="mt-3">
                  {member.requiresOwnAccount ? (
                    <span className="inline-flex items-center gap-1 rounded-full bg-indigo-50 px-2.5 py-1 text-[11px] font-semibold text-indigo-700 dark:bg-indigo-950/60 dark:text-indigo-300">
                      🔐 {member.hasLinkedAccount
                        ? (isSpanish ? 'Cuenta propia vinculada' : 'Own account linked')
                        : (isSpanish ? 'Cuenta propia pendiente de invitar' : 'Own account pending invite')}
                    </span>
                  ) : (
                    <span className="inline-flex items-center gap-1 rounded-full bg-warm-100 px-2.5 py-1 text-[11px] font-semibold text-warm-600 dark:bg-white/5 dark:text-warm-300">
                      👨‍👩‍👧 {isSpanish ? 'Gestionado por el administrador(a)' : 'Managed by admin'}
                    </span>
                  )}
                </div>

                {/* Role / Skills / Languages Details */}
                <div className="mt-5 space-y-3 border-t border-warm-100 pt-4 dark:border-white/10 text-xs">
                  {(member.educationLevelEs || member.institutionEs) ? (
                    <div>
                      <span className="font-semibold text-warm-400 uppercase tracking-wider text-[10px]">
                        {isSpanish ? 'Institución & Nivel:' : 'Institution & Level:'}
                      </span>
                      {member.educationLevelEs ? (
                        <p className="font-medium text-warm-800 dark:text-warm-200 mt-0.5">
                          {isSpanish ? member.educationLevelEs : member.educationLevelEn}
                        </p>
                      ) : null}
                      {member.institutionEs ? (
                        <p className="text-warm-500 text-[11px]">
                          {isSpanish ? member.institutionEs : member.institutionEn}
                        </p>
                      ) : null}
                    </div>
                  ) : null}

                  {member.languages && member.languages.length > 0 ? (
                    <div>
                      <span className="font-semibold text-warm-400 uppercase tracking-wider text-[10px]">
                        {isSpanish ? 'Idiomas de Dominio:' : 'Languages Spoken:'}
                      </span>
                      <div className="mt-1 flex flex-wrap gap-1.5">
                        {member.languages.map((lng, i) => (
                          <span
                            key={i}
                            className="rounded-md bg-indigo-50 px-2 py-0.5 text-[11px] font-medium text-indigo-700 dark:bg-indigo-950/50 dark:text-indigo-300"
                          >
                            🗣️ {lng}
                          </span>
                        ))}
                      </div>
                    </div>
                  ) : null}

                  {((isSpanish ? member.topFocusEs : member.topFocusEn)?.length ?? 0) > 0 ? (
                    <div>
                      <span className="font-semibold text-warm-400 uppercase tracking-wider text-[10px]">
                        {isSpanish ? 'Enfoque & Fortalezas:' : 'Key Focus & Strengths:'}
                      </span>
                      <div className="mt-1 flex flex-wrap gap-1.5">
                        {(isSpanish ? member.topFocusEs : member.topFocusEn).map((foc, i) => (
                          <span
                            key={i}
                            className="rounded-md bg-warm-100 px-2 py-0.5 text-[11px] font-medium text-warm-800 dark:bg-white/5 dark:text-warm-200"
                          >
                            ✨ {foc}
                          </span>
                        ))}
                      </div>
                    </div>
                  ) : null}

                  {(member.targetMilestoneEs || member.targetMilestoneEn) ? (
                    <div className="rounded-xl bg-warm-50 p-3 dark:bg-white/[0.02]">
                      <span className="text-[10px] font-bold text-warm-500 uppercase tracking-wider">
                        {isSpanish ? 'Hito 2026-2027:' : '2026-2027 Milestone:'}
                      </span>
                      <p className="font-bold text-emerald-800 dark:text-emerald-400 mt-0.5">
                        🎯 {isSpanish ? member.targetMilestoneEs : member.targetMilestoneEn}
                      </p>
                    </div>
                  ) : null}
                </div>

                {/* Direct Action Link to MemberMainPage */}
                <div className="mt-5 pt-4 border-t border-warm-100 dark:border-white/10 flex items-center justify-between gap-2">
                  <button
                    type="button"
                    onClick={() => handleDeleteMember(member.id, member.name)}
                    disabled={deletingId === member.id}
                    className="inline-flex items-center gap-1.5 rounded-xl bg-rose-50 px-3 py-2 text-xs font-bold text-rose-700 hover:bg-rose-100 dark:bg-rose-950/40 dark:text-rose-300 dark:hover:bg-rose-900/40 transition disabled:opacity-60"
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                    <span>
                      {deletingId === member.id
                        ? (isSpanish ? 'Eliminando…' : 'Deleting…')
                        : (isSpanish ? 'Eliminar' : 'Delete')}
                    </span>
                  </button>
                  <Link
                    to={`/family/members/${member.id}`}
                    className="inline-flex items-center gap-1.5 rounded-xl bg-indigo-50 px-3.5 py-2 text-xs font-bold text-indigo-700 hover:bg-indigo-100 dark:bg-indigo-950/60 dark:text-indigo-300 dark:hover:bg-indigo-900/60 transition shadow-xs"
                  >
                    <span>{isSpanish ? 'Gestionar Integrante & Situación Educativa →' : 'Manage Member & Education →'}</span>
                  </Link>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
