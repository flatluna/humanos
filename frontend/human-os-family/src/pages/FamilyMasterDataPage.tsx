import { useEffect, useRef, useState } from 'react';
import {
  AlertTriangle,
  Building2,
  CalendarDays,
  CheckCircle2,
  Edit3,
  Globe2,
  Languages,
  Loader2,
  Lock,
  MapPin,
  Save,
  Search,
  ShieldCheck,
  UsersRound,
  X,
} from 'lucide-react';
import { useLanguage } from '../context/LanguageContext';
import { familyApi, type AddressSuggestion, type FamilyMasterData, type FamilyMasterRecord } from '../api/familyApi';

const CARD_ICON_STYLES = {
  indigo: 'bg-indigo-100 text-indigo-600 dark:bg-indigo-500/20 dark:text-indigo-300',
  emerald: 'bg-emerald-100 text-emerald-600 dark:bg-emerald-500/20 dark:text-emerald-300',
  violet: 'bg-violet-100 text-violet-600 dark:bg-violet-500/20 dark:text-violet-300',
  amber: 'bg-amber-100 text-amber-600 dark:bg-amber-500/20 dark:text-amber-300',
} as const;

export const HOUSEHOLD_TYPES = [
  { value: 'Bicultural', labelEs: 'Bicultural / Transfronterizo', labelEn: 'Bicultural / Cross-Border' },
  { value: 'Nuclear', labelEs: 'Nuclear (Padres e hijos)', labelEn: 'Nuclear (Parents & children)' },
  { value: 'Monoparental', labelEs: 'Monoparental (Madre o padre a cargo)', labelEn: 'Single-Parent' },
  { value: 'Extendida', labelEs: 'Extendida (Con abuelos o parientes)', labelEn: 'Extended Family' },
  { value: 'Reconstituida', labelEs: 'Reconstituida / Ensamblada', labelEn: 'Blended / Stepfamily' },
  { value: 'SinHijos', labelEs: 'Pareja sin hijos', labelEn: 'Couples / No Children' },
] as const;

export const LANGUAGE_OPTIONS = [
  { value: 'es', labelEs: 'Español (ES)', labelEn: 'Spanish (ES)' },
  { value: 'en', labelEs: 'Inglés (EN)', labelEn: 'English (EN)' },
  { value: 'pt', labelEs: 'Portugués (PT)', labelEn: 'Portuguese (PT)' },
  { value: 'fr', labelEs: 'Francés (FR)', labelEn: 'French (FR)' },
] as const;

