import { useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, ArrowRight, GraduationCap, Sparkles, RefreshCw, Check } from 'lucide-react';
import { createProgram, uploadProgramLogo, generateProgramLogoPreview } from '../lib/api/programsApi';
import StepIndicator from '../components/StepIndicator';

const STEPS = ['Información básica', 'Objetivos y requisitos', 'Revisar y publicar'];

function base64ToFile(base64: string, contentType: string, fileName: string): File {
  const byteChars = atob(base64);
  const byteNumbers = new Array(byteChars.length);
  for (let i = 0; i < byteChars.length; i++) byteNumbers[i] = byteChars.charCodeAt(i);
  const blob = new Blob([new Uint8Array(byteNumbers)], { type: contentType });
  return new File([blob], fileName, { type: contentType });
}

export default function NewProgramPage() {
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
      setLogoGenError(err instanceof Error ? err.message : 'No se pudo generar el logo.');
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
        description: description.trim() || null,
        objectives: objectives.trim() || null,
        requirements: requirements.trim() || null,
      });

      if (logo && logoAccepted) {
        await uploadProgramLogo(program.ProgramId, logo);
      }

      navigate(`/programs/${program.ProgramId}`);
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : 'No se pudo crear el programa.');
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
            <h2 className="text-2xl font-bold text-white">Información básica</h2>
            <p className="mt-1 text-slate-400">Dale un nombre, descripción y logo a tu nuevo programa.</p>
          </div>

          <Field label="Nombre del programa">
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Ej. Ruta de Contabilidad Fiscal"
              className="input"
            />
          </Field>

          <Field label="Descripción (opcional)">
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={3}
              placeholder="¿Qué logrará el estudiante al completar este programa?"
              className="input resize-none"
            />
          </Field>

          <Field label="Logo del programa (opcional)" hint="Generado por IA (gpt-image-1.5) a partir del nombre y la descripción.">
            <div className="space-y-3">
              {logoPreviewUrl && (
                <div className="flex items-center gap-4 rounded-2xl border border-white/10 bg-white/[0.02] p-4">
                  <img src={logoPreviewUrl} alt="Logo generado" className="h-20 w-20 shrink-0 rounded-xl object-cover" />
                  <div className="min-w-0 flex-1">
                    {logoAccepted ? (
                      <p className="flex items-center gap-1.5 text-sm font-medium text-emerald-400">
                        <Check className="h-4 w-4" /> Logo aceptado
                      </p>
                    ) : (
                      <p className="text-sm text-slate-400">¿Usar este logo generado?</p>
                    )}
                    <div className="mt-2 flex gap-2">
                      {!logoAccepted && (
                        <button
                          type="button"
                          onClick={() => setLogoAccepted(true)}
                          className="inline-flex items-center gap-1.5 rounded-lg bg-brand-500/15 px-3 py-1.5 text-xs font-medium text-brand-300 hover:bg-brand-500/25"
                        >
                          <Check className="h-3.5 w-3.5" /> Aceptar
                        </button>
                      )}
                      <button
                        type="button"
                        onClick={handleGenerateLogo}
                        disabled={generatingLogo}
                        className="inline-flex items-center gap-1.5 rounded-lg border border-white/10 px-3 py-1.5 text-xs font-medium text-slate-300 hover:bg-white/5 disabled:opacity-50"
                      >
                        <RefreshCw className="h-3.5 w-3.5" /> Regenerar
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
                  {generatingLogo ? 'Generando logo...' : 'Generar logo con IA'}
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
            <h2 className="text-2xl font-bold text-white">Objetivos y requisitos</h2>
            <p className="mt-1 text-slate-400">Comunica al estudiante qué logrará y qué necesita antes de empezar.</p>
          </div>

          <Field label="Objetivos del programa">
            <textarea
              value={objectives}
              onChange={(e) => setObjectives(e.target.value)}
              rows={5}
              placeholder="Ej. Al terminar, el estudiante podrá calcular y presentar declaraciones de IVA..."
              className="input resize-none"
            />
          </Field>

          <Field label="Requisitos (opcional)">
            <textarea
              value={requirements}
              onChange={(e) => setRequirements(e.target.value)}
              rows={4}
              placeholder="Ej. Conocimientos básicos de aritmética y contabilidad general"
              className="input resize-none"
            />
          </Field>

          <WizardNav onBack={() => setStep(1)} onNext={() => setStep(3)} />
        </div>
      )}

      {step === 3 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">Revisar y publicar</h2>
            <p className="mt-1 text-slate-400">Confirma los detalles antes de crear el programa.</p>
          </div>

          <div className="space-y-3 rounded-2xl border border-white/10 bg-white/[0.03] p-5">
            <SummaryRow label="Nombre" value={name} />
            {description && <SummaryRow label="Descripción" value={description} />}
            <SummaryRow label="Logo" value={logo && logoAccepted ? 'Generado por IA' : 'Sin logo'} />
            <SummaryRow label="Objetivos" value={objectives || '—'} />
            <SummaryRow label="Requisitos" value={requirements || '—'} />
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
            nextLabel={submitting ? 'Creando...' : 'Crear programa'}
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
  nextLabel = 'Continuar',
  nextIcon: NextIcon = ArrowRight,
}: {
  onBack?: () => void;
  onNext: () => void;
  nextDisabled?: boolean;
  nextLabel?: string;
  nextIcon?: typeof ArrowRight;
}) {
  return (
    <div className="flex items-center justify-between pt-4">
      {onBack ? (
        <button
          type="button"
          onClick={onBack}
          className="flex items-center gap-1.5 rounded-xl px-4 py-2.5 text-sm font-medium text-slate-400 hover:text-white"
        >
          <ArrowLeft className="h-4 w-4" />
          Atrás
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
        {nextLabel}
        <NextIcon className="h-4 w-4" />
      </button>
    </div>
  );
}
