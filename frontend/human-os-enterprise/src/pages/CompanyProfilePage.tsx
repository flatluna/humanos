import { useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery } from '@tanstack/react-query';
import { ArrowLeft, Building2, ImagePlus, Loader2, Save } from 'lucide-react';
import { useNavigate } from 'react-router';
import { useAuth } from '@/auth/AuthContext';
import { getTenant, updateTenant, uploadTenantLogo } from '@/api/orgStructureApi';
import { apiClient } from '@/api/index';

const COMPANY_TYPES = [
  ['Corporate', 'Corporativa'],
  ['Technology company', 'Empresa tecnológica'],
  ['Software / SaaS', 'Software / SaaS'],
  ['IT services', 'Servicios de TI'],
  ['Consulting', 'Consultoría'],
  ['Financial services', 'Servicios financieros'],
  ['Banking', 'Banca'],
  ['Insurance', 'Seguros'],
  ['Healthcare', 'Salud'],
  ['Education', 'Educación'],
  ['Government', 'Gobierno'],
  ['Nonprofit', 'Organización sin fines de lucro'],
  ['Manufacturing', 'Manufactura'],
  ['Retail', 'Comercio minorista'],
  ['Wholesale', 'Comercio mayorista'],
  ['Logistics and transportation', 'Logística y transporte'],
  ['Construction', 'Construcción'],
  ['Real estate', 'Bienes raíces'],
  ['Energy and utilities', 'Energía y servicios públicos'],
  ['Media and entertainment', 'Medios y entretenimiento'],
] as const;

