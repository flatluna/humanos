import { useSearchParams } from 'react-router-dom';

/**
 * The 3 "Probar como estudiante" modes (2026-07-21):
 * - real: exactly what the student sees — nodes/steps gated normally.
 * - demo: for internal review/QA — every node on the graph map is
 *   clickable regardless of its Locked/Available/Mastered state, so the
 *   team can inspect/approve any AI-generated node before it's ever
 *   published. Purely a client-side bypass (same pattern already proven
 *   in humanlearn/CapabilityGraphMapPage's `reviewMode`) — the backend
 *   never enforces node-unlock rules when starting a session, so this is
 *   safe and 100% reversible, no backend changes needed.
 * - edit: lets a reviewer tweak a node's AI-generated content via a
 *   prompt next to its illustration. NOT YET IMPLEMENTED — needs a new
 *   backend endpoint to regenerate a NodeExperienceBlueprintStep's
 *   Content/illustration from a reviewer instruction. See
 *   /memories/repo/studio-three-preview-modes-plan.md.
 */
export type PreviewMode = 'real' | 'demo' | 'edit';

export const PREVIEW_MODE_OPTIONS: { value: PreviewMode; label: string; description: string }[] = [
  { value: 'real', label: 'Real', description: 'Como lo ve el estudiante — con candados' },
  { value: 'demo', label: 'Demo', description: 'Sin candados, para revisar y aprobar todo' },
  { value: 'edit', label: 'Edición', description: 'Editar contenido con IA (próximamente)' },
];

/** Reads/writes the `mode` query param shared across all `/preview` routes. */
export function usePreviewMode(): [PreviewMode, (mode: PreviewMode) => void] {
  const [searchParams, setSearchParams] = useSearchParams();
  const raw = searchParams.get('mode');
  const mode: PreviewMode = raw === 'demo' || raw === 'edit' ? raw : 'real';

  const setMode = (next: PreviewMode) => {
    const params = new URLSearchParams(searchParams);
    if (next === 'real') {
      params.delete('mode');
    } else {
      params.set('mode', next);
    }
    setSearchParams(params, { replace: true });
  };

  return [mode, setMode];
}

/** Appends the current mode to a preview URL (e.g. when navigating from the graph map into a node) so it isn't lost. */
export function withPreviewMode(path: string, mode: PreviewMode): string {
  return mode === 'real' ? path : `${path}?mode=${mode}`;
}