export function FamilyMasterDataPage() {
  const { lang } = useLanguage();
  const isSpanish = lang === 'es';

  const [data, setData] = useState<FamilyMasterData | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Modo edición
  const [isEditing, setIsEditing] = useState(false);
  const [editForm, setEditForm] = useState<FamilyMasterRecord | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);

  // Bing Maps address autocomplete & filter state
  const [addressFilterQuery, setAddressFilterQuery] = useState('');
  const [addressSuggestions, setAddressSuggestions] = useState<AddressSuggestion[]>([]);
  const [isSearchingAddress, setIsSearchingAddress] = useState(false);
  const addressSearchTimerRef = useRef<number | null>(null);

  useEffect(() => {
    let cancelled = false;

    setIsLoading(true);
    setError(null);

    familyApi
      .getFamilyMasterData()
      .then((result) => {
        if (!cancelled) {
          setData(result);
        }
      })
      .catch((err: Error) => {
        if (!cancelled) {
          setError(err.message);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setIsLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  function handleStartEditing() {
    if (!data) return;
    setEditForm({ ...data.family });
    setAddressFilterQuery(
      data.family.primaryFullAddress ||
        [data.family.primaryStreet, data.family.primaryCity, data.family.primaryState, data.family.primaryCountry]
          .filter(Boolean)
          .join(', '),
    );
    setAddressSuggestions([]);
    setSaveError(null);
    setSaveSuccess(null);
    setIsEditing(true);
  }

  function handleCancelEditing() {
    setIsEditing(false);
    setEditForm(null);
    setAddressSuggestions([]);
    setSaveError(null);
  }

  function handleAddressFilterChange(query: string) {
    setAddressFilterQuery(query);

    if (addressSearchTimerRef.current) {
      window.clearTimeout(addressSearchTimerRef.current);
    }

    if (!query || query.trim().length < 3) {
      setAddressSuggestions([]);
      setIsSearchingAddress(false);
      return;
    }

    setIsSearchingAddress(true);
    addressSearchTimerRef.current = window.setTimeout(() => {
      void familyApi
        .searchAddresses(query)
        .then((suggs) => {
          setAddressSuggestions(suggs);
        })
        .finally(() => {
          setIsSearchingAddress(false);
        });
    }, 300);
  }

  function handleSelectSuggestion(sugg: AddressSuggestion) {
    const street = [sugg.streetNumber, sugg.streetName].filter(Boolean).join(' ') || sugg.streetName || null;
    const country =
      sugg.countryCode === 'US' ? 'USA' : sugg.countryCode === 'MX' ? 'MEX' : sugg.countryCode || 'USA';

    setEditForm((prev) => {
      if (!prev) return prev;
      return {
        ...prev,
        primaryFullAddress: sugg.address,
        primaryStreet: street,
        primaryCity: sugg.city ?? null,
        primaryState: sugg.region ?? null,
        primaryZipCode: sugg.postalCode ?? null,
        primaryCountry: country,
        latitude: sugg.latitude != null ? Number(sugg.latitude) : null,
        longitude: sugg.longitude != null ? Number(sugg.longitude) : null,
      };
    });

    setAddressFilterQuery(sugg.address);
    setAddressSuggestions([]);
  }

  async function handleSave() {
    if (!editForm) return;

    setIsSaving(true);
    setSaveError(null);
    setSaveSuccess(null);

    try {
      const updatedData = await familyApi.updateFamilyMasterData(editForm);
      setData(updatedData);
      setIsEditing(false);
      setSaveSuccess(
        isSpanish
          ? 'Los datos maestros y la ubicación se guardaron con éxito.'
          : 'Family master data and location saved successfully.',
      );
      setTimeout(() => setSaveSuccess(null), 5000);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : String(err);
      setSaveError(msg);
    } finally {
      setIsSaving(false);
    }
  }

  if (isLoading) {
    return (
      <div className="flex min-h-[40vh] items-center justify-center">
        <div className="flex items-center gap-3 text-warm-500 dark:text-warm-400">
          <Loader2 className="h-5 w-5 animate-spin" />
          <span className="text-sm font-medium">
            {isSpanish ? 'Cargando datos maestros de la familia…' : 'Loading family master data…'}
          </span>
        </div>
      </div>
    );
  }

  if (error || !data) {
    return (
      <div className="flex min-h-[40vh] flex-col items-center justify-center gap-3 rounded-3xl border border-red-200/80 bg-red-50 p-8 text-center dark:border-red-500/20 dark:bg-red-950/30">
        <AlertTriangle className="h-8 w-8 text-red-500" />
        <h2 className="text-base font-bold text-red-700 dark:text-red-300">
          {isSpanish ? 'No se pudo cargar la información' : 'Unable to load data'}
        </h2>
        <p className="max-w-md text-sm text-red-600 dark:text-red-300/80">
          {error ?? (isSpanish ? 'No hay un registro de familia disponible.' : 'No family record is available.')}
        </p>
      </div>
    );
  }

  const { family, members } = data;

  const primaryLocDisplay =
    family.primaryFullAddress ||
    [family.primaryCity, family.primaryState, family.primaryCountry].filter(Boolean).join(', ') ||
    '—';

  const dataCards = [
    {
      icon: Building2,
      label: isSpanish ? 'Nombre del hogar' : 'Household name',
      value: family.familyName,
      color: 'indigo',
    },
    {
      icon: MapPin,
      label: isSpanish ? 'Ubicación principal' : 'Primary location',
      value: primaryLocDisplay,
      color: 'emerald',
    },
    {
      icon: Languages,
      label: isSpanish ? 'Idioma principal' : 'Primary language',
      value: `${family.primaryLanguage.toUpperCase()}${family.isBilingualHousehold ? (isSpanish ? ' · Hogar bilingüe' : ' · Bilingual household') : ''}`,
      color: 'violet',
    },
    {
      icon: CalendarDays,
      label: isSpanish ? 'Registrado desde' : 'On record since',
      value: new Date(family.createdDate).toLocaleDateString(isSpanish ? 'es-MX' : 'en-US'),
      color: 'amber',
    },
  ] as const;

  return (
    <div className="space-y-8 animate-fade-in">
      {/* Top Banner and Actions */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="text-3xl font-extrabold tracking-tight text-warm-900 dark:text-white">
              {isSpanish ? 'Datos maestros de la familia' : 'Family Master Data'}
            </h1>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-indigo-100 px-3 py-1 text-xs font-bold text-indigo-800 dark:bg-indigo-950/60 dark:text-indigo-300">
              <ShieldCheck className="h-3 w-3" />
              {isSpanish ? 'Registro principal' : 'Primary record'}
            </span>
          </div>
          <p className="mt-1.5 max-w-3xl text-sm text-warm-600 dark:text-warm-300">
            {isSpanish
              ? 'Identidad, configuración y composición oficial del hogar, leída directamente de la base de datos.'
              : 'Official household identity, configuration, and composition, read directly from the database.'}
          </p>
        </div>

        {/* Modo edición toggle */}
        <div className="flex items-center gap-3">
          {!isEditing ? (
            <button
              type="button"
              onClick={handleStartEditing}
              className="inline-flex items-center gap-2 rounded-2xl bg-indigo-600 px-4 py-2.5 text-sm font-bold text-white shadow-sm hover:bg-indigo-700 transition"
            >
              <Edit3 className="h-4 w-4" />
              {isSpanish ? 'Modo edición' : 'Edit mode'}
            </button>
          ) : (
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={handleCancelEditing}
                disabled={isSaving}
                className="inline-flex items-center gap-1.5 rounded-2xl border border-warm-300 bg-white px-4 py-2 text-sm font-semibold text-warm-700 hover:bg-warm-50 dark:border-white/10 dark:bg-slate-800 dark:text-white"
              >
                <X className="h-4 w-4" />
                {isSpanish ? 'Cancelar' : 'Cancel'}
              </button>
              <button
                type="button"
                onClick={handleSave}
                disabled={isSaving}
                className="inline-flex items-center gap-1.5 rounded-2xl bg-emerald-600 px-4 py-2 text-sm font-bold text-white shadow-sm hover:bg-emerald-700 transition disabled:opacity-50"
              >
                {isSaving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
                {isSpanish ? 'Guardar cambios' : 'Save changes'}
              </button>
            </div>
          )}
        </div>
      </div>

      {saveSuccess && (
        <div className="flex items-center gap-3 rounded-2xl border border-emerald-200 bg-emerald-50 p-4 text-sm font-medium text-emerald-800 dark:border-emerald-500/20 dark:bg-emerald-950/40 dark:text-emerald-300">
          <CheckCircle2 className="h-5 w-5 shrink-0 text-emerald-600 dark:text-emerald-400" />
          <span>{saveSuccess}</span>
        </div>
      )}

      {saveError && (
        <div className="flex items-center gap-3 rounded-2xl border border-red-200 bg-red-50 p-4 text-sm font-medium text-red-800 dark:border-red-500/20 dark:bg-red-950/40 dark:text-red-300">
          <AlertTriangle className="h-5 w-5 shrink-0 text-red-600 dark:text-red-400" />
          <span>{saveError}</span>
        </div>
      )}

      {/* Hero Household Profile */}
      <section className="rounded-3xl border border-indigo-200/80 bg-gradient-to-br from-indigo-900 via-indigo-950 to-warm-950 p-6 text-white shadow-lg">
        <div className="flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="text-xs font-bold uppercase tracking-[0.18em] text-indigo-200">
              {isSpanish ? 'Perfil del hogar' : 'Household profile'}
            </p>
            <h2 className="mt-2 text-2xl font-black">{family.familyName}</h2>
            <p className="mt-1 flex items-center gap-1.5 text-sm text-indigo-200">
              <Globe2 className="h-4 w-4" />
              {HOUSEHOLD_TYPES.find((ht) => ht.value === family.householdType)
                ? isSpanish
                  ? HOUSEHOLD_TYPES.find((ht) => ht.value === family.householdType)?.labelEs
                  : HOUSEHOLD_TYPES.find((ht) => ht.value === family.householdType)?.labelEn
                : family.householdType}
            </p>
            {family.primaryFullAddress && (
              <p className="mt-2 flex items-center gap-1.5 text-xs text-indigo-300">
                <MapPin className="h-3.5 w-3.5 shrink-0 text-emerald-400" />
                {family.primaryFullAddress}
                {family.latitude != null && family.longitude != null && (
                  <span className="ml-1 rounded bg-white/10 px-1.5 py-0.5 font-mono text-[10px] text-emerald-300">
                    {family.latitude.toFixed(5)}, {family.longitude.toFixed(5)}
                  </span>
                )}
              </p>
            )}
          </div>
          <div className="rounded-2xl bg-white/10 px-5 py-4 text-center backdrop-blur-sm">
            <p className="text-2xl font-black">{members.length}</p>
            <p className="text-xs font-medium text-indigo-200">
              {isSpanish ? 'integrantes activos' : 'active members'}
            </p>
          </div>
        </div>
      </section>

      {/* FORMULARIO EN MODO EDICIÓN */}
      {isEditing && editForm && (
        <section className="space-y-6 rounded-3xl border border-indigo-200 bg-white p-6 shadow-md dark:border-indigo-500/30 dark:bg-[#0f172a]">
          <div className="border-b border-warm-100 pb-4 dark:border-white/10">
            <div className="flex items-center gap-2">
              <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-indigo-100 text-indigo-700 dark:bg-indigo-950 dark:text-indigo-300">
                <Edit3 className="h-4 w-4" />
              </span>
              <h2 className="text-lg font-bold text-warm-900 dark:text-white">
                {isSpanish ? 'Edición de datos maestros' : 'Edit Master Data'}
              </h2>
            </div>
            <p className="mt-1 text-xs text-warm-500 dark:text-warm-400">
              {isSpanish
                ? 'Modifica los valores del hogar. La ubicación principal se resuelve y valida con Bing Maps.'
                : 'Modify household values. The primary location is resolved and validated with Bing Maps.'}
            </p>
          </div>

          {/* Información Básica */}
          <div>
            <h3 className="mb-3 text-sm font-bold text-warm-800 dark:text-warm-200">
              {isSpanish ? 'Información básica del hogar' : 'Basic household info'}
            </h3>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label className="block text-xs font-bold uppercase tracking-wider text-warm-600 dark:text-warm-400">
                  {isSpanish ? 'Nombre del hogar' : 'Household name'}
                </label>
                <input
                  type="text"
                  value={editForm.familyName}
                  onChange={(e) => setEditForm({ ...editForm, familyName: e.target.value })}
                  className="mt-1 w-full rounded-xl border border-warm-300 bg-white px-3.5 py-2 text-sm font-medium text-warm-900 focus:border-indigo-500 focus:outline-none dark:border-white/20 dark:bg-slate-900 dark:text-white"
                />
              </div>
              <div>
                <label className="block text-xs font-bold uppercase tracking-wider text-warm-600 dark:text-warm-400">
                  {isSpanish ? 'Tipo de hogar' : 'Household type'}
                </label>
                <select
                  value={editForm.householdType}
                  onChange={(e) => setEditForm({ ...editForm, householdType: e.target.value })}
                  className="mt-1 w-full rounded-xl border border-warm-300 bg-white px-3.5 py-2 text-sm font-medium text-warm-900 focus:border-indigo-500 focus:outline-none dark:border-white/20 dark:bg-slate-900 dark:text-white"
                >
                  {HOUSEHOLD_TYPES.map((ht) => (
                    <option key={ht.value} value={ht.value}>
                      {isSpanish ? ht.labelEs : ht.labelEn}
                    </option>
                  ))}
                  {!HOUSEHOLD_TYPES.some((ht) => ht.value === editForm.householdType) && (
                    <option value={editForm.householdType}>{editForm.householdType}</option>
                  )}
                </select>
              </div>
              <div>
                <label className="block text-xs font-bold uppercase tracking-wider text-warm-600 dark:text-warm-400">
                  {isSpanish ? 'Idioma principal' : 'Primary language'}
                </label>
                <select
                  value={editForm.primaryLanguage}
                  onChange={(e) => setEditForm({ ...editForm, primaryLanguage: e.target.value })}
                  className="mt-1 w-full rounded-xl border border-warm-300 bg-white px-3.5 py-2 text-sm font-medium text-warm-900 focus:border-indigo-500 focus:outline-none dark:border-white/20 dark:bg-slate-900 dark:text-white"
                >
                  {LANGUAGE_OPTIONS.map((langOpt) => (
                    <option key={langOpt.value} value={langOpt.value}>
                      {isSpanish ? langOpt.labelEs : langOpt.labelEn}
                    </option>
                  ))}
                  {!LANGUAGE_OPTIONS.some((langOpt) => langOpt.value === editForm.primaryLanguage) && (
                    <option value={editForm.primaryLanguage}>{editForm.primaryLanguage.toUpperCase()}</option>
                  )}
                </select>
              </div>
              <div className="flex items-center pt-2 sm:pt-6">
                <label className="flex items-center gap-3 cursor-pointer select-none">
                  <input
                    type="checkbox"
                    checked={editForm.isBilingualHousehold}
                    onChange={(e) => setEditForm({ ...editForm, isBilingualHousehold: e.target.checked })}
                    className="h-4 w-4 rounded border-warm-300 text-indigo-600 focus:ring-indigo-500"
                  />
                  <div>
                    <span className="text-xs font-bold uppercase tracking-wider text-warm-700 dark:text-warm-300">
                      {isSpanish ? 'Hogar bilingüe' : 'Bilingual household'}
                    </span>
                    <p className="text-[11px] text-warm-500 dark:text-warm-400">
                      {isSpanish
                        ? 'Permite contenido y acompañamiento en dos idiomas.'
                        : 'Enables content and coaching in dual languages.'}
                    </p>
                  </div>
                </label>
              </div>
            </div>
          </div>

          {/* UBICACIÓN PRINCIPAL: BING MAPS AUTOCOMPLETE & LOCKED FIELDS */}
          <div className="rounded-2xl border border-indigo-100 bg-indigo-50/40 p-5 dark:border-indigo-500/20 dark:bg-slate-900/60">
            <div className="flex items-center gap-2 text-indigo-700 dark:text-indigo-400">
              <MapPin className="h-5 w-5" />
              <h3 className="text-sm font-extrabold uppercase tracking-wider">
                {isSpanish ? 'Ubicación principal (Bing Maps)' : 'Primary location (Bing Maps)'}
              </h3>
            </div>
            <p className="mt-1 text-xs text-warm-600 dark:text-warm-400">
              {isSpanish
                ? 'Usa el buscador para filtrar o adivinar la dirección mediante Bing Maps. Los datos resultantes (País, Estado, Ciudad, Calle y Coordenadas) quedan fijados y protegidos.'
                : 'Use the search box to filter or predict address using Bing Maps. The resulting values (Country, State, City, Street, Coordinates) are securely locked.'}
            </p>

            {/* Bing Maps Filter / Adivina dirección Input */}
            <div className="relative mt-4">
              <label className="block text-xs font-bold uppercase tracking-wider text-indigo-900 dark:text-indigo-300">
                {isSpanish ? 'Filtrar o adivinar dirección con Bing Maps' : 'Filter or predict address with Bing Maps'}
              </label>
              <div className="relative mt-1">
                <Search className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-indigo-500" />
                <input
                  type="text"
                  value={addressFilterQuery}
                  onChange={(e) => handleAddressFilterChange(e.target.value)}
                  placeholder={
                    isSpanish
                      ? 'Escribe dirección, calle o ciudad (ej. 100 Congress Ave Austin, o Reforma 222 CDMX)...'
                      : 'Type address, street or city to search via Bing Maps...'
                  }
                  className="w-full rounded-xl border border-indigo-300 bg-white pl-10 pr-10 py-2.5 text-sm font-medium text-warm-900 shadow-sm focus:border-indigo-600 focus:ring-1 focus:ring-indigo-600 focus:outline-none dark:border-indigo-500/40 dark:bg-slate-900 dark:text-white"
                />
                {isSearchingAddress && (
                  <Loader2 className="absolute right-3.5 top-1/2 h-4 w-4 -translate-y-1/2 animate-spin text-indigo-600 dark:text-indigo-400" />
                )}
              </div>

              {/* Suggestions Dropdown */}
              {addressSuggestions.length > 0 && (
                <div className="absolute z-20 mt-1 max-h-60 w-full overflow-auto rounded-xl border border-indigo-200 bg-white py-1 shadow-xl dark:border-indigo-500/30 dark:bg-slate-900">
                  <div className="px-3 py-1 text-[11px] font-bold uppercase tracking-wider text-indigo-600 dark:text-indigo-400">
                    {isSpanish ? 'Sugerencias de Bing Maps (clic para seleccionar)' : 'Bing Maps suggestions (click to select)'}
                  </div>
                  {addressSuggestions.map((sugg, idx) => (
                    <button
                      key={sugg.entityId || idx}
                      type="button"
                      onClick={() => handleSelectSuggestion(sugg)}
                      className="flex w-full flex-col items-start px-3.5 py-2 text-left hover:bg-indigo-50 transition dark:hover:bg-indigo-950/50"
                    >
                      <span className="text-sm font-semibold text-warm-900 dark:text-white">{sugg.address}</span>
                      <div className="mt-0.5 flex flex-wrap gap-1 text-[11px] text-warm-500 dark:text-warm-400">
                        {sugg.city && <span className="font-medium text-indigo-600 dark:text-indigo-400">{sugg.city}</span>}
                        {sugg.region && <span>· {sugg.region}</span>}
                        {sugg.countryCode && <span>· {sugg.countryCode}</span>}
                        {sugg.latitude != null && sugg.longitude != null && (
                          <span className="font-mono text-[10px] text-emerald-600 dark:text-emerald-400">
                            · ({sugg.latitude.toFixed(4)}, {sugg.longitude.toFixed(4)})
                          </span>
                        )}
                      </div>
                    </button>
                  ))}
                </div>
              )}
            </div>

            {/* LOCKED READONLY FIELDS (No editables directamente) */}
            <div className="mt-5 rounded-xl border border-warm-200/90 bg-white/80 p-4 dark:border-white/10 dark:bg-slate-900/80">
              <div className="mb-3 flex items-center justify-between">
                <div className="flex items-center gap-1.5 text-xs font-bold text-warm-700 dark:text-warm-300">
                  <Lock className="h-3.5 w-3.5 text-amber-600 dark:text-amber-400" />
                  <span>
                    {isSpanish
                      ? 'Campos autocompletados por Bing Maps (no editables directamente):'
                      : 'Fields populated by Bing Maps (read-only):'}
                  </span>
                </div>
                <span className="rounded bg-warm-100 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-warm-600 dark:bg-white/10 dark:text-warm-300">
                  {isSpanish ? 'Solo lectura' : 'Locked'}
                </span>
              </div>

              <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                {/* País */}
                <div>
                  <label className="block text-[11px] font-bold uppercase tracking-wider text-warm-500 dark:text-warm-400">
                    {isSpanish ? 'País' : 'Country'}
                  </label>
                  <input
                    type="text"
                    value={editForm.primaryCountry || ''}
                    readOnly
                    disabled
                    className="mt-1 w-full cursor-not-allowed rounded-lg border border-warm-200 bg-warm-100/70 px-3 py-1.5 text-xs font-semibold text-warm-800 dark:border-white/10 dark:bg-slate-800/80 dark:text-warm-200"
                  />
                </div>

                {/* Estado */}
                <div>
                  <label className="block text-[11px] font-bold uppercase tracking-wider text-warm-500 dark:text-warm-400">
                    {isSpanish ? 'Estado / Región' : 'State / Region'}
                  </label>
                  <input
                    type="text"
                    value={editForm.primaryState || ''}
                    readOnly
                    disabled
                    className="mt-1 w-full cursor-not-allowed rounded-lg border border-warm-200 bg-warm-100/70 px-3 py-1.5 text-xs font-semibold text-warm-800 dark:border-white/10 dark:bg-slate-800/80 dark:text-warm-200"
                  />
                </div>

                {/* Ciudad */}
                <div>
                  <label className="block text-[11px] font-bold uppercase tracking-wider text-warm-500 dark:text-warm-400">
                    {isSpanish ? 'Ciudad' : 'City'}
                  </label>
                  <input
                    type="text"
                    value={editForm.primaryCity || ''}
                    readOnly
                    disabled
                    className="mt-1 w-full cursor-not-allowed rounded-lg border border-warm-200 bg-warm-100/70 px-3 py-1.5 text-xs font-semibold text-warm-800 dark:border-white/10 dark:bg-slate-800/80 dark:text-warm-200"
                  />
                </div>

                {/* Calle / Número */}
                <div>
                  <label className="block text-[11px] font-bold uppercase tracking-wider text-warm-500 dark:text-warm-400">
                    {isSpanish ? 'Calle / Número' : 'Street'}
                  </label>
                  <input
                    type="text"
                    value={editForm.primaryStreet || ''}
                    readOnly
                    disabled
                    className="mt-1 w-full cursor-not-allowed rounded-lg border border-warm-200 bg-warm-100/70 px-3 py-1.5 text-xs font-semibold text-warm-800 dark:border-white/10 dark:bg-slate-800/80 dark:text-warm-200"
                  />
                </div>

                {/* Código Postal */}
                <div>
                  <label className="block text-[11px] font-bold uppercase tracking-wider text-warm-500 dark:text-warm-400">
                    {isSpanish ? 'Código postal' : 'Postal code'}
                  </label>
                  <input
                    type="text"
                    value={editForm.primaryZipCode || ''}
                    readOnly
                    disabled
                    className="mt-1 w-full cursor-not-allowed rounded-lg border border-warm-200 bg-warm-100/70 px-3 py-1.5 text-xs font-semibold text-warm-800 dark:border-white/10 dark:bg-slate-800/80 dark:text-warm-200"
                  />
                </div>

                {/* Coordenadas Lat/Long */}
                <div>
                  <label className="block text-[11px] font-bold uppercase tracking-wider text-warm-500 dark:text-warm-400">
                    {isSpanish ? 'Coordenadas (Lat, Long)' : 'Coordinates (Lat, Long)'}
                  </label>
                  <input
                    type="text"
                    value={
                      editForm.latitude != null && editForm.longitude != null
                        ? `${editForm.latitude.toFixed(6)}, ${editForm.longitude.toFixed(6)}`
                        : ''
                    }
                    readOnly
                    disabled
                    placeholder="—"
                    className="mt-1 w-full cursor-not-allowed rounded-lg border border-warm-200 bg-warm-100/70 px-3 py-1.5 font-mono text-xs font-semibold text-emerald-700 dark:border-white/10 dark:bg-slate-800/80 dark:text-emerald-400"
                  />
                </div>

                {/* Dirección completa calle, etc. */}
                <div className="sm:col-span-3">
                  <label className="block text-[11px] font-bold uppercase tracking-wider text-warm-500 dark:text-warm-400">
                    {isSpanish ? 'Dirección completa guardada en SQL' : 'Full address saved in SQL'}
                  </label>
                  <input
                    type="text"
                    value={editForm.primaryFullAddress || ''}
                    readOnly
                    disabled
                    placeholder="—"
                    className="mt-1 w-full cursor-not-allowed rounded-lg border border-warm-200 bg-warm-100/70 px-3 py-1.5 text-xs font-semibold text-warm-800 dark:border-white/10 dark:bg-slate-800/80 dark:text-warm-200"
                  />
                </div>
              </div>
            </div>
          </div>

          {/* Botones de acción al final del formulario */}
          <div className="flex items-center justify-end gap-3 border-t border-warm-100 pt-4 dark:border-white/10">
            <button
              type="button"
              onClick={handleCancelEditing}
              disabled={isSaving}
              className="rounded-xl border border-warm-300 bg-white px-4 py-2 text-sm font-semibold text-warm-700 hover:bg-warm-50 dark:border-white/10 dark:bg-slate-800 dark:text-white"
            >
              {isSpanish ? 'Cancelar' : 'Cancel'}
            </button>
            <button
              type="button"
              onClick={handleSave}
              disabled={isSaving}
              className="inline-flex items-center gap-2 rounded-xl bg-emerald-600 px-5 py-2 text-sm font-bold text-white shadow hover:bg-emerald-700 disabled:opacity-50"
            >
              {isSaving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
              {isSpanish ? 'Guardar cambios' : 'Save changes'}
            </button>
          </div>
        </section>
      )}

      {/* Tarjetas de Configuración General (Vista normal) */}
      <section>
        <h2 className="mb-4 text-lg font-bold text-warm-900 dark:text-white">
          {isSpanish ? 'Configuración general' : 'General configuration'}
        </h2>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          {dataCards.map(({ icon: Icon, label, value, color }) => (
            <div
              key={label}
              className="flex items-start gap-4 rounded-2xl border border-warm-200/80 bg-white p-5 shadow-xs dark:border-white/10 dark:bg-[#0f172a]"
            >
              <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${CARD_ICON_STYLES[color]}`}>
                <Icon className="h-5 w-5" />
              </span>
              <div className="min-w-0 flex-1">
                <p className="text-[11px] font-bold uppercase tracking-wider text-warm-400">{label}</p>
                <p className="mt-1 text-sm font-semibold text-warm-900 dark:text-white">{value}</p>
                {label.includes('Ubicación') || label.includes('Location') ? (
                  family.latitude != null && family.longitude != null ? (
                    <p className="mt-1 font-mono text-[11px] text-emerald-600 dark:text-emerald-400">
                      GPS: {family.latitude.toFixed(6)}, {family.longitude.toFixed(6)}
                    </p>
                  ) : null
                ) : null}
              </div>
            </div>
          ))}
        </div>
      </section>

      {/* Integrantes Registrados */}
      <section className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
        <div className="mb-5 flex items-center gap-3 border-b border-warm-100 pb-4 dark:border-white/10">
          <UsersRound className="h-5 w-5 text-indigo-600 dark:text-indigo-400" />
          <div>
            <h2 className="text-lg font-bold text-warm-900 dark:text-white">
              {isSpanish ? 'Integrantes registrados' : 'Registered members'}
            </h2>
            <p className="text-xs text-warm-500 dark:text-warm-400">
              {isSpanish ? 'Personas vinculadas al hogar familiar.' : 'People connected to this household.'}
            </p>
          </div>
        </div>

        {members.length === 0 ? (
          <p className="text-sm text-warm-500 dark:text-warm-400">
            {isSpanish ? 'Aún no hay integrantes registrados.' : 'No members registered yet.'}
          </p>
        ) : (
          <div className="space-y-3">
            {members.map((member) => (
              <div
                key={member.memberId}
                className="flex flex-col gap-3 rounded-2xl border border-warm-100 p-4 sm:flex-row sm:items-center sm:justify-between dark:border-white/10"
              >
                <div className="flex items-center gap-3">
                  <span
                    className={`flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br ${member.avatarBg} text-sm font-black text-white`}
                  >
                    {member.preferredName.charAt(0).toUpperCase()}
                  </span>
                  <div>
                    <p className="text-sm font-bold text-warm-900 dark:text-white">{member.fullName}</p>
                    <p className="text-xs text-indigo-600 dark:text-indigo-400">
                      {isSpanish ? member.roleTitleEs : member.roleTitleEn}
                    </p>
                  </div>
                </div>
                <div className="flex flex-wrap gap-2 text-[11px] text-warm-500 dark:text-warm-400">
                  {member.primaryLocation && (
                    <span className="rounded-full bg-warm-100 px-2.5 py-1 dark:bg-white/10">
                      {member.primaryLocation}
                    </span>
                  )}
                  {member.age != null && (
                    <span className="rounded-full bg-warm-100 px-2.5 py-1 dark:bg-white/10">
                      {member.age} {isSpanish ? 'años' : 'years'}
                    </span>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}


