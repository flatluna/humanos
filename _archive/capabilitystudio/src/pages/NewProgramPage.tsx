import { useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, ArrowRight, GraduationCap, Sparkles, RefreshCw, Check } from 'lucide-react';
import { createProgram, uploadProgramLogo, generateProgramLogoPreview } from '../lib/api/programsApi';
import StepIndicator from '../components/StepIndicator';
import { useAuth } from '../auth/AuthContext';

function base64ToFile(base64: string, contentType: string, fileName: string): File {
  const byteChars = atob(base64);
  const byteNumbers = new Array(byteChars.length);
  for (let i = 0; i < byteChars.length; i++) byteNumbers[i] = byteChars.charCodeAt(i);
  const blob = new Blob([new Uint8Array(byteNumbers)], { type: contentType });
  return new File([blob], fileName, { type: contentType });
}

export default function NewProgramPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const STEPS = t('newProgram.steps', { returnObjects: true }) as string[];
  const navigate = useNavigate();
  const [step, setStep] = useState(1);

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');

  const [logo, setLogo] = useState<File | null>(null);
  const [logoPreviewUrl, setLogoPreviewUrl] = useState<string | null>(null);
  const [logoAccepted, setLogoAccepted] = useState(false);
  const [generatingLogo, setGeneratingLogo] = useState(false);
  const [logoGenError, setLogoGenError] = useState<string | null>(null);

  const [objectives, setObjectives] = useState('');
  const [requirements, setRequirements] = useState('');

  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const canAdvanceFromStep1 = name.trim().length > 0;

  async function handleGenerateLogo() {
    if (!name.trim()) return;
    setGeneratingLogo(true);
    setLogoGenError(null);
    setLogoAccepted(false);
    try {
      const result = await generateProgramLogoPreview(name.trim(), description.trim() || null);
      const file = base64ToFile(result.ImageBase64, result.ContentType, 'program-logo.png');
      setLogo(file);
      setLogoPreviewUrl(`data:${result.ContentType};base64,${result.ImageBase64}`);
    } catch (err) {
      setLogoGenError(err instanceof Error ? err.message : t('newProgram.logoGenErrorFallback'));
    } finally {
      setGeneratingLogo(false);
    }
  }

  async function handleSubmit() {
    setSubmitting(true);
    setSubmitError(null);
    try {
      const program = await createProgram({
        name: name.trim(),
        tenantId: user?.tenantId ?? sessionStorage.getItem('humanos.studio.inheritedTenantId') ?? null,
        description: description.trim() || null,
        objectives: objectives.trim() || null,
        requirements: requirements.trim() || null,
      });

      if (logo && logoAccepted) {
        await uploadProgramLogo(program.ProgramId, logo);
      }

      navigate(`/programs/${program.ProgramId}`);
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : t('newProgram.createErrorFallback'));
      setSubmitting(false);
    }
  }

  return (
    <div className="mx-auto max-w-3xl px-6 py-12">
      <div className="mb-10">
        <StepIndicator steps={STEPS} currentStep={step} />
      </div>

      {step === 1 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newProgram.step1Title')}</h2>
            <p className="mt-1 text-slate-400">{t('newProgram.step1Subtitle')}</p>
          </div>

          <Field label={t('newProgram.nameLabel')}>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder={t('newProgram.namePlaceholder')}
              className="input"
            />
          </Field>

          <Field label={t('newProgram.descriptionLabel')}>
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={3}
              placeholder={t('newProgram.descriptionPlaceholder')}
              className="input resize-none"
            />
          </Field>

          <Field label={t('newProgram.logoLabel')} hint={t('newProgram.logoHint')}>
            <div className="space-y-3">
              {logoPreviewUrl && (
                <div className="flex items-center gap-4 rounded-2xl border border-white/10 bg-white/[0.02] p-4">
                  <img src={logoPreviewUrl} alt={t('newProgram.logoAlt')} className="h-20 w-20 shrink-0 rounded-xl object-contain object-center" />
                  <div className="min-w-0 flex-1">
                    {logoAccepted ? (
                      <p className="flex items-center gap-1.5 text-sm font-medium text-emerald-400">
                        <Check className="h-4 w-4" /> {t('newProgram.logoAccepted')}
                      </p>
                    ) : (
                      <p className="text-sm text-slate-400">{t('newProgram.useThisLogo')}</p>
                    )}
                    <div className="mt-2 flex gap-2">
                      {!logoAccepted && (
                        <button
                          type="button"
                          onClick={() => setLogoAccepted(true)}
                          className="inline-flex items-center gap-1.5 rounded-lg bg-brand-500/15 px-3 py-1.5 text-xs font-medium text-brand-300 hover:bg-brand-500/25"
                        >
                          <Check className="h-3.5 w-3.5" /> {t('newProgram.accept')}
                        </button>
                      )}
                      <button
                        type="button"
                        onClick={handleGenerateLogo}
                        disabled={generatingLogo}
                        className="inline-flex items-center gap-1.5 rounded-lg border border-white/10 px-3 py-1.5 text-xs font-medium text-slate-300 hover:bg-white/5 disabled:opacity-50"
                      >
                        <RefreshCw className="h-3.5 w-3.5" /> {t('newProgram.regenerate')}
                      </button>
                    </div>
                  </div>
                </div>
              )}

              {!logoPreviewUrl && (
                <button
                  type="button"
                  onClick={handleGenerateLogo}
                  disabled={!name.trim() || generatingLogo}
                  className="flex w-full items-center justify-center gap-2 rounded-2xl border border-dashed border-white/15 bg-white/[0.02] py-8 text-sm font-medium text-slate-300 hover:border-white/25 hover:bg-white/[0.04] disabled:cursor-not-allowed disabled:opacity-40"
                >
                  <Sparkles className={`h-4 w-4 ${generatingLogo ? 'animate-pulse' : ''}`} />
                  {generatingLogo ? t('newProgram.generatingLogo') : t('newProgram.generateLogo')}
                </button>
              )}

              {logoGenError && <p className="text-xs text-red-300">{logoGenError}</p>}
            </div>
          </Field>

          <WizardNav onNext={() => setStep(2)} nextDisabled={!canAdvanceFromStep1} />
        </div>
      )}

      {step === 2 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newProgram.step2Title')}</h2>
            <p className="mt-1 text-slate-400">{t('newProgram.step2Subtitle')}</p>
          </div>

          <Field label={t('newProgram.objectivesLabel')}>
            <textarea
              value={objectives}
              onChange={(e) => setObjectives(e.target.value)}
              rows={5}
              placeholder={t('newProgram.objectivesPlaceholder')}
              className="input resize-none"
            />
          </Field>

          <Field label={t('newProgram.requirementsLabel')}>
            <textarea
              value={requirements}
              onChange={(e) => setRequirements(e.target.value)}
              rows={4}
              placeholder={t('newProgram.requirementsPlaceholder')}
              className="input resize-none"
            />
          </Field>

          <WizardNav onBack={() => setStep(1)} onNext={() => setStep(3)} />
        </div>
      )}

      {step === 3 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newProgram.step3Title')}</h2>
            <p className="mt-1 text-slate-400">{t('newProgram.step3Subtitle')}</p>
          </div>

          <div className="space-y-3 rounded-2xl border border-white/10 bg-white/[0.03] p-5">
            <SummaryRow label={t('newProgram.summaryName')} value={name} />
            {description && <SummaryRow label={t('newProgram.summaryDescription')} value={description} />}
            <SummaryRow label={t('newProgram.summaryLogo')} value={logo && logoAccepted ? t('newProgram.logoGenerated') : t('newProgram.noLogo')} />
            <SummaryRow label={t('newProgram.summaryObjectives')} value={objectives || t('newProgram.dash')} />
            <SummaryRow label={t('newProgram.summaryRequirements')} value={requirements || t('newProgram.dash')} />
          </div>

          {submitError && (
            <div className="rounded-xl border border-red-500/20 bg-red-500/5 p-4 text-sm text-red-300">
              {submitError}
            </div>
          )}

          <WizardNav
            onBack={() => setStep(2)}
            onNext={handleSubmit}
            nextDisabled={submitting}
            nextLabel={submitting ? t('newProgram.creating') : t('newProgram.createProgram')}
            nextIcon={GraduationCap}
          />
        </div>
      )}
    </div>
  );
}

