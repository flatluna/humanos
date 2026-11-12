import { useEffect, useMemo, useState } from 'react';
import {
  Layers,
  Search,
  Sparkles,
  Clock,
  ArrowRight,
  Loader2,
  Wand2,
  ExternalLink,
  Pencil,
  Trash2,
  RefreshCw,
  BookOpen,
} from 'lucide-react';
import { useLanguage } from '../context/LanguageContext';
import { useFamily } from '../context/FamilyContext';
import { useAuth } from '../auth/AuthContext';
import { canUseAiAgentsAndStudio } from '../lib/accountRules';
import {
  capabilitiesApi,
  HumanCapability,
  HumanDevelopmentStage,
  Country,
  Subject,
  CapabilityRecommendation,
} from '../api/capabilitiesApi';

// Path (within capabilitystudio) where a recommendation is handed off to
// generate its full Engrams via NewAcademicLearningPage. Studio itself has no
// standalone MSAL login anymore — it's only reachable via the Studio Launch
// SSO handoff (see buildFamilyStudioLaunchUrl / docs/STUDIO_LAUNCH_INTEGRATION.md
// in capabilitystudio), so this is just the `returnTo` path, not a full URL.
function buildStudioReturnPath(rec: CapabilityRecommendation, subjects: Subject[]): string {
  const params = new URLSearchParams();
  params.set('code', rec.code);
  params.set('nameEs', rec.nameEs);
  params.set('nameEn', rec.nameEn);
  params.set('descriptionEs', rec.descriptionEs);
  params.set('descriptionEn', rec.descriptionEn);
  if (rec.whyItMattersEs) params.set('whyEs', rec.whyItMattersEs);
  if (rec.whyItMattersEn) params.set('whyEn', rec.whyItMattersEn);
  if ((rec.microSkillsCoveredEs ?? []).length > 0) params.set('skillsEs', rec.microSkillsCoveredEs.join('|'));
  if ((rec.microSkillsCoveredEn ?? []).length > 0) params.set('skillsEn', rec.microSkillsCoveredEn.join('|'));
  if (rec.ageRange) params.set('ageRange', rec.ageRange);
  if (rec.courseRequirementsEs) params.set('requirementsEs', rec.courseRequirementsEs);
  if (rec.courseRequirementsEn) params.set('requirementsEn', rec.courseRequirementsEn);
  if ((rec.courseOutlineEs ?? []).length > 0) params.set('outlineEs', rec.courseOutlineEs.join('|'));
  if ((rec.courseOutlineEn ?? []).length > 0) params.set('outlineEn', rec.courseOutlineEn.join('|'));
  const subject = subjects.find((s) => s.code === rec.subjectCode);
  if (subject) {
    params.set('subjectKey', subject.code);
    params.set('subjectEs', subject.nameEs);
    params.set('subjectEn', subject.nameEn);
  }
  // Pass the generation context through so Studio builds the course in the SAME single
  // language this recommendation was created in, instead of guessing from its own UI toggle.
  if (rec.language) params.set('language', rec.language);
  params.set('countryCode', rec.countryCode);
  params.set('educationSystemTypeCode', rec.educationSystemTypeCode);
  return `/new-academic-learning?${params.toString()}`;
}

/**
 * Builds the Family backend's Studio Launch URL (SSO handoff — see
 * docs/STUDIO_LAUNCH_INTEGRATION.md in capabilitystudio). Opening this URL
 * (not Studio directly) lets the already-signed-in family member's identity
 * (Azure OID/email) flow into a one-time code, which the backend 302s to
 * Studio's /launch page to exchange for a signed JWT — Studio never shows
 * its own login, and the course is actually created under that identity
 * instead of just "marking as sent" without real SSO.
 */
function buildFamilyStudioLaunchUrl(oid: string, email: string, returnTo: string): string {
  const params = new URLSearchParams();
  params.set('oid', oid);
  params.set('email', email);
  params.set('returnTo', returnTo);
  return `/api/family/studio-launch?${params.toString()}`;
}

