import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { FileText, MessageSquareText, Video, Link2, ArrowLeft, ArrowRight, Sparkles, Globe } from 'lucide-react';
import { getSubjects, type Subject } from '../lib/api/subjectsApi';
import { getCapabilityDomains, type CapabilityDomain } from '../lib/api/domainsApi';
import { getPrograms, getProgramById, type BackendProgram, type BackendProgramDetail } from '../lib/api/programsApi';
import {
  DEFAULT_STUDIO_TENANT_ID,
  readFileAsBase64,
  startPdfCapabilityGraph,
  startCapabilityGraphFromDescription,
  getActiveCapabilityGraphRun,
} from '../lib/api/pdfPipelineApi';
import StepIndicator from '../components/StepIndicator';
import TypeOptionCard from '../components/TypeOptionCard';
import FileDropzone from '../components/FileDropzone';
import { getSubjectIcon } from '../lib/subjectVisuals';

const STEPS = ['Información básica', 'Tipo de material', 'Cargar contenido', 'Revisar y crear'];

type CapabilityType = 'pdf' | 'text' | 'video' | 'link';

export default function NewCapabilityPage() {
  const navigate = useNavigate();
  const [step, setStep] = useState(1);

  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [domains, setDomains] = useState<CapabilityDomain[]>([]);
  const [programs, setPrograms] = useState<BackendProgram[]>([]);

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [subjectId, setSubjectId] = useState<string>('');
  const [capabilityDomainId, setCapabilityDomainId] = useState<string>('');
  const [programId, setProgramId] = useState<string>('');
  const [programDetail, setProgramDetail] = useState<BackendProgramDetail | null>(null);
  const [programSequenceNumber, setProgramSequenceNumber] = useState<number | null>(null);
  const [capabilityObjectives, setCapabilityObjectives] = useState('');
  const [capabilityRequirements, setCapabilityRequirements] = useState('');

  const MAX_PROGRAM_SEQUENCE_SLOTS = 20;

  const [capabilityType, setCapabilityType] = useState<CapabilityType>('pdf');
  const [file, setFile] = useState<File | null>(null);
  const [ideaDescription, setIdeaDescription] = useState('');
  const [enableWebEnrichment, setEnableWebEnrichment] = useState(false);

  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [conflictingRunId, setConflictingRunId] = useState<string | null>(null);

  useEffect(() => {
    getSubjects().then(setSubjects).catch(() => setSubjects([]));
    getCapabilityDomains().then(setDomains).catch(() => setDomains([]));
    getPrograms().then(setPrograms).catch(() => setPrograms([]));
  }, []);

  const selectedProgram = useMemo(
    () => programs.find((p) => p.ProgramId === programId) ?? null,
    [programs, programId],
  );

  // Map of occupied sequence slots (1..20) → the capability name already
  // sitting there, so the picker below can disable them without forcing
  // the designer to fill gaps in order (e.g. picking #8 while #6 is still
  // empty is perfectly valid).
  const occupiedSlots = useMemo(() => {
    const map = new Map<number, string>();
    for (const pc of programDetail?.Capabilities ?? []) {
      map.set(pc.SortOrder, pc.CapabilityName);
    }
    return map;
  }, [programDetail]);

  useEffect(() => {
    if (!programId) {
      setProgramDetail(null);
      setProgramSequenceNumber(null);
      return;
    }
    let cancelled = false;
    getProgramById(programId)
      .then((detail) => {
        if (cancelled) return;
        setProgramDetail(detail);
        // Auto-suggest the smallest free slot, but the designer can still
        // freely override it below — never forced to be contiguous.
        const taken = new Set(detail.Capabilities.map((c) => c.SortOrder));
        let suggested = 1;
        while (taken.has(suggested) && suggested <= MAX_PROGRAM_SEQUENCE_SLOTS) suggested += 1;
        setProgramSequenceNumber(suggested <= MAX_PROGRAM_SEQUENCE_SLOTS ? suggested : null);
      })
      .catch(() => {
        if (!cancelled) setProgramDetail(null);
      });
    return () => {
      cancelled = true;
    };
  }, [programId]);

  const canAdvanceFromStep1 = name.trim().length > 0 && capabilityDomainId.length > 0;
  const canAdvanceFromStep3 =
    capabilityType === 'pdf' ? file !== null : capabilityType === 'text' ? ideaDescription.trim().length > 0 : false;

  const canSubmit = useMemo(() => {
    if (!canAdvanceFromStep1) return false;
    if (capabilityType === 'pdf') return file !== null;
    if (capabilityType === 'text') return ideaDescription.trim().length > 0;
    return false;
  }, [canAdvanceFromStep1, capabilityType, file, ideaDescription]);

  async function handleSubmit() {
    setSubmitting(true);
    setSubmitError(null);
    setConflictingRunId(null);
    try {
      if (capabilityType === 'pdf') {
        if (!file) return;
        const contentBase64 = await readFileAsBase64(file);
        const run = await startPdfCapabilityGraph({
          tenantId: DEFAULT_STUDIO_TENANT_ID,
          capabilityDomainId,
          subjectId: subjectId || null,
          capabilityName: name.trim(),
          fileName: file.name,
          contentBase64,
          enableWebEnrichment,
          programId: programId || null,
          programSequenceNumber: programId ? programSequenceNumber : null,
          capabilityObjectives: programId ? capabilityObjectives.trim() || null : null,
          capabilityRequirements: programId ? capabilityRequirements.trim() || null : null,
        });
        navigate(`/runs/${run.RunId}`);
      } else if (capabilityType === 'text') {
        const run = await startCapabilityGraphFromDescription({
          tenantId: DEFAULT_STUDIO_TENANT_ID,
          capabilityDomainId,
          subjectId: subjectId || null,
          capabilityName: name.trim(),
          description: ideaDescription.trim(),
          enableWebEnrichment,
          programId: programId || null,
          programSequenceNumber: programId ? programSequenceNumber : null,
          capabilityObjectives: programId ? capabilityObjectives.trim() || null : null,
          capabilityRequirements: programId ? capabilityRequirements.trim() || null : null,
        });
        navigate(`/runs/${run.RunId}`);
      }
    } catch (err) {
      // Only one capability generation is allowed at a time — if this
      // failed because another run is still in progress, link straight to
      // it instead of just showing a generic error message.
      const activeRun = await getActiveCapabilityGraphRun().catch(() => null);
      if (activeRun) {
        setConflictingRunId(activeRun.RunId);
        setSubmitError('Ya hay una capability generándose. Espera a que termine antes de iniciar otra.');
      } else {
        setSubmitError(err instanceof Error ? err.message : 'No se pudo iniciar la creación.');
      }
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
            <p className="mt-1 text-slate-400">Dale un nombre y clasificación a tu nueva capability.</p>
          </div>

          <Field label="Nombre de la capability">
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Ej. Ley del IVA Mexicana"
              className="input"
            />
          </Field>

          <Field label="Descripción (opcional)">
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={3}
              placeholder="¿Qué aprenderá el estudiante?"
              className="input resize-none"
            />
          </Field>

          <Field label="Dominio / Tema (Subject)">
            <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
              {subjects.map((subject) => {
                const Icon = getSubjectIcon(subject.Code);
                const active = subjectId === subject.SubjectId;
                return (
                  <button
                    key={subject.SubjectId}
                    type="button"
                    onClick={() => setSubjectId(active ? '' : subject.SubjectId)}
                    className={`flex items-center gap-2 rounded-xl border px-3 py-2.5 text-left text-sm transition-colors ${
                      active
                        ? 'border-brand-400/60 bg-brand-500/10 text-white'
                        : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
                    }`}
                  >
                    <Icon className="h-4 w-4 shrink-0" />
                    <span className="truncate">{subject.Name}</span>
                  </button>
                );
              })}
            </div>
          </Field>

          <Field label="Categoría de autoría (CapabilityDomain)" hint="Metadato interno del Studio, no se muestra al estudiante.">
            <select value={capabilityDomainId} onChange={(e) => setCapabilityDomainId(e.target.value)} className="input">
              <option value="">Selecciona una categoría...</option>
              {domains.map((domain) => (
                <option key={domain.CapabilityDomainId} value={domain.CapabilityDomainId}>
                  {domain.Name}
                </option>
              ))}
            </select>
          </Field>

          <Field label="Programa (opcional)" hint="Si seleccionas un programa, esta capability se agregará al final de su secuencia.">
            <select value={programId} onChange={(e) => setProgramId(e.target.value)} className="input">
              <option value="">Ninguno</option>
              {programs.map((program) => (
                <option key={program.ProgramId} value={program.ProgramId}>
                  {program.Name}
                </option>
              ))}
            </select>
          </Field>

          {selectedProgram && (
            <div className="space-y-4 rounded-xl border border-white/10 bg-white/[0.02] p-4 text-sm">
              <p className="text-slate-300">
                Se conectará al programa <span className="font-semibold text-white">{selectedProgram.Name}</span>.
              </p>
              {selectedProgram.Objectives && (
                <p className="text-slate-400">
                  <span className="font-medium text-slate-300">Objetivos del programa: </span>
                  {selectedProgram.Objectives}
                </p>
              )}

              <div>
                <label className="mb-1.5 block text-sm font-medium text-slate-300">
                  Número de secuencia en el programa
                </label>
                <p className="mb-2 text-xs text-slate-500">
                  Elige cualquier número del 1 al {MAX_PROGRAM_SEQUENCE_SLOTS}. No es necesario que existan las
                  posiciones anteriores — puedes crear la #8 aunque la #6 todavía no exista.
                </p>
                <div className="grid grid-cols-5 gap-2 sm:grid-cols-10">
                  {Array.from({ length: MAX_PROGRAM_SEQUENCE_SLOTS }, (_, i) => i + 1).map((slot) => {
                    const takenBy = occupiedSlots.get(slot);
                    const selected = programSequenceNumber === slot;
                    return (
                      <button
                        key={slot}
                        type="button"
                        disabled={!!takenBy}
                        title={takenBy ? `Ocupado por: ${takenBy}` : `Usar posición #${slot}`}
                        onClick={() => setProgramSequenceNumber(slot)}
                        className={`rounded-lg border px-2 py-2 text-xs font-medium transition-colors ${
                          takenBy
                            ? 'cursor-not-allowed border-white/5 bg-white/[0.02] text-slate-600 line-through'
                            : selected
                              ? 'border-brand-400/60 bg-brand-500/10 text-white'
                              : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
                        }`}
                      >
                        {slot}
                      </button>
                    );
                  })}
                </div>
              </div>

              <Field
                label="Objetivos de esta capability en el programa (opcional)"
                hint="Qué debe lograr específicamente esta capability dentro de la secuencia del programa."
              >
                <textarea
                  value={capabilityObjectives}
                  onChange={(e) => setCapabilityObjectives(e.target.value)}
                  rows={2}
                  placeholder="Ej. Que el estudiante domine las operaciones básicas antes de avanzar al álgebra"
                  className="input resize-none"
                />
              </Field>

              <Field
                label="Requisitos previos dentro del programa (opcional)"
                hint="Qué debe haber completado o dominado el estudiante antes de esta capability, dentro de este programa."
              >
                <textarea
                  value={capabilityRequirements}
                  onChange={(e) => setCapabilityRequirements(e.target.value)}
                  rows={2}
                  placeholder="Ej. Haber completado la capability #3 (Fundamentos de aritmética)"
                  className="input resize-none"
                />
              </Field>
            </div>
          )}

          <WizardNav onNext={() => setStep(2)} nextDisabled={!canAdvanceFromStep1} />
        </div>
      )}

      {step === 2 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">Tipo de material</h2>
            <p className="mt-1 text-slate-400">¿A partir de qué contenido quieres generar la capability?</p>
          </div>

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <TypeOptionCard
              icon={FileText}
              title="Documento PDF"
              description="Sube un manual, libro o guía en PDF."
              selected={capabilityType === 'pdf'}
              onClick={() => setCapabilityType('pdf')}
            />
            <TypeOptionCard
              icon={MessageSquareText}
              title="Texto / idea"
              description="Describe el tema directamente."
              selected={capabilityType === 'text'}
              onClick={() => setCapabilityType('text')}
            />
            <TypeOptionCard
              icon={Video}
              title="Video"
              description="Genera contenido a partir de un video."
              selected={capabilityType === 'video'}
              disabled
              onClick={() => {}}
            />
            <TypeOptionCard
              icon={Link2}
              title="Enlace web"
              description="Extrae contenido desde una URL."
              selected={capabilityType === 'link'}
              disabled
              onClick={() => {}}
            />
          </div>

          <WizardNav onBack={() => setStep(1)} onNext={() => setStep(3)} />
        </div>
      )}

      {step === 3 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">Cargar contenido</h2>
            <p className="mt-1 text-slate-400">
              {capabilityType === 'pdf'
                ? 'El PDF será procesado por el pipeline Curador → GraphArchitect.'
                : 'Tu descripción será expandida por IA en material de curso y luego procesada por el mismo pipeline Curador → GraphArchitect.'}
            </p>
          </div>

          {capabilityType === 'pdf' && <FileDropzone file={file} onFileSelected={setFile} />}

          {capabilityType === 'text' && (
            <Field label="Describe el tema y lo que debe aprender el estudiante">
              <textarea
                value={ideaDescription}
                onChange={(e) => setIdeaDescription(e.target.value)}
                rows={5}
                placeholder="Ej. Capacidad para que un niño aprenda a sumar y restar"
                className="input resize-none"
              />
            </Field>
          )}

          <label className="flex items-center gap-3 rounded-2xl border border-white/10 bg-white/[0.02] p-4">
            <input
              type="checkbox"
              checked={enableWebEnrichment}
              onChange={(e) => setEnableWebEnrichment(e.target.checked)}
              className="h-4 w-4 rounded border-white/20 bg-white/5 text-brand-500 focus:ring-brand-500"
            />
            <div className="flex items-center gap-2 text-sm text-white">
              <Globe className="h-4 w-4 text-slate-400" />
              Enriquecer con búsqueda web (opcional)
            </div>
          </label>

          <WizardNav onBack={() => setStep(2)} onNext={() => setStep(4)} nextDisabled={!canAdvanceFromStep3} />
        </div>
      )}

      {step === 4 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">Revisar y crear</h2>
            <p className="mt-1 text-slate-400">Confirma los detalles antes de iniciar la generación.</p>
          </div>

          <div className="space-y-3 rounded-2xl border border-white/10 bg-white/[0.03] p-5">
            <SummaryRow label="Nombre" value={name} />
            {description && <SummaryRow label="Descripción" value={description} />}
            <SummaryRow
              label="Dominio"
              value={subjects.find((s) => s.SubjectId === subjectId)?.Name ?? 'Sin especificar'}
            />
            <SummaryRow
              label="Categoría"
              value={domains.find((d) => d.CapabilityDomainId === capabilityDomainId)?.Name ?? '—'}
            />
            {capabilityType === 'pdf' ? (
              <SummaryRow label="Archivo" value={file?.name ?? '—'} />
            ) : (
              <SummaryRow label="Idea" value={ideaDescription || '—'} />
            )}
            <SummaryRow label="Enriquecimiento web" value={enableWebEnrichment ? 'Activado' : 'Desactivado'} />
            {selectedProgram && (
              <>
                <SummaryRow label="Programa" value={selectedProgram.Name} />
                <SummaryRow
                  label="Secuencia"
                  value={programSequenceNumber ? `#${programSequenceNumber}` : 'Sin asignar'}
                />
                {capabilityObjectives && <SummaryRow label="Objetivos en el programa" value={capabilityObjectives} />}
                {capabilityRequirements && (
                  <SummaryRow label="Requisitos en el programa" value={capabilityRequirements} />
                )}
              </>
            )}
          </div>

          {submitError && (
            <div className="rounded-xl border border-red-500/20 bg-red-500/5 p-4 text-sm text-red-300">
              {submitError}
              {conflictingRunId && (
                <Link
                  to={`/runs/${conflictingRunId}`}
                  className="ml-1.5 inline-flex items-center gap-1 font-medium text-brand-400 hover:text-brand-300"
                >
                  Ver progreso <ArrowRight className="h-3.5 w-3.5" />
                </Link>
              )}
            </div>
          )}

          <WizardNav
            onBack={() => setStep(3)}
            onNext={handleSubmit}
            nextDisabled={!canSubmit || submitting}
            nextLabel={submitting ? 'Iniciando...' : 'Crear capability'}
            nextIcon={Sparkles}
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
      <span className="text-right font-medium text-white">{value}</span>
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