function Field({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
  return (
    <div>
      <label className="mb-1.5 block text-sm font-medium text-slate-300">{label}</label>
      {children}
      {hint && <p className="mt-1.5 text-xs text-slate-500">{hint}</p>}
    </div>
  );
}

function SummaryRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-start justify-between gap-4 text-sm">
      <span className="text-slate-400">{label}</span>
      <span className="max-w-[70%] text-right font-medium text-white">{value}</span>
    </div>
  );
}

function WizardNav({
  onBack,
  onNext,
  nextDisabled,
  nextLabel,
  nextIcon: NextIcon = ArrowRight,
}: {
  onBack?: () => void;
  onNext: () => void;
  nextDisabled?: boolean;
  nextLabel?: string;
  nextIcon?: typeof ArrowRight;
}) {
  const { t } = useTranslation();
  const resolvedNextLabel = nextLabel ?? t('common.continue');
  return (
    <div className="flex items-center justify-between pt-4">
      {onBack ? (
        <button
          type="button"
          onClick={onBack}
          className="flex items-center gap-1.5 rounded-xl px-4 py-2.5 text-sm font-medium text-slate-400 hover:text-white"
        >
          <ArrowLeft className="h-4 w-4" />
          {t('common.back')}
        </button>
      ) : (
        <span />
      )}
      <button
        type="button"
        onClick={onNext}
        disabled={nextDisabled}
        className="flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform enabled:hover:scale-[1.02] disabled:cursor-not-allowed disabled:opacity-40"
      >
        {resolvedNextLabel}
        <NextIcon className="h-4 w-4" />
      </button>
    </div>
  );
}
