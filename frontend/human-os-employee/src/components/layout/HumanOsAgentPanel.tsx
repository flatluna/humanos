import { useEffect } from 'react';
import { X } from 'lucide-react';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { AiAgentCoursePage } from '@/features/ai-agents/AiAgentCoursePage';
import { useHumanOsAgentPanelStore } from '@/store/useHumanOsAgentPanelStore';

const HUMAN_OS_AGENT_DOCUMENT_ID = '9e6fb348-c614-41d1-820c-8f792ea3ead9';

/**
 * Global "Human OS" agent slide-in panel (2026-09-24). The launcher button
 * itself lives centered in EmployeeHeader (see useHumanOsAgentPanelStore);
 * this component only owns the backdrop + panel, mounted once in
 * EmployeeAppLayout so it slides in from the right over the current page.
 * Kept as a real panel (not an iframe) so it reuses AiAgentCoursePage
 * directly without double-rendering the app shell inside itself.
 */
export function HumanOsAgentPanel() {
  const isOpen = useHumanOsAgentPanelStore((state) => state.isOpen);
  const close = useHumanOsAgentPanelStore((state) => state.close);
  const { language } = useI18n();
  const isEn = language === 'en';

  useEffect(() => {
    if (!isOpen) return;
    const closeOnEscape = (event: KeyboardEvent) => { if (event.key === 'Escape') close(); };
    document.addEventListener('keydown', closeOnEscape);
    document.body.style.overflow = 'hidden';
    return () => { document.removeEventListener('keydown', closeOnEscape); document.body.style.overflow = ''; };
  }, [isOpen, close]);

  return (
    <>
      {/* Backdrop */}
      <div
        aria-hidden={!isOpen}
        onClick={close}
        className={`fixed inset-0 z-[64] bg-slate-950/50 backdrop-blur-sm transition-opacity duration-300 ${
          isOpen ? 'opacity-100' : 'pointer-events-none opacity-0'
        }`}
      />

      {/* Slide-in panel */}
      <div
        role="dialog"
        aria-modal="true"
        aria-label={isEn ? 'Human OS agent' : 'Agente de Human OS'}
        className={`fixed inset-y-0 right-0 z-[65] flex w-full max-w-md transform flex-col bg-white shadow-2xl transition-transform duration-300 ease-out dark:bg-slate-950 ${
          isOpen ? 'translate-x-0' : 'translate-x-full'
        }`}
      >
        <div className="flex shrink-0 items-center justify-between border-b border-slate-200 px-4 py-3 dark:border-white/10">
          <div className="flex items-center gap-2">
            <img src="/HumanOSLogo.png" alt="Human OS" className="h-7 w-7 object-contain" />
            <span className="text-sm font-semibold text-slate-900 dark:text-white">Human OS</span>
          </div>
          <button
            type="button"
            onClick={close}
            aria-label={isEn ? 'Close' : 'Cerrar'}
            className="rounded-full p-1.5 text-slate-500 hover:bg-slate-100 hover:text-slate-900 dark:text-white/60 dark:hover:bg-white/10 dark:hover:text-white"
          >
            <X className="h-5 w-5" />
          </button>
        </div>
        <div className="min-h-0 flex-1 overflow-hidden p-4">
          {isOpen && <AiAgentCoursePage documentIdOverride={HUMAN_OS_AGENT_DOCUMENT_ID} compact />}
        </div>
      </div>
    </>
  );
}