export function CompanyProfilePage() {
  const { i18n } = useTranslation();
  const { user } = useAuth();
  const navigate = useNavigate();
  const logoInputRef = useRef<HTMLInputElement | null>(null);
  const [logoVersion, setLogoVersion] = useState(Date.now());
  const [logoError, setLogoError] = useState<string | null>(null);
  const tenantId = user?.tenantId;
  const tenantQuery = useQuery({
    queryKey: ['tenant', tenantId],
    queryFn: () => getTenant(tenantId!),
    enabled: Boolean(tenantId),
  });
  const [name, setName] = useState<string | null>(null);
  const [domain, setDomain] = useState<string | null>(null);
  const [description, setDescription] = useState<string | null>(null);
  const [cultureCode, setCultureCode] = useState<string | null>(null);
  const [timeZone, setTimeZone] = useState<string | null>(null);
  const [orgType, setOrgType] = useState<string | null>(null);
  const [isActive, setIsActive] = useState<boolean | null>(null);

  const tenant = tenantQuery.data;
  const values = {
    name: name ?? tenant?.name ?? user?.tenantName ?? '',
    domain: domain ?? tenant?.domain ?? '',
    description: description ?? tenant?.description ?? '',
    cultureCode: cultureCode ?? tenant?.cultureCode ?? 'es-MX',
    timeZone: timeZone ?? tenant?.timeZone ?? 'America/Mexico_City',
    orgType: orgType ?? tenant?.orgType ?? 'Corporate',
    isActive: isActive ?? tenant?.isActive ?? true,
  };
  const isEnglish = i18n.language === 'en';
  const labels = isEnglish
    ? { back: 'Back to organization', title: 'Company profile', intro: 'Update the main information for', name: 'Company name', domain: 'Domain', language: 'Language', timeZone: 'Time zone', type: 'Company type', active: 'Active company', description: 'Description', save: 'Save changes', saved: 'Company information saved successfully.', saveError: 'Unable to save changes.', loadError: 'Unable to load company information.' }
    : { back: 'Volver a organización', title: 'Perfil de la empresa', intro: 'Actualiza la información principal de', name: 'Nombre de la empresa', domain: 'Dominio', language: 'Idioma', timeZone: 'Zona horaria', type: 'Tipo de empresa', active: 'Empresa activa', description: 'Descripción', save: 'Guardar cambios', saved: 'Los datos de la empresa se guardaron correctamente.', saveError: 'No se pudieron guardar los cambios.', loadError: 'No se pudo cargar la información de la empresa.' };

  const saveMutation = useMutation({
    mutationFn: () => updateTenant(tenantId!, values),
    onSuccess: async (savedTenant) => {
      setName(savedTenant.name);
      setDomain(savedTenant.domain);
      setDescription(savedTenant.description);
      setCultureCode(savedTenant.cultureCode);
      setTimeZone(savedTenant.timeZone);
      setOrgType(savedTenant.orgType);
      setIsActive(savedTenant.isActive);
    },
  });

  const logoMutation = useMutation({
    mutationFn: (file: File) => uploadTenantLogo(tenantId!, file),
    onSuccess: () => {
      setLogoError(null);
      setLogoVersion(Date.now());
    },
    onError: (error) => setLogoError(error instanceof Error ? error.message : labels.saveError),
  });

  function handleLogoSelected(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    if (!file.type.startsWith('image/')) {
      setLogoError(isEnglish ? 'Select an image file.' : 'Selecciona un archivo de imagen.');
      return;
    }
    logoMutation.mutate(file);
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    saveMutation.mutate();
  }

  if (tenantQuery.isLoading) {
    return <div className="flex min-h-64 items-center justify-center"><Loader2 className="h-6 w-6 animate-spin text-violet-600" /></div>;
  }

  if (tenantQuery.isError || !tenant) {
    return <div className="mx-auto max-w-3xl rounded-2xl border border-red-200 bg-red-50 p-6 text-sm text-red-700">{labels.loadError}</div>;
  }

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <button type="button" onClick={() => navigate('/organizacion')} className="mb-4 inline-flex items-center gap-2 text-sm text-slate-500 hover:text-violet-600">
            <ArrowLeft className="h-4 w-4" /> {labels.back}
          </button>
          <div className="flex items-center gap-3">
            <img
              src={`${apiClient.defaults.baseURL}/tenants/${tenant.tenantId}/logo?v=${logoVersion}`}
              alt=""
              className="h-14 w-14 rounded-2xl border border-slate-200 bg-white object-contain p-1 dark:border-white/10 dark:bg-white/5"
            />
            <div className="rounded-2xl bg-violet-100 p-3 text-violet-700 dark:bg-violet-500/15 dark:text-violet-300"><Building2 className="h-6 w-6" /></div>
            <div>
              <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">{labels.title}</h1>
              <p className="mt-1 text-sm text-slate-500 dark:text-white/50">{labels.intro} {user?.tenantName ?? tenant.name}.</p>
            </div>
          </div>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="space-y-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-white/[0.03]">
        <div className="flex items-center justify-between rounded-xl border border-dashed border-slate-300 p-4 dark:border-white/15">
          <div>
            <p className="text-sm font-medium text-slate-700 dark:text-white/80">{isEnglish ? 'Company logo' : 'Logo de la empresa'}</p>
            <p className="mt-1 text-xs text-slate-500 dark:text-white/50">{isEnglish ? 'Upload a new image to replace the current logo.' : 'Sube una imagen nueva para reemplazar el logo actual.'}</p>
          </div>
          <>
            <input ref={logoInputRef} type="file" accept="image/*" className="hidden" onChange={handleLogoSelected} />
            <button type="button" onClick={() => logoInputRef.current?.click()} disabled={logoMutation.isPending} className="inline-flex items-center gap-2 rounded-xl border border-violet-200 px-3 py-2 text-sm font-medium text-violet-700 hover:bg-violet-50 disabled:opacity-60 dark:border-violet-500/30 dark:text-violet-300 dark:hover:bg-violet-500/10">
              {logoMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <ImagePlus className="h-4 w-4" />}
              {logoMutation.isPending ? (isEnglish ? 'Uploading...' : 'Subiendo...') : (isEnglish ? 'Upload logo' : 'Subir logo')}
            </button>
          </>
        </div>
        {logoError && <p className="rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700 dark:bg-red-500/10 dark:text-red-300">{logoError}</p>}
        <div className="grid gap-5 sm:grid-cols-2">
          <label className="sm:col-span-2"><span className="mb-2 block text-sm font-medium text-slate-700 dark:text-white/80">{labels.name}</span><input required value={values.name} onChange={(e) => setName(e.target.value)} className="w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm outline-none focus:border-violet-500 focus:ring-2 focus:ring-violet-500/20 dark:border-white/15 dark:bg-white/5 dark:text-white" /></label>
          <div className="sm:col-span-2"><span className="mb-2 block text-sm font-medium text-slate-700 dark:text-white/80">Tenant ID</span><div className="rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5 font-mono text-xs text-slate-500 dark:border-white/10 dark:bg-white/5 dark:text-white/50">{tenant.tenantId || user?.tenantId}</div></div>
          <label><span className="mb-2 block text-sm font-medium text-slate-700 dark:text-white/80">{labels.domain}</span><input value={values.domain} onChange={(e) => setDomain(e.target.value)} placeholder="empresa.com" className="w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm outline-none focus:border-violet-500 focus:ring-2 focus:ring-violet-500/20 dark:border-white/15 dark:bg-white/5 dark:text-white" /></label>
          <label><span className="mb-2 block text-sm font-medium text-slate-700 dark:text-white/80">{labels.language}</span><select value={values.cultureCode} onChange={(e) => { const culture = e.target.value; setCultureCode(culture); void i18n.changeLanguage(culture.startsWith('en') ? 'en' : 'es'); }} className="w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm outline-none focus:border-violet-500 dark:border-white/15 dark:bg-white/5 dark:text-white"><option value="es-MX">Español (México)</option><option value="es-ES">Español (España)</option><option value="en-US">English (United States)</option></select></label>
          <label><span className="mb-2 block text-sm font-medium text-slate-700 dark:text-white/80">{labels.timeZone}</span><input required value={values.timeZone} onChange={(e) => setTimeZone(e.target.value)} className="w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm outline-none focus:border-violet-500 dark:border-white/15 dark:bg-white/5 dark:text-white" /></label>
          <label><span className="mb-2 block text-sm font-medium text-slate-700 dark:text-white/80">{labels.type}</span><select value={values.orgType} onChange={(e) => setOrgType(e.target.value)} className="w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm outline-none focus:border-violet-500 dark:border-white/15 dark:bg-white/5 dark:text-white">{COMPANY_TYPES.map(([english, spanish]) => <option key={english} value={english}>{spanish} / {english}</option>)}</select></label>
          <label className="flex items-center gap-3 self-end pb-2"><input type="checkbox" checked={values.isActive} onChange={(e) => setIsActive(e.target.checked)} className="h-4 w-4 rounded border-slate-300 text-violet-600" /><span className="text-sm font-medium text-slate-700 dark:text-white/80">{labels.active}</span></label>
          <label className="sm:col-span-2"><span className="mb-2 block text-sm font-medium text-slate-700 dark:text-white/80">{labels.description}</span><textarea rows={5} value={values.description} onChange={(e) => setDescription(e.target.value)} className="w-full resize-y rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm outline-none focus:border-violet-500 focus:ring-2 focus:ring-violet-500/20 dark:border-white/15 dark:bg-white/5 dark:text-white" /></label>
        </div>

        {saveMutation.isError && <p className="rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700 dark:bg-red-500/10 dark:text-red-300">{labels.saveError} {saveMutation.error instanceof Error ? saveMutation.error.message : ''}</p>}
        {saveMutation.isSuccess && <p className="rounded-xl bg-emerald-50 px-4 py-3 text-sm text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300">{labels.saved}</p>}
        <div className="flex justify-end"><button type="submit" disabled={saveMutation.isPending} className="inline-flex items-center gap-2 rounded-xl bg-violet-600 px-5 py-2.5 text-sm font-medium text-white shadow-sm transition hover:bg-violet-700 disabled:cursor-not-allowed disabled:opacity-60">{saveMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}{saveMutation.isPending ? (isEnglish ? 'Saving...' : 'Guardando...') : labels.save}</button></div>
      </form>
    </div>
  );
}
