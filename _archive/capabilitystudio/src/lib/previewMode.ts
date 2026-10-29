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

export const PREVIEW_MODE_OPTIONS: { value: PreviewMode; labelKey: string; descriptionKey: string }[] = [
  { value: 'real', labelKey: 'previewMode.real', descriptionKey: 'previewMode.realDescription' },
  { value: 'demo', labelKey: 'previewMode.demo', descriptionKey: 'previewMode.demoDescription' },
  { value: 'edit', labelKey: 'previewMode.edit', descriptionKey: 'previewMode.editDescription' },
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

/**
 * Dev/demo-only "Aprobación automática (temporal)" toggle (2026-08-02) —
 * Capability Studio ONLY, never present in the real student app. When on,
 * PreviewNodePage shows an "Aprobar (temporal)" button next to gated steps
 * (DeepDive/Recall/Production/Assessment) that calls
 * StudioForceAdvanceStepFunction instead of the real submit endpoints, so a
 * demo/pilot walkthrough can skip past them without answering. Persisted as
 * its own `autoApprove=1` query param (same pattern as `mode` above) so it
 * survives navigation within a preview session but never leaks anywhere
 * else. The backend endpoint itself is ALSO gated behind its own
 * `AllowStudioAutoApprove` app setting — this toggle only controls whether
 * the UI shows the button, it is not itself a security boundary.
 */
export function useAutoApprove(): [boolean, (next: boolean) => void] {
  const [searchParams, setSearchParams] = useSearchParams();
  const autoApprove = searchParams.get('autoApprove') === '1';

  const setAutoApprove = (next: boolean) => {
    const params = new URLSearchParams(searchParams);
    if (next) {
      params.set('autoApprove', '1');
    } else {
      params.delete('autoApprove');
    }
    setSearchParams(params, { replace: true });
  };

  return [autoApprove, setAutoApprove];
}

