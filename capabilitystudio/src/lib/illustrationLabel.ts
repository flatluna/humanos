import type { TFunction } from 'i18next';

/**
 * Must match the backend's `DeterministicModelTag` constant (see
 * RuntimeGraphApiMappers/TutorApiMappers/BlueprintReviewApiMappers in
 * backend/HumanOS/AzureFunctions/Api/*.cs).
 */
const DETERMINISTIC_MODEL_TAG = 'deterministic-skiasharp-v1';

/**
 * Computes the small "Generated with AI (model: ...)" provenance caption
 * shown under illustrations in Capability Studio's preview tools, using
 * the CURRENT Studio UI language (`t`) — not the backend's hardcoded
 * Spanish string. This is Studio-only reviewer tooling text (never shown
 * in the actual student app), so it should follow the reviewer's own
 * ES/EN toggle rather than the capability's ContentLanguageCode.
 * Ignores the backend's own `GeneratedByLabel` field entirely; only
 * `imageModel` is used.
 */
export function getGeneratedByLabel(imageModel: string, t: TFunction): string {
  return imageModel === DETERMINISTIC_MODEL_TAG
    ? t('previewNode.generatedDeterministic')
    : t('previewNode.generatedByAi', { model: imageModel || '—' });
}