// Presentational-only styling keyed by CategoryGroup (comes from the global
// family.HumanDevelopmentStages catalog), not fabricated business data.
const CATEGORY_STYLES: Record<string, { icon: string; badgeBg: string }> = {
  Elementary: { icon: '📚', badgeBg: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-950/60 dark:text-emerald-300' },
  Middle: { icon: '⚡', badgeBg: 'bg-indigo-100 text-indigo-900 dark:bg-indigo-950/60 dark:text-indigo-300' },
  HighSchool: { icon: '🎓', badgeBg: 'bg-violet-100 text-violet-900 dark:bg-violet-950/60 dark:text-violet-300' },
  College: { icon: '🏛️', badgeBg: 'bg-rose-100 text-rose-900 dark:bg-rose-950/60 dark:text-rose-300' },
  Professional: { icon: '💼', badgeBg: 'bg-warm-200 text-warm-900 dark:bg-white/10 dark:text-warm-200' },
};
const DEFAULT_CATEGORY_STYLE = { icon: '🧩', badgeBg: 'bg-warm-200 text-warm-900 dark:bg-white/10 dark:text-warm-200' };

const DOMAIN_STYLES: Record<string, { chip: string; text: string; bg: string }> = {
  'STEM & Math': {
    chip: 'bg-amber-100 text-amber-900 dark:bg-amber-500/20 dark:text-amber-300',
    text: 'text-amber-700 dark:text-amber-300',
    bg: 'from-amber-500 to-orange-500',
  },
  'Language & Literacy': {
    chip: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-500/20 dark:text-emerald-300',
    text: 'text-emerald-700 dark:text-emerald-300',
    bg: 'from-emerald-500 to-teal-600',
  },
  'Socio-Emotional': {
    chip: 'bg-rose-100 text-rose-900 dark:bg-rose-500/20 dark:text-rose-300',
    text: 'text-rose-700 dark:text-rose-300',
    bg: 'from-rose-500 to-pink-600',
  },
  'Critical Thinking': {
    chip: 'bg-indigo-100 text-indigo-900 dark:bg-indigo-500/20 dark:text-indigo-300',
    text: 'text-indigo-700 dark:text-indigo-300',
    bg: 'from-indigo-600 to-violet-600',
  },
  'Practical Life': {
    chip: 'bg-teal-100 text-teal-900 dark:bg-teal-500/20 dark:text-teal-300',
    text: 'text-teal-700 dark:text-teal-300',
    bg: 'from-teal-600 to-cyan-600',
  },
};

const MASTERY_BADGES: Record<string, { labelEs: string; labelEn: string; style: string }> = {
  Foundation: {
    labelEs: 'Nivel Inicial / Foundation',
    labelEn: 'Foundation Level',
    style: 'bg-slate-100 text-slate-700 dark:bg-white/10 dark:text-slate-300',
  },
  Proficient: {
    labelEs: 'Competente / Proficient',
    labelEn: 'Proficient',
    style: 'bg-blue-100 text-blue-800 dark:bg-blue-500/20 dark:text-blue-300',
  },
  Advanced: {
    labelEs: 'Avanzado / Advanced',
    labelEn: 'Advanced',
    style: 'bg-indigo-100 text-indigo-800 dark:bg-indigo-500/20 dark:text-indigo-300',
  },
  Master: {
    labelEs: 'Maestría / Master 🌟',
    labelEn: 'Mastery 🌟',
    style: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-500/20 dark:text-emerald-300 font-bold',
  },
};

// Subjects whose content genuinely differs by country (each country has its own history,
// civic system, geography, and official/home language) — only these show the Country picker.
// Everything else (e.g. Math, Science) is country-agnostic, so the picker would just add noise.
const COUNTRY_SPECIFIC_SUBJECT_KEYWORDS = ['history', 'historia', 'civic', 'cívic', 'civica', 'geography', 'geografía', 'geografia', 'language arts', 'artes del lenguaje', 'lengua'];

function isCountrySpecificSubject(subject: Subject | undefined): boolean {
  if (!subject) return false;
  const haystack = `${subject.nameEn} ${subject.nameEs}`.toLowerCase();
  return COUNTRY_SPECIFIC_SUBJECT_KEYWORDS.some((kw) => haystack.includes(kw));
}

export function CapabilitiesPage() {
  const { lang } = useLanguage();
  const { members, setIsAiCoachOpen, currentUserRole } = useFamily();
  const { account } = useAuth();
  const canUseAiAgent = canUseAiAgentsAndStudio(currentUserRole);

  const [stages, setStages] = useState<HumanDevelopmentStage[]>([]);
  const [capabilities, setCapabilities] = useState<HumanCapability[]>([]);
  const [isLoadingStages, setIsLoadingStages] = useState(true);
  const [isLoadingCapabilities, setIsLoadingCapabilities] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [selectedStageKey, setSelectedStageKey] = useState<string>('all_stages');
  const [selectedDomain, setSelectedDomain] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [selectedCapabilityModal, setSelectedCapabilityModal] = useState<HumanCapability | null>(null);

  // AI Subject Capability Recommender: lookups + form state + results
  const [countries, setCountries] = useState<Country[]>([]);
  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [recCountryCode, setRecCountryCode] = useState<string>('');
  const [recEduTypeCode, setRecEduTypeCode] = useState<string>('');
  const [recSubjectCode, setRecSubjectCode] = useState<string>('');
  // Generation language is no longer a user-facing choice — the course is always generated in
  // the family app's current UI language, avoiding an extra control for a redundant decision.
  // Language the AI should generate THIS course in — a single language only, to avoid making
  // the LLM produce a redundant translation nobody will use. Defaults to the active UI language.
  const [generationLanguage, setGenerationLanguage] = useState<'es' | 'en'>(lang === 'es' ? 'es' : 'en');
  const selectedSubject = subjects.find((s) => s.code === recSubjectCode);
  const [recommendations, setRecommendations] = useState<CapabilityRecommendation[]>([]);
  const [selectedRecIds, setSelectedRecIds] = useState<Set<string>>(new Set());
  const [isDeletingBulk, setIsDeletingBulk] = useState(false);
  const [isGeneratingRecommendations, setIsGeneratingRecommendations] = useState(false);
  const [recommendationError, setRecommendationError] = useState<string | null>(null);
  const [deletingRecommendationId, setDeletingRecommendationId] = useState<string | null>(null);
  const [checkingStatusRecommendationId, setCheckingStatusRecommendationId] = useState<string | null>(null);
  const [editingRecommendationId, setEditingRecommendationId] = useState<string | null>(null);
  const [isSavingEdit, setIsSavingEdit] = useState(false);
  const [editForm, setEditForm] = useState<{
    language: 'es' | 'en';
    name: string;
    description: string;
    whyItMatters: string;
    microSkillsCovered: string;
    courseRequirements: string;
    courseOutline: string;
  } | null>(null);

  // Load lookups (Countries, EducationSystemTypes, Subjects) once on mount.
  useEffect(() => {
    Promise.all([
      capabilitiesApi.getCountries(),
      capabilitiesApi.getEducationSystemTypes(),
      capabilitiesApi.getSubjects(),
    ])
      .then(([c, e, s]) => {
        setCountries(c);
        setSubjects(s);
        if (c.length > 0) setRecCountryCode((prev) => prev || c[0].code);
        if (e.length > 0) setRecEduTypeCode((prev) => prev || e[0].code);
        if (s.length > 0) setRecSubjectCode((prev) => prev || s[0].code);
      })
      .catch(() => {
        // Lookups are non-critical for the main catalog view; fail silently here.
      });
  }, []);

  // Load saved recommendations whenever the current grade + subject selection changes.
  useEffect(() => {
    setSelectedRecIds(new Set());
    if (selectedStageKey === 'all_stages' || !recSubjectCode) {
      setRecommendations([]);
      return;
    }
    let cancelled = false;
    capabilitiesApi
      .getRecommendations({
        countryCode: recCountryCode || undefined,
        educationSystemTypeCode: recEduTypeCode || undefined,
        stageKey: selectedStageKey,
        subjectCode: recSubjectCode,
      })
      .then((data) => {
        if (!cancelled) {
          setRecommendations(data);
          // Auto-check Studio for any recommendation already sent but not yet
          // confirmed Generated — flips the button to "View course" without
          // requiring a manual refresh once Studio finishes creating it.
          data
            .filter((r) => r.generationStatus === 'SentToStudio')
            .forEach((r) => {
              capabilitiesApi
                .syncRecommendationStudioStatus(r.recommendationId)
                .then((updated) => {
                  if (cancelled) return;
                  setRecommendations((prev) =>
                    prev.map((item) => (item.recommendationId === updated.recommendationId ? updated : item))
                  );
                })
                .catch(() => {
                  // Best-effort only — the manual "Send to Studio" button stays available.
                });
            });
        }
      })
      .catch(() => {
        if (!cancelled) setRecommendations([]);
      });
    return () => {
      cancelled = true;
    };
  }, [selectedStageKey, recCountryCode, recEduTypeCode, recSubjectCode]);

  async function handleGenerateRecommendations() {
    if (selectedStageKey === 'all_stages') {
      setRecommendationError(
        lang === 'es'
          ? 'Selecciona un grado específico arriba (no "Ver todos los niveles") para generar recomendaciones.'
          : 'Select a specific grade above (not "View all stages") to generate recommendations.'
      );
      return;
    }
    if (!recCountryCode || !recEduTypeCode || !recSubjectCode) {
      setRecommendationError(
        lang === 'es' ? 'Selecciona una Materia (y el País si aplica).' : 'Select a Subject (and Country if it applies).'
      );
      return;
    }

    // Avoid duplicates: once this exact combination (grade + subject +
    // country + education system) already has recommendations, the button
    // is disabled in the UI (see below) so this function should not even be
    // reachable in that case. This guard is kept as a safety net.
    if (recommendations.length > 0) {
      return;
    }

    setRecommendationError(null);
    setIsGeneratingRecommendations(true);
    try {
      const results = await capabilitiesApi.generateRecommendations({
        countryCode: recCountryCode,
        educationSystemTypeCode: recEduTypeCode,
        stageKey: selectedStageKey,
        subjectCode: recSubjectCode,
        language: generationLanguage,
      });
      setRecommendations(results);
      setSelectedRecIds(new Set());
    } catch (err) {
      setRecommendationError(err instanceof Error ? err.message : String(err));
    } finally {
      setIsGeneratingRecommendations(false);
    }
  }

  async function handleSendToStudio(rec: CapabilityRecommendation) {
    // SSO handoff: open the Family backend's Studio Launch endpoint (not
    // Studio directly) so the already-signed-in member's identity carries
    // over and Studio opens pre-authenticated, ready to actually create the
    // course/Engrams under that identity — a plain window.open to Studio's
    // URL would land on a login wall and nothing would really get created.
    if (!account?.idTokenClaims) {
      setRecommendationError(
        lang === 'es'
          ? 'No se pudo identificar tu sesión de Microsoft. Vuelve a iniciar sesión e intenta de nuevo.'
          : 'Could not identify your Microsoft session. Please sign in again and retry.'
      );
      return;
    }
    const oid = (account.idTokenClaims as Record<string, unknown>).oid as string | undefined;
    const email = account.username;
    if (!oid) {
      setRecommendationError(
        lang === 'es'
          ? 'Tu cuenta de Microsoft no tiene un identificador (oid) válido para abrir Studio.'
          : 'Your Microsoft account has no valid identifier (oid) to open Studio.'
      );
      return;
    }

    const returnTo = buildStudioReturnPath(rec, subjects);
    const launchUrl = buildFamilyStudioLaunchUrl(oid, email, returnTo);
    window.open(launchUrl, '_blank', 'noopener,noreferrer');
    try {
      await capabilitiesApi.markRecommendationSentToStudio(rec.recommendationId);
      setRecommendations((prev) =>
        prev.map((r) => (r.recommendationId === rec.recommendationId ? { ...r, generationStatus: 'SentToStudio' } : r))
      );
    } catch {
      // Non-critical: the admin already has the Studio tab open; status sync can retry later.
    }
  }

  /**
   * "Ver Curso" — opens the already-created Studio course via the same SSO
   * Studio Launch handoff as "Send to Studio" (not a raw link to Studio's
   * domain, which the SSO gate blocks without a valid launch session).
   */
  async function handleViewCourse(rec: CapabilityRecommendation) {
    if (!rec.studioDocumentId) return;
    if (!account?.idTokenClaims) {
      setRecommendationError(
        lang === 'es'
          ? 'No se pudo identificar tu sesión de Microsoft. Vuelve a iniciar sesión e intenta de nuevo.'
          : 'Could not identify your Microsoft session. Please sign in again and retry.'
      );
      return;
    }
    const oid = (account.idTokenClaims as Record<string, unknown>).oid as string | undefined;
    const email = account.username;
    if (!oid) {
      setRecommendationError(
        lang === 'es'
          ? 'Tu cuenta de Microsoft no tiene un identificador (oid) válido para abrir Studio.'
          : 'Your Microsoft account has no valid identifier (oid) to open Studio.'
      );
      return;
    }

    const returnTo = `/structured-learning/${rec.studioDocumentId}/course?mode=edit`;
    const launchUrl = buildFamilyStudioLaunchUrl(oid, email, returnTo);
    window.open(launchUrl, '_blank', 'noopener,noreferrer');
  }

  /** Manual "Check status" for a recommendation stuck on "Sent to Studio" — lets the
   * admin re-check without waiting for the next full page load's auto-sync. */
  async function handleCheckStudioStatus(rec: CapabilityRecommendation) {
    setCheckingStatusRecommendationId(rec.recommendationId);
    try {
      const updated = await capabilitiesApi.syncRecommendationStudioStatus(rec.recommendationId);
      setRecommendations((prev) =>
        prev.map((r) => (r.recommendationId === updated.recommendationId ? updated : r))
      );
    } catch (err) {
      setRecommendationError(err instanceof Error ? err.message : String(err));
    } finally {
      setCheckingStatusRecommendationId(null);
    }
  }

  async function handleDeleteRecommendation(rec: CapabilityRecommendation) {
    const name = lang === 'es' ? rec.nameEs : rec.nameEn;
    const confirmed = window.confirm(
      lang === 'es'
        ? `¿Eliminar la recomendación "${name}"? Esta acción no se puede deshacer.`
        : `Delete the recommendation "${name}"? This cannot be undone.`
    );
    if (!confirmed) return;

    setDeletingRecommendationId(rec.recommendationId);
    setRecommendationError(null);
    try {
      await capabilitiesApi.deleteRecommendation(rec.recommendationId);
      setRecommendations((prev) => prev.filter((r) => r.recommendationId !== rec.recommendationId));
      setSelectedRecIds((prev) => {
        if (!prev.has(rec.recommendationId)) return prev;
        const next = new Set(prev);
        next.delete(rec.recommendationId);
        return next;
      });
    } catch (err) {
      setRecommendationError(err instanceof Error ? err.message : String(err));
    } finally {
      setDeletingRecommendationId(null);
    }
  }

  function toggleSelectRecommendation(id: string) {
    setSelectedRecIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  function toggleSelectAllRecommendations() {
    if (selectedRecIds.size === recommendations.length) {
      setSelectedRecIds(new Set());
    } else {
      setSelectedRecIds(new Set(recommendations.map((r) => r.recommendationId)));
    }
  }

  async function handleDeleteSelectedRecommendations() {
    if (selectedRecIds.size === 0) return;
    const count = selectedRecIds.size;
    const confirmed = window.confirm(
      lang === 'es'
        ? `¿Eliminar las ${count} recomendaciones seleccionadas? Esta acción no se puede deshacer.`
        : `Delete the ${count} selected recommendations? This cannot be undone.`
    );
    if (!confirmed) return;

    setIsDeletingBulk(true);
    setRecommendationError(null);
    try {
      const idsToDelete = Array.from(selectedRecIds);
      const results = await Promise.allSettled(
        idsToDelete.map((id) => capabilitiesApi.deleteRecommendation(id))
      );
      const successfullyDeletedIds = new Set<string>();
      results.forEach((res, index) => {
        if (res.status === 'fulfilled') {
          successfullyDeletedIds.add(idsToDelete[index]);
        }
      });

      setRecommendations((prev) => prev.filter((r) => !successfullyDeletedIds.has(r.recommendationId)));
      setSelectedRecIds((prev) => {
        const next = new Set(prev);
        successfullyDeletedIds.forEach((id) => next.delete(id));
        return next;
      });

      if (successfullyDeletedIds.size < idsToDelete.length) {
        const failedCount = idsToDelete.length - successfullyDeletedIds.size;
        setRecommendationError(
          lang === 'es'
            ? `No se pudieron eliminar ${failedCount} recomendaciones.`
            : `Failed to delete ${failedCount} recommendations.`
        );
      }
    } catch (err) {
      setRecommendationError(err instanceof Error ? err.message : String(err));
    } finally {
      setIsDeletingBulk(false);
    }
  }

  function startEditingRecommendation(rec: CapabilityRecommendation) {
    setEditingRecommendationId(rec.recommendationId);
    // Single-language content: this recommendation only truly exists in rec.language (fallback
    // to current UI language for legacy rows created before single-language mode). Prefill from
    // whichever Es/En field actually holds the real text.
    const editLanguage: 'es' | 'en' = rec.language ?? (lang === 'es' ? 'es' : 'en');
    setEditForm({
      language: editLanguage,
      name: editLanguage === 'es' ? rec.nameEs : rec.nameEn,
      description: editLanguage === 'es' ? rec.descriptionEs : rec.descriptionEn,
      whyItMatters: (editLanguage === 'es' ? rec.whyItMattersEs : rec.whyItMattersEn) ?? '',
      microSkillsCovered: (editLanguage === 'es' ? rec.microSkillsCoveredEs : rec.microSkillsCoveredEn).join(', '),
      courseRequirements: (editLanguage === 'es' ? rec.courseRequirementsEs : rec.courseRequirementsEn) ?? '',
      courseOutline: (editLanguage === 'es' ? rec.courseOutlineEs : rec.courseOutlineEn).join('\n'),
    });
  }

  function cancelEditingRecommendation() {
    setEditingRecommendationId(null);
    setEditForm(null);
  }

  async function saveEditingRecommendation(recommendationId: string) {
    if (!editForm) return;
    setIsSavingEdit(true);
    setRecommendationError(null);
    try {
      // Single-language mode: write the one real value into BOTH Es and En fields (no
      // translation performed), matching how the AI agent saves single-language recommendations.
      const microSkills = editForm.microSkillsCovered
        .split(',')
        .map((s) => s.trim())
        .filter(Boolean);
      const courseOutline = editForm.courseOutline
        .split('\n')
        .map((s) => s.trim())
        .filter(Boolean);
      const updated = await capabilitiesApi.updateRecommendation(recommendationId, {
        nameEs: editForm.name,
        nameEn: editForm.name,
        descriptionEs: editForm.description,
        descriptionEn: editForm.description,
        whyItMattersEs: editForm.whyItMatters || null,
        whyItMattersEn: editForm.whyItMatters || null,
        microSkillsCoveredEs: microSkills,
        microSkillsCoveredEn: microSkills,
        courseRequirementsEs: editForm.courseRequirements || null,
        courseRequirementsEn: editForm.courseRequirements || null,
        courseOutlineEs: courseOutline,
        courseOutlineEn: courseOutline,
      });
      setRecommendations((prev) => prev.map((r) => (r.recommendationId === recommendationId ? updated : r)));
      setEditingRecommendationId(null);
      setEditForm(null);
    } catch (err) {
      setRecommendationError(err instanceof Error ? err.message : String(err));
    } finally {
      setIsSavingEdit(false);
    }
  }

  // Load the global stages catalog once on mount.
  useEffect(() => {
    let cancelled = false;
    setIsLoadingStages(true);
    capabilitiesApi
      .getStages()
      .then((data) => {
        if (cancelled) return;
        setStages(data);
      })
      .catch((err) => {
        if (!cancelled) setLoadError(err instanceof Error ? err.message : String(err));
      })
      .finally(() => {
        if (!cancelled) setIsLoadingStages(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  // Load capabilities whenever the selected stage changes (or all stages combined).
  useEffect(() => {
    let cancelled = false;
    setIsLoadingCapabilities(true);

    const stageKeysToLoad =
      selectedStageKey === 'all_stages' ? stages.map((s) => s.stageKey) : [selectedStageKey];

    if (stageKeysToLoad.length === 0) {
      setCapabilities([]);
      setIsLoadingCapabilities(false);
      return;
    }

    Promise.all(stageKeysToLoad.map((key) => capabilitiesApi.getStageCapabilities(key)))
      .then((results) => {
        if (cancelled) return;
        setCapabilities(results.flat());
      })
      .catch((err) => {
        if (!cancelled) setLoadError(err instanceof Error ? err.message : String(err));
      })
      .finally(() => {
        if (!cancelled) setIsLoadingCapabilities(false);
      });

    return () => {
      cancelled = true;
    };
  }, [selectedStageKey, stages]);

  const currentStage = useMemo(
    () => stages.find((s) => s.stageKey === selectedStageKey) ?? null,
    [stages, selectedStageKey]
  );

  // Filter capabilities by domain, search (stage filtering already happened via the API call)
  const filteredCapabilities = capabilities.filter((c) => {
    if (selectedDomain !== 'all' && c.domain !== selectedDomain) return false;
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      const matchName = c.nameEs.toLowerCase().includes(q) || c.nameEn.toLowerCase().includes(q);
      const matchCode = c.code.toLowerCase().includes(q);
      const matchDesc = c.descriptionEs.toLowerCase().includes(q) || c.descriptionEn.toLowerCase().includes(q);
      const matchSkills = c.skillsCovered.some((s) => s.toLowerCase().includes(q));
      if (!matchName && !matchCode && !matchDesc && !matchSkills) return false;
    }
    return true;
  });

  return (
    <div className="space-y-8 animate-fade-in pb-16">
      {/* Header */}
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-3xl font-extrabold tracking-tight text-warm-900 dark:text-white">
              {lang === 'es' ? 'Capabilities & Skills por Nivel Humano' : 'Capabilities & Skills by Life Stage'}
            </h1>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-violet-100 px-3 py-1 text-xs font-bold text-violet-800 dark:bg-violet-950/60 dark:text-violet-300">
              <Layers className="h-3 w-3" />
              HumanOS Architecture
            </span>
          </div>
          <p className="mt-1.5 text-sm text-warm-600 dark:text-warm-300 max-w-4xl">
            {lang === 'es'
              ? 'Catálogo estructurado de capabilities requeridas para maestría en cada etapa del desarrollo, desde Elementary School hasta la madurez profesional y ejecutiva.'
              : 'Structured catalog of core capabilities required for mastery across every human development milestone, from early elementary to executive adulthood.'}
          </p>
        </div>

        <button
          type="button"
          onClick={() => setIsAiCoachOpen(true)}
          className="inline-flex items-center gap-2 rounded-2xl bg-gradient-to-r from-violet-600 to-indigo-600 px-4 py-2.5 text-xs font-bold text-white shadow-md shadow-violet-600/20 transition hover:from-violet-700 hover:to-indigo-700"
        >
          <Sparkles className="h-4 w-4" />
          <span>{lang === 'es' ? 'Mapear Skills con AI Coach' : 'Map Skills with AI Coach'}</span>
        </button>
      </div>

      {/* STAGE SELECTOR (Timeline of Human Development) */}
      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <p className="text-xs font-bold uppercase tracking-wider text-warm-500 dark:text-warm-400">
            {lang === 'es' ? 'Línea de Tiempo del Desarrollo Humano & Grados Escolares:' : 'Human Development Timeline & Grade Levels:'}
          </p>
          <button
            type="button"
            onClick={() => setSelectedStageKey('all_stages')}
            className={`text-xs font-bold transition ${
              selectedStageKey === 'all_stages'
                ? 'text-violet-600 underline dark:text-violet-400'
                : 'text-warm-500 hover:text-warm-800 dark:text-warm-400'
            }`}
          >
            {lang === 'es' ? 'Ver todos los niveles juntos' : 'View all stages combined'}
          </button>
        </div>

        {isLoadingStages ? (
          <div className="flex items-center gap-2 text-xs font-semibold text-warm-500 dark:text-warm-400">
            <Loader2 className="h-4 w-4 animate-spin" />
            {lang === 'es' ? 'Cargando niveles...' : 'Loading stages...'}
          </div>
        ) : stages.length === 0 ? (
          <p className="text-xs font-medium text-warm-500 dark:text-warm-400">
            {lang === 'es'
              ? 'Aún no hay niveles de desarrollo humano cargados en el catálogo global.'
              : 'No human development stages have been loaded into the global catalog yet.'}
          </p>
        ) : (
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-7">
            {stages.map((stage) => {
              const isSelected = selectedStageKey === stage.stageKey;
              const style = CATEGORY_STYLES[stage.categoryGroup] ?? DEFAULT_CATEGORY_STYLE;
              return (
                <button
                  key={stage.stageId}
                  type="button"
                  onClick={() => setSelectedStageKey(stage.stageKey)}
                  className={`relative flex flex-col justify-between rounded-2xl border p-3.5 text-left transition-all ${
                    isSelected
                      ? 'border-violet-500 bg-violet-50/80 shadow-md ring-2 ring-violet-500/30 dark:border-violet-400 dark:bg-violet-950/40'
                      : 'border-warm-200/80 bg-white hover:border-warm-300 hover:bg-warm-50/70 dark:border-white/10 dark:bg-[#0f172a] dark:hover:bg-white/5'
                  }`}
                >
                  <div>
                    <span className="text-2xl">{style.icon}</span>
                    <h3 className={`mt-2 text-xs font-bold leading-snug ${isSelected ? 'text-violet-950 dark:text-violet-200' : 'text-warm-900 dark:text-white'}`}>
                      {lang === 'es' ? stage.stageNameEs : stage.stageNameEn}
                    </h3>
                  </div>

                  <div className="mt-3 pt-2 border-t border-warm-100 dark:border-white/5">
                    <span className="block text-[10px] font-semibold text-warm-500 dark:text-warm-400">
                      {stage.ageRange}
                    </span>
                    <span className="block text-[9px] uppercase font-bold text-violet-600 dark:text-violet-400">
                      {stage.categoryGroup}
                    </span>
                  </div>
                </button>
              );
            })}
          </div>
        )}
      </div>

      {/* CURRENT STAGE HIGHLIGHT BANNER */}
      {selectedStageKey !== 'all_stages' && currentStage && (
        <div className="rounded-3xl border border-warm-200/80 bg-gradient-to-r from-violet-50 via-indigo-50 to-white p-5 shadow-xs dark:border-white/10 dark:from-violet-950/30 dark:via-warm-900 dark:to-warm-900">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-center gap-3.5">
              <span className="text-3xl">{(CATEGORY_STYLES[currentStage.categoryGroup] ?? DEFAULT_CATEGORY_STYLE).icon}</span>
              <div>
                <div className="flex items-center gap-2">
                  <h2 className="text-base font-bold text-warm-900 dark:text-white">
                    {lang === 'es' ? currentStage.stageNameEs : currentStage.stageNameEn}
                  </h2>
                  <span className={`rounded-full px-2.5 py-0.5 text-xs font-bold ${(CATEGORY_STYLES[currentStage.categoryGroup] ?? DEFAULT_CATEGORY_STYLE).badgeBg}`}>
                    {currentStage.ageRange}
                  </span>
                </div>
                <p className="mt-1 text-xs text-warm-600 dark:text-warm-300 max-w-3xl">
                  {lang === 'es' ? currentStage.descriptionEs : currentStage.descriptionEn}
                </p>
              </div>
            </div>

            <span className="text-xs font-black text-violet-700 dark:text-violet-300 bg-white dark:bg-warm-800 px-3 py-1.5 rounded-xl border border-violet-200 dark:border-white/10 shrink-0">
              {filteredCapabilities.length} {lang === 'es' ? 'Capabilities Requeridas' : 'Required Capabilities'}
            </span>
          </div>
        </div>
      )}

      {/* FILTER & SEARCH BAR */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        {/* Search */}
        <div className="relative flex-1 max-w-md">
          <Search className="absolute left-3.5 top-2.5 h-4 w-4 text-warm-400" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder={
              lang === 'es'
                ? 'Buscar capability por nombre, código (ej: ELEM-MATH) o skill...'
                : 'Search capability by name, code (e.g. ELEM-MATH), or skill...'
            }
            className="w-full rounded-2xl border border-warm-200 bg-white py-2 pl-9 pr-4 text-xs text-warm-900 focus:border-violet-500 focus:outline-none dark:border-white/10 dark:bg-warm-900 dark:text-white"
          />
        </div>

        {/* Domain Filters */}
        <div className="flex flex-wrap items-center gap-1.5">
          {['all', 'STEM & Math', 'Language & Literacy', 'Socio-Emotional', 'Critical Thinking', 'Practical Life'].map((dom) => (
            <button
              key={dom}
              type="button"
              onClick={() => setSelectedDomain(dom)}
              className={`rounded-xl px-3 py-1.5 text-xs font-semibold transition ${
                selectedDomain === dom
                  ? 'bg-violet-600 text-white shadow-xs font-bold'
                  : 'bg-white text-warm-700 hover:bg-warm-100 dark:bg-warm-800 dark:text-warm-300 dark:hover:bg-warm-700'
              }`}
            >
              {dom === 'all' ? (lang === 'es' ? 'Todos los Dominios' : 'All Domains') : dom}
            </button>
          ))}
        </div>
      </div>

      {/* CAPABILITIES GRID */}
      {isLoadingCapabilities ? (
        <div className="flex items-center justify-center gap-2 rounded-3xl border border-dashed border-warm-200 p-12 text-sm font-semibold text-warm-500 dark:border-white/10 dark:text-warm-400">
          <Loader2 className="h-5 w-5 animate-spin" />
          {lang === 'es' ? 'Cargando capabilities...' : 'Loading capabilities...'}
        </div>
      ) : loadError ? (
        <div className="rounded-3xl border border-rose-200 bg-rose-50 p-6 text-sm font-semibold text-rose-700 dark:border-rose-500/20 dark:bg-rose-950/20 dark:text-rose-300">
          {lang === 'es' ? 'No se pudieron cargar los datos: ' : 'Could not load data: '}
          {loadError}
        </div>
      ) : filteredCapabilities.length === 0 ? (
        <div className="rounded-3xl border border-dashed border-warm-200 p-12 text-center text-sm font-semibold text-warm-500 dark:border-white/10 dark:text-warm-400">
          {lang === 'es'
            ? 'Todavía no hay capabilities cargadas en el catálogo global para este nivel.'
            : 'No capabilities have been loaded into the global catalog for this stage yet.'}
        </div>
      ) : (
      <div className="grid grid-cols-1 gap-5 lg:grid-cols-2">
        {filteredCapabilities.map((cap) => {
          const domainStyle = DOMAIN_STYLES[cap.domain] ?? DOMAIN_STYLES['STEM & Math'];
          const mastery = MASTERY_BADGES[cap.targetMasteryLevel] ?? MASTERY_BADGES.Proficient;

          return (
            <div
              key={cap.capabilityId}
              className="group relative flex flex-col justify-between rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs transition hover:-translate-y-0.5 hover:shadow-md dark:border-white/10 dark:bg-[#0f172a]"
            >
              <div>
                {/* Header: Code & Domain & Mastery */}
                <div className="flex items-center justify-between gap-2 flex-wrap">
                  <div className="flex items-center gap-2">
                    <span className="font-mono text-xs font-bold text-warm-600 dark:text-warm-400 bg-warm-100 dark:bg-white/10 px-2 py-0.5 rounded-md">
                      {cap.code}
                    </span>
                    <span className={`rounded-md px-2 py-0.5 text-[11px] font-bold ${domainStyle.chip}`}>
                      {cap.domain}
                    </span>
                  </div>

                  <span className={`rounded-full px-2.5 py-0.5 text-[11px] ${mastery.style}`}>
                    {lang === 'es' ? mastery.labelEs : mastery.labelEn}
                  </span>
                </div>

                {/* Capability Name & Description */}
                <h3 className="mt-3 text-base font-bold text-warm-900 dark:text-white group-hover:text-violet-600 dark:group-hover:text-violet-400 transition">
                  {lang === 'es' ? cap.nameEs : cap.nameEn}
                </h3>

                <p className="mt-1.5 text-xs text-warm-600 dark:text-warm-300 leading-relaxed">
                  {lang === 'es' ? cap.descriptionEs : cap.descriptionEn}
                </p>

                {/* Why it Matters (Pedagogical insight like HumanOS) */}
                <div className="mt-3 rounded-2xl bg-warm-50 p-3.5 dark:bg-white/[0.02] border border-warm-100 dark:border-white/5">
                  <span className="text-[10px] font-bold uppercase tracking-wider text-warm-400">
                    💡 {lang === 'es' ? 'Por qué es clave para el futuro:' : 'Why it matters for future success:'}
                  </span>
                  <p className="text-xs font-medium text-warm-800 dark:text-warm-200 mt-0.5">
                    {lang === 'es' ? cap.whyItMattersEs : cap.whyItMattersEn}
                  </p>
                </div>

                {/* Micro-Skills Covered */}
                <div className="mt-3 pt-2">
                  <span className="text-[10px] font-bold uppercase tracking-wider text-warm-400">
                    {lang === 'es' ? 'Competencias Específicas / Micro-Skills:' : 'Specific Micro-Skills Covered:'}
                  </span>
                  <div className="mt-1.5 flex flex-wrap gap-1.5">
                    {cap.skillsCovered.map((s, idx) => (
                      <span
                        key={idx}
                        className="rounded-lg bg-warm-100/80 px-2 py-0.5 text-[10px] font-semibold text-warm-700 dark:bg-white/5 dark:text-warm-300"
                      >
                        ✓ {s}
                      </span>
                    ))}
                  </div>
                </div>
              </div>

              {/* Card Footer: Hours & Action */}
              <div className="mt-5 flex items-center justify-between border-t border-warm-100 pt-3 text-xs text-warm-500 dark:border-white/10 dark:text-warm-400">
                <span className="flex items-center gap-1 font-medium">
                  <Clock className="h-3.5 w-3.5 text-warm-400" />
                  ~{cap.recommendedHoursToMaster} hrs {lang === 'es' ? 'para maestría' : 'to mastery'}
                </span>

                <button
                  type="button"
                  onClick={() => setSelectedCapabilityModal(cap)}
                  className="inline-flex items-center gap-1 text-xs font-bold text-violet-600 hover:text-violet-700 dark:text-violet-400"
                >
                  <span>{lang === 'es' ? 'Ver Detalles & Asignar' : 'View Details & Assign'}</span>
                  <ArrowRight className="h-3.5 w-3.5" />
                </button>
              </div>
            </div>
          );
        })}
      </div>
      )}

      {/* AI SUBJECT CAPABILITY RECOMMENDER (Admin Agent) — restricted to AdminEngrams (EngramsIQ owner/staff) */}
      {canUseAiAgent ? (
      <div className="rounded-3xl border border-violet-200/80 bg-violet-50/40 p-5 dark:border-violet-500/20 dark:bg-violet-950/10 space-y-4">
        <div className="flex items-center gap-2">
          <Wand2 className="h-4 w-4 text-violet-600 dark:text-violet-400" />
          <h2 className="text-sm font-bold text-warm-900 dark:text-white">
            {lang === 'es'
              ? 'Agente IA: Recomendador de Capabilities por Materia'
              : 'AI Agent: Capability Recommender by Subject'}
          </h2>
        </div>
        <p className="text-xs text-warm-600 dark:text-warm-300 max-w-3xl">
          {lang === 'es'
            ? 'El grado se toma del nivel seleccionado arriba. Elige una Materia; el agente generará las capabilities más importantes para esa combinación. Para materias que varían por país (Historia, Cívica, Geografía, Lengua), también elige el País. El resultado queda "Pendiente de Generar Engrams" hasta que lo envíes a HumanOS Studio.'
            : 'Grade comes from the stage selected above. Choose a Subject; the agent will generate the most important capabilities for that combination. For subjects that vary by country (History, Civics, Geography, Language Arts), also choose the Country. Results stay "Pending Engram Generation" until you send them to HumanOS Studio.'}
        </p>

        <div className={`grid grid-cols-1 gap-3 ${isCountrySpecificSubject(selectedSubject) ? 'sm:grid-cols-4' : 'sm:grid-cols-3'}`}>
          <select
            value={recSubjectCode}
            onChange={(e) => setRecSubjectCode(e.target.value)}
            className="rounded-xl border border-warm-200 bg-white px-3 py-2 text-xs font-semibold text-warm-900 dark:border-white/10 dark:bg-warm-900 dark:text-white"
          >
            <optgroup label={lang === 'es' ? 'Materias Académicas' : 'Academic Subjects'}>
              {subjects
                .filter((s) => s.subjectCategory === 'Academic')
                .map((s) => (
                  <option key={s.subjectId} value={s.code}>
                    {lang === 'es' ? s.nameEs : s.nameEn}
                  </option>
                ))}
            </optgroup>
            <optgroup label={lang === 'es' ? 'Carácter y Habilidades de Vida' : 'Character & Life Skills'}>
              {subjects
                .filter((s) => s.subjectCategory === 'Character')
                .map((s) => (
                  <option key={s.subjectId} value={s.code}>
                    {lang === 'es' ? s.nameEs : s.nameEn}
                  </option>
                ))}
            </optgroup>
          </select>

          {isCountrySpecificSubject(selectedSubject) && (
            <select
              value={recCountryCode}
              onChange={(e) => setRecCountryCode(e.target.value)}
              title={lang === 'es' ? 'Esta materia varía por país (historia e idioma propios)' : 'This subject varies by country (its own history and language)'}
              className="rounded-xl border border-warm-200 bg-white px-3 py-2 text-xs font-semibold text-warm-900 dark:border-white/10 dark:bg-warm-900 dark:text-white"
            >
              {countries.map((c) => (
                <option key={c.countryId} value={c.code}>
                  {lang === 'es' ? c.nameEs : c.nameEn}
                </option>
              ))}
            </select>
          )}

          <select
            value={generationLanguage}
            onChange={(e) => setGenerationLanguage(e.target.value as 'es' | 'en')}
            title={
              lang === 'es'
                ? 'Idioma en el que se generará este curso (un solo idioma, no se traduce)'
                : 'Language this course will be generated in (single language, not translated)'
            }
            className="rounded-xl border border-warm-200 bg-white px-3 py-2 text-xs font-semibold text-warm-900 dark:border-white/10 dark:bg-warm-900 dark:text-white"
          >
            <option value="es">🇲🇽 {lang === 'es' ? 'Español' : 'Spanish'}</option>
            <option value="en">🇺🇸 {lang === 'es' ? 'Inglés' : 'English'}</option>
          </select>

          <button
            type="button"
            onClick={handleGenerateRecommendations}
            disabled={isGeneratingRecommendations || recommendations.length > 0}
            title={
              recommendations.length > 0
                ? (lang === 'es'
                    ? 'Ya existen recomendaciones para esta combinación. Elimina las existentes si quieres generar nuevas.'
                    : 'Recommendations already exist for this combination. Delete the existing ones if you want to generate new ones.')
                : undefined
            }
            className="inline-flex items-center justify-center gap-2 rounded-xl bg-violet-600 px-3 py-2 text-xs font-bold text-white transition hover:bg-violet-700 disabled:opacity-60 disabled:cursor-not-allowed"
          >
            {isGeneratingRecommendations ? <Loader2 className="h-4 w-4 animate-spin" /> : <Wand2 className="h-4 w-4" />}
            {isGeneratingRecommendations ? (lang === 'es' ? 'Generando…' : 'Generating…') : (lang === 'es' ? 'Generar con IA' : 'Generate with AI')}
          </button>
        </div>
        {recommendations.length > 0 && !isGeneratingRecommendations && (
          <p className="text-[11px] text-amber-700 dark:text-amber-400">
            {lang === 'es'
              ? `⚠️ Ya hay ${recommendations.length} recomendación(es) para esta combinación. El botón está deshabilitado para evitar duplicados; elimina las recomendaciones existentes si quieres generar nuevas.`
              : `⚠️ There are already ${recommendations.length} recommendation(s) for this combination. The button is disabled to avoid duplicates; delete the existing recommendations if you want to generate new ones.`}
          </p>
        )}

        {recommendationError && (
          <p className="text-xs font-semibold text-rose-600 dark:text-rose-400">{recommendationError}</p>
        )}

        {recommendations.length > 0 && (
          <div className="space-y-3 pt-1">
            {/* Multi-selection action toolbar */}
            <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-violet-200/80 bg-white/80 px-4 py-2.5 shadow-2xs dark:border-white/10 dark:bg-[#0f172a]/60">
              <label className="flex items-center gap-2.5 cursor-pointer select-none text-xs font-semibold text-warm-800 dark:text-warm-200">
                <input
                  type="checkbox"
                  checked={recommendations.length > 0 && selectedRecIds.size === recommendations.length}
                  ref={(el) => {
                    if (el) {
                      el.indeterminate = selectedRecIds.size > 0 && selectedRecIds.size < recommendations.length;
                    }
                  }}
                  onChange={toggleSelectAllRecommendations}
                  className="h-4 w-4 rounded border-warm-300 text-violet-600 focus:ring-violet-500 dark:border-white/20 dark:bg-warm-800 cursor-pointer"
                />
                <span>
                  {selectedRecIds.size === 0
                    ? lang === 'es'
                      ? 'Seleccionar todas'
                      : 'Select all'
                    : lang === 'es'
                      ? `${selectedRecIds.size} de ${recommendations.length} seleccionada(s)`
                      : `${selectedRecIds.size} of ${recommendations.length} selected`}
                </span>
              </label>

              {selectedRecIds.size > 0 && (
                <button
                  type="button"
                  onClick={handleDeleteSelectedRecommendations}
                  disabled={isDeletingBulk}
                  className="inline-flex items-center gap-1.5 rounded-xl bg-rose-600 px-3 py-1.5 text-xs font-bold text-white shadow-xs transition hover:bg-rose-700 disabled:opacity-60 cursor-pointer"
                >
                  {isDeletingBulk ? (
                    <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  ) : (
                    <Trash2 className="h-3.5 w-3.5" />
                  )}
                  {lang === 'es'
                    ? `Eliminar seleccionadas (${selectedRecIds.size})`
                    : `Delete selected (${selectedRecIds.size})`}
                </button>
              )}
            </div>

            {recommendations.map((rec) =>
              editingRecommendationId === rec.recommendationId && editForm ? (
                <div
                  key={rec.recommendationId}
                  className="space-y-2 rounded-2xl border border-violet-300 bg-violet-50/60 p-3.5 dark:border-violet-500/40 dark:bg-violet-950/20"
                >
                  <div className="grid grid-cols-1 gap-2">
                    <p className="text-[10px] font-bold uppercase tracking-wide text-violet-600 dark:text-violet-400">
                      {editForm.language === 'es' ? '🇲🇽 Español' : '🇺🇸 English'}
                    </p>
                    <input
                      type="text"
                      value={editForm.name}
                      onChange={(e) => setEditForm((f) => (f ? { ...f, name: e.target.value } : f))}
                      placeholder={lang === 'es' ? 'Nombre' : 'Name'}
                      className="rounded-lg border border-warm-300 bg-white px-2 py-1.5 text-xs dark:border-white/10 dark:bg-[#0f172a] dark:text-white"
                    />
                    <textarea
                      value={editForm.description}
                      onChange={(e) => setEditForm((f) => (f ? { ...f, description: e.target.value } : f))}
                      placeholder={lang === 'es' ? 'Descripción' : 'Description'}
                      rows={2}
                      className="rounded-lg border border-warm-300 bg-white px-2 py-1.5 text-xs dark:border-white/10 dark:bg-[#0f172a] dark:text-white"
                    />
                    <input
                      type="text"
                      value={editForm.whyItMatters}
                      onChange={(e) => setEditForm((f) => (f ? { ...f, whyItMatters: e.target.value } : f))}
                      placeholder={lang === 'es' ? 'Por qué importa' : 'Why it matters'}
                      className="rounded-lg border border-warm-300 bg-white px-2 py-1.5 text-xs dark:border-white/10 dark:bg-[#0f172a] dark:text-white"
                    />
                    <input
                      type="text"
                      value={editForm.microSkillsCovered}
                      onChange={(e) => setEditForm((f) => (f ? { ...f, microSkillsCovered: e.target.value } : f))}
                      placeholder={lang === 'es' ? 'Micro-skills (separadas por coma)' : 'Micro-skills (comma-separated)'}
                      className="rounded-lg border border-warm-300 bg-white px-2 py-1.5 text-xs dark:border-white/10 dark:bg-[#0f172a] dark:text-white"
                    />
                    <input
                      type="text"
                      value={editForm.courseRequirements}
                      onChange={(e) => setEditForm((f) => (f ? { ...f, courseRequirements: e.target.value } : f))}
                      placeholder={lang === 'es' ? 'Requisitos previos del curso' : 'Course requirements'}
                      className="rounded-lg border border-warm-300 bg-white px-2 py-1.5 text-xs dark:border-white/10 dark:bg-[#0f172a] dark:text-white"
                    />
                    <textarea
                      value={editForm.courseOutline}
                      onChange={(e) => setEditForm((f) => (f ? { ...f, courseOutline: e.target.value } : f))}
                      placeholder={lang === 'es' ? 'Índice del curso — un capítulo por línea' : 'Course outline — one chapter per line'}
                      rows={4}
                      className="rounded-lg border border-warm-300 bg-white px-2 py-1.5 text-xs dark:border-white/10 dark:bg-[#0f172a] dark:text-white"
                    />
                  </div>
                  <div className="flex justify-end gap-2">
                    <button
                      type="button"
                      onClick={cancelEditingRecommendation}
                      disabled={isSavingEdit}
                      className="rounded-xl border border-warm-300 px-3 py-1.5 text-[11px] font-bold text-warm-700 transition hover:bg-warm-100 disabled:opacity-60 dark:border-white/10 dark:text-warm-300 dark:hover:bg-white/5"
                    >
                      {lang === 'es' ? 'Cancelar' : 'Cancel'}
                    </button>
                    <button
                      type="button"
                      onClick={() => saveEditingRecommendation(rec.recommendationId)}
                      disabled={isSavingEdit}
                      className="inline-flex items-center gap-1.5 rounded-xl bg-violet-600 px-3 py-1.5 text-[11px] font-bold text-white transition hover:bg-violet-700 disabled:opacity-60"
                    >
                      {isSavingEdit ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : null}
                      {lang === 'es' ? 'Guardar' : 'Save'}
                    </button>
                  </div>
                </div>
              ) : (
                <div
                  key={rec.recommendationId}
                  className={`flex flex-col gap-2 rounded-2xl border p-3.5 transition sm:flex-row sm:items-start sm:justify-between ${
                    selectedRecIds.has(rec.recommendationId)
                      ? 'border-violet-400 bg-violet-50/50 shadow-xs dark:border-violet-500/40 dark:bg-violet-950/20'
                      : 'border-warm-200/80 bg-white dark:border-white/10 dark:bg-[#0f172a]'
                  }`}
                >
                  <div className="flex items-start gap-3 min-w-0 flex-1">
                    <input
                      type="checkbox"
                      checked={selectedRecIds.has(rec.recommendationId)}
                      onChange={() => toggleSelectRecommendation(rec.recommendationId)}
                      aria-label={lang === 'es' ? `Seleccionar ${rec.nameEs}` : `Select ${rec.nameEn}`}
                      className="mt-1 h-4 w-4 shrink-0 rounded border-warm-300 text-violet-600 focus:ring-violet-500 dark:border-white/20 dark:bg-warm-800 cursor-pointer"
                    />
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="font-mono text-[10px] font-bold text-warm-600 dark:text-warm-400 bg-warm-100 dark:bg-white/10 px-2 py-0.5 rounded-md">
                          {rec.code}
                        </span>
                        {rec.ageRange && (
                          <span className="rounded-full bg-sky-100 px-2 py-0.5 text-[10px] font-bold text-sky-800 dark:bg-sky-500/20 dark:text-sky-300">
                            🎂 {rec.ageRange}
                          </span>
                        )}
                        <span
                          className={`rounded-full px-2 py-0.5 text-[10px] font-bold ${
                            rec.generationStatus === 'PendingEngramGeneration'
                              ? 'bg-amber-100 text-amber-800 dark:bg-amber-500/20 dark:text-amber-300'
                              : rec.generationStatus === 'SentToStudio'
                                ? 'bg-blue-100 text-blue-800 dark:bg-blue-500/20 dark:text-blue-300'
                                : 'bg-emerald-100 text-emerald-900 dark:bg-emerald-500/20 dark:text-emerald-300'
                          }`}
                        >
                          {rec.generationStatus === 'PendingEngramGeneration'
                            ? lang === 'es'
                              ? 'Pendiente de Generar Engrams'
                              : 'Pending Engram Generation'
                            : rec.generationStatus === 'SentToStudio'
                              ? lang === 'es'
                                ? 'Enviado a Studio'
                                : 'Sent to Studio'
                              : lang === 'es'
                                ? 'Generado'
                                : 'Generated'}
                        </span>
                      </div>
                      <h4 className="mt-1 text-xs font-bold text-warm-900 dark:text-white">
                        {lang === 'es' ? rec.nameEs : rec.nameEn}
                      </h4>
                      <p className="text-[11px] text-warm-600 dark:text-warm-300 line-clamp-2">
                        {lang === 'es' ? rec.descriptionEs : rec.descriptionEn}
                      </p>
                      {(rec.courseRequirementsEs || rec.courseRequirementsEn) && (
                        <p className="mt-1 text-[11px] text-warm-500 dark:text-warm-400">
                          <span className="font-bold">{lang === 'es' ? 'Requisitos: ' : 'Requirements: '}</span>
                          {lang === 'es' ? rec.courseRequirementsEs : rec.courseRequirementsEn}
                        </p>
                      )}
                      {(lang === 'es' ? rec.courseOutlineEs : rec.courseOutlineEn).length > 0 && (
                        <div className="mt-1.5">
                          <p className="text-[10px] font-bold uppercase tracking-wide text-warm-500 dark:text-warm-400">
                            {lang === 'es' ? '📑 Índice del curso' : '📑 Course outline'}
                          </p>
                          <ol className="ml-4 list-decimal text-[11px] text-warm-700 dark:text-warm-300">
                            {(lang === 'es' ? rec.courseOutlineEs : rec.courseOutlineEn).map((chapter, i) => (
                              <li key={i}>{chapter}</li>
                            ))}
                          </ol>
                        </div>
                      )}
                      {(rec.sources ?? []).length > 0 && (
                        <div className="mt-1.5">
                          <p className="text-[10px] font-bold uppercase tracking-wide text-warm-500 dark:text-warm-400">
                            {lang === 'es' ? '🌐 Fuentes' : '🌐 Sources'}
                          </p>
                          <ul className="ml-4 list-disc text-[11px] text-warm-600 dark:text-warm-400">
                            {rec.sources.map((source, i) => {
                              const urlMatch = source.match(/https?:\/\/\S+/);
                              if (!urlMatch) return <li key={i}>{source}</li>;
                              const url = urlMatch[0].replace(/[).,]+$/, '');
                              const before = source.slice(0, urlMatch.index).trim();
                              return (
                                <li key={i}>
                                  {before && <>{before} </>}
                                  <a
                                    href={url}
                                    target="_blank"
                                    rel="noopener noreferrer"
                                    className="text-violet-600 underline hover:text-violet-700 dark:text-violet-400"
                                  >
                                    {url}
                                  </a>
                                </li>
                              );
                            })}
                          </ul>
                        </div>
                      )}
                    </div>
                  </div>

                  <div className="flex shrink-0 items-center gap-1.5 sm:self-start">
                    <button
                      type="button"
                      onClick={() => startEditingRecommendation(rec)}
                      title={lang === 'es' ? 'Editar' : 'Edit'}
                      className="inline-flex items-center gap-1 rounded-xl border border-warm-300 px-2.5 py-1.5 text-[11px] font-bold text-warm-700 transition hover:bg-warm-100 dark:border-white/10 dark:text-warm-300 dark:hover:bg-white/5"
                    >
                      <Pencil className="h-3.5 w-3.5" />
                    </button>
                    <button
                      type="button"
                      onClick={() => handleDeleteRecommendation(rec)}
                      disabled={deletingRecommendationId === rec.recommendationId}
                      title={lang === 'es' ? 'Eliminar' : 'Delete'}
                      className="inline-flex items-center gap-1 rounded-xl border border-rose-300 px-2.5 py-1.5 text-[11px] font-bold text-rose-700 transition hover:bg-rose-100 disabled:opacity-60 dark:border-rose-500/30 dark:text-rose-400 dark:hover:bg-rose-500/10"
                    >
                      {deletingRecommendationId === rec.recommendationId ? (
                        <Loader2 className="h-3.5 w-3.5 animate-spin" />
                      ) : (
                        <Trash2 className="h-3.5 w-3.5" />
                      )}
                    </button>
                    {rec.generationStatus === 'Generated' && rec.studioDocumentId ? (
                      <button
                        type="button"
                        onClick={() => handleViewCourse(rec)}
                        className="inline-flex items-center gap-1.5 rounded-xl bg-emerald-600 px-3 py-1.5 text-[11px] font-bold text-white transition hover:bg-emerald-700"
                      >
                        <BookOpen className="h-3.5 w-3.5" />
                        {lang === 'es' ? 'Ver Curso' : 'View Course'}
                      </button>
                    ) : rec.generationStatus === 'SentToStudio' ? (
                      <>
                        <button
                          type="button"
                          onClick={() => handleCheckStudioStatus(rec)}
                          disabled={checkingStatusRecommendationId === rec.recommendationId}
                          title={lang === 'es' ? 'Verificar si el curso ya se generó' : 'Check if the course was generated'}
                          className="inline-flex items-center gap-1 rounded-xl border border-warm-300 px-2.5 py-1.5 text-[11px] font-bold text-warm-700 transition hover:bg-warm-100 disabled:opacity-60 dark:border-white/10 dark:text-warm-300 dark:hover:bg-white/5"
                        >
                          {checkingStatusRecommendationId === rec.recommendationId ? (
                            <Loader2 className="h-3.5 w-3.5 animate-spin" />
                          ) : (
                            <RefreshCw className="h-3.5 w-3.5" />
                          )}
                        </button>
                        <button
                          type="button"
                          onClick={() => handleSendToStudio(rec)}
                          className="inline-flex items-center gap-1.5 rounded-xl bg-indigo-600 px-3 py-1.5 text-[11px] font-bold text-white transition hover:bg-indigo-700"
                        >
                          <ExternalLink className="h-3.5 w-3.5" />
                          {lang === 'es' ? 'Reabrir en Studio' : 'Reopen in Studio'}
                        </button>
                      </>
                    ) : (
                      <button
                        type="button"
                        onClick={() => handleSendToStudio(rec)}
                        className="inline-flex items-center gap-1.5 rounded-xl bg-indigo-600 px-3 py-1.5 text-[11px] font-bold text-white transition hover:bg-indigo-700"
                      >
                        <ExternalLink className="h-3.5 w-3.5" />
                        {lang === 'es' ? 'Crear Curso' : 'Create Course'}
                      </button>
                    )}
                  </div>
                </div>
              )
            )}
          </div>

        )}
      </div>
      ) : (
        <div className="rounded-3xl border border-warm-200/80 bg-warm-50/60 p-5 text-center dark:border-white/10 dark:bg-white/5">
          <Wand2 className="mx-auto h-5 w-5 text-warm-400 dark:text-warm-500" />
          <p className="mt-2 text-xs font-semibold text-warm-600 dark:text-warm-300">
            {lang === 'es'
              ? 'El Agente de IA (recomendador de capabilities) y el envío a HumanOS Studio están reservados al equipo de EngramsIQ. Puedes explorar y ver el catálogo de cursos libremente.'
              : 'The AI Agent (capability recommender) and sending to HumanOS Studio are reserved for the EngramsIQ team. You can freely browse and view the course catalog.'}
          </p>
        </div>
      )}

      {/* DETAIL MODAL / DRAWER */}
      {selectedCapabilityModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4 backdrop-blur-xs animate-fade-in">
          <div className="w-full max-w-xl rounded-3xl border border-warm-200 bg-white p-6 shadow-2xl dark:border-white/10 dark:bg-[#0f172a] space-y-5">
            <div className="flex items-start justify-between">
              <div>
                <span className="font-mono text-xs font-bold bg-violet-100 text-violet-800 dark:bg-violet-950/60 dark:text-violet-300 px-2 py-0.5 rounded-md">
                  {selectedCapabilityModal.code}
                </span>
                <h3 className="mt-2 text-lg font-bold text-warm-900 dark:text-white">
                  {lang === 'es' ? selectedCapabilityModal.nameEs : selectedCapabilityModal.nameEn}
                </h3>
                <p className="text-xs text-warm-500">
                  {selectedCapabilityModal.domain} · {selectedCapabilityModal.recommendedHoursToMaster} hrs recomendadas
                </p>
              </div>

              <button
                type="button"
                onClick={() => setSelectedCapabilityModal(null)}
                className="rounded-full p-1.5 text-warm-400 hover:bg-warm-100 dark:hover:bg-white/10"
              >
                ✕
              </button>
            </div>

            <p className="text-xs text-warm-700 dark:text-warm-300 leading-relaxed">
              {lang === 'es' ? selectedCapabilityModal.descriptionEs : selectedCapabilityModal.descriptionEn}
            </p>

            <div className="rounded-2xl bg-warm-50 p-4 dark:bg-white/[0.02]">
              <span className="text-[11px] font-bold text-warm-500 uppercase tracking-wider">
                {lang === 'es' ? 'Asignar a un Integrante de la Familia:' : 'Assign to Family Member:'}
              </span>
              <div className="mt-2 flex flex-wrap gap-2">
                {members.map((m) => (
                  <button
                    key={m.id}
                    type="button"
                    onClick={() => {
                      alert(
                        lang === 'es'
                          ? `Capability ${selectedCapabilityModal.code} asignada exitosamente a ${m.name}`
                          : `Capability ${selectedCapabilityModal.code} successfully assigned to ${m.name}`
                      );
                      setSelectedCapabilityModal(null);
                    }}
                    className="flex items-center gap-1.5 rounded-xl border border-warm-200 bg-white px-3 py-1.5 text-xs font-semibold text-warm-700 hover:border-violet-500 hover:bg-violet-50 dark:border-white/10 dark:bg-warm-900 dark:text-warm-300"
                  >
                    <span className={`flex h-4 w-4 items-center justify-center rounded-full bg-gradient-to-br ${m.avatarBg} text-[9px] font-bold text-white`}>
                      {m.initial}
                    </span>
                    <span>{m.name.split(' ')[0]}</span>
                  </button>
                ))}
              </div>
            </div>

            <div className="flex justify-end pt-2">
              <button
                type="button"
                onClick={() => setSelectedCapabilityModal(null)}
                className="rounded-2xl bg-warm-900 px-5 py-2 text-xs font-bold text-white hover:bg-warm-800 dark:bg-white dark:text-warm-900"
              >
                {lang === 'es' ? 'Cerrar' : 'Close'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
