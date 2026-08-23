import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Check } from 'lucide-react';
import { useAuth } from '../auth/AuthContext';
import { getSubjects, type Subject } from '../lib/api/subjectsApi';
import { createStructuredLearningTopic } from '../lib/api/structuredLearningTopicsApi';

const DEFAULT_STUDIO_TENANT_ID = '00000000-0000-0000-0000-000000000000';

export default function StructuredLearningTopicCreatePage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const { subjectName = '' } = useParams<{ subjectName: string }>();
  const tenantId = user?.tenantId ?? DEFAULT_STUDIO_TENANT_ID;
  const [subject, setSubject] = useState<Subject | null>(null);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getSubjects()
      .then((subjects) => setSubject(subjects.find((item) => item.Name.toLowerCase() === decodeURIComponent(subjectName).toLowerCase()) ?? null))
      .catch(() => setSubject(null));
  }, [subjectName]);

  async function handleSubmit() {
    if (!subject || !name.trim()) return;
    try {
      await createStructuredLearningTopic(tenantId, subject.SubjectId, name.trim(), description.trim() || null);
      navigate(`/structured-learning-topics/${encodeURIComponent(subject.Name)}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }

  if (!subject) {
    return <div className="mx-auto max-w-3xl px-4 py-10 text-sm text-slate-400">Dominio no encontrado.</div>;
  }

  return (
    <div className="mx-auto max-w-2xl px-4 py-10 sm:px-6">
      <Link to={`/structured-learning-topics/${encodeURIComponent(subject.Name)}`} className="inline-flex items-center gap-1.5 text-sm text-slate-400 hover:text-white">
        <ArrowLeft className="h-4 w-4" /> Volver a {subject.Name}
      </Link>
      <div className="mt-8 rounded-2xl border border-white/10 bg-white/[0.03] p-6">
        <p className="text-xs font-semibold uppercase tracking-[0.16em] text-emerald-400">{subject.Name}</p>
        <h1 className="mt-2 text-2xl font-bold text-white">Agregar tipo</h1>
        <p className="mt-2 text-sm text-slate-400">Crea una familia de documentos dentro de este dominio. Su descripción será global y estará disponible al seleccionar el tipo.</p>
        <div className="mt-6 space-y-4">
          <label className="block text-sm text-slate-300">
            Nombre del tipo
            <input value={name} onChange={(event) => setName(event.target.value)} placeholder="Ley del IVA" className="mt-1.5 w-full rounded-lg border border-white/10 bg-slate-950/50 px-3 py-2.5 text-sm text-white placeholder:text-slate-600" />
          </label>
          <label className="block text-sm text-slate-300">
            Descripción global
            <textarea value={description} onChange={(event) => setDescription(event.target.value)} placeholder="Documentos fiscales relacionados con el IVA en México." rows={5} className="mt-1.5 w-full rounded-lg border border-white/10 bg-slate-950/50 px-3 py-2.5 text-sm text-white placeholder:text-slate-600" />
          </label>
          {error && <p className="text-sm text-red-400">{error}</p>}
          <button type="button" onClick={handleSubmit} disabled={!name.trim()} className="flex items-center gap-2 rounded-xl bg-emerald-500 px-4 py-2.5 text-sm font-semibold text-white hover:bg-emerald-400 disabled:cursor-not-allowed disabled:opacity-40">
            <Check className="h-4 w-4" /> Guardar tipo
          </button>
        </div>
      </div>
    </div>
  );
}
