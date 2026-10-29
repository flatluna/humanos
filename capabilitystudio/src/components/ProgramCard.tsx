import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Layers, GraduationCap, ImageOff, Check, Square } from 'lucide-react';
import type { BackendProgram } from '../lib/api/programsApi';
import { apiImageUrl } from '../lib/api/httpClient';

export default function ProgramCard({
  program,
  selected,
  onToggleSelect,
}: {
  program: BackendProgram;
  selected: boolean;
  onToggleSelect: () => void;
}) {
  const { t } = useTranslation();

  return (
    <Link
      to={`/programs/${program.ProgramId}`}
      className="group relative flex flex-col overflow-hidden rounded-3xl border border-white/10 bg-white/[0.03] transition-all hover:-translate-y-1 hover:border-white/20 hover:bg-white/[0.06] hover:shadow-2xl hover:shadow-brand-500/10"
    >
      <button
        type="button"
        aria-label={selected ? 'Deselect program' : 'Select program'}
        onClick={(event) => {
          event.preventDefault();
          event.stopPropagation();
          onToggleSelect();
        }}
        className={`absolute right-3 top-3 z-10 flex h-8 w-8 items-center justify-center rounded-full border transition-all ${
          selected
            ? 'border-brand-400 bg-brand-500 text-white shadow-lg shadow-brand-500/30'
            : 'border-white/20 bg-slate-950/70 text-slate-300 hover:border-white/40 hover:text-white'
        }`}
      >
        {selected ? <Check className="h-4 w-4" /> : <Square className="h-4 w-4" />}
      </button>

      <div className="relative h-36 w-full overflow-hidden bg-gradient-to-br from-violet-500/30 to-fuchsia-500/20">
        {program.HasLogo ? (
          <img
            src={apiImageUrl(`/programs/${program.ProgramId}/logo`)}
            alt=""
            className="h-full w-full object-contain object-center opacity-90 transition-transform duration-500 group-hover:scale-105"
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center">
            <GraduationCap className="h-12 w-12 text-white/70" strokeWidth={1.5} />
          </div>
        )}
        <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-transparent to-transparent" />
      </div>

      <div className="flex flex-1 flex-col gap-2 p-5">
        <h3 className="font-semibold text-white leading-snug line-clamp-2">{program.Name}</h3>
        {program.Description && <p className="text-sm text-slate-400 line-clamp-2">{program.Description}</p>}

        <div className="mt-auto flex items-center gap-4 pt-3 text-xs text-slate-500">
          <span className="flex items-center gap-1.5">
            <Layers className="h-3.5 w-3.5" />
            {t('programCard.capabilities', { count: program.CapabilityCount })}
          </span>
          {!program.IsActive && (
            <span className="ml-auto flex items-center gap-1 text-amber-400">
              <ImageOff className="h-3.5 w-3.5" />
              {t('programCard.inactive')}
            </span>
          )}
        </div>
      </div>
    </Link>
  );
}
