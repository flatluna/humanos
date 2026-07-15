import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Sparkles } from 'lucide-react';
import { Card } from '@/components/ui/Card';
import type { JobDescription } from '../types';

interface MissingJobDescriptionProps {
  onSaveDraft: (description: JobDescription) => void;
}

/** Shown when Human OS cannot find an official job description. Lets the
 *  employee describe their work in their own words rather than blocking
 *  progress — the agent may later help organize this, but it must never
 *  invent responsibilities and present them as organizational fact.
 */
export function MissingJobDescription({ onSaveDraft }: MissingJobDescriptionProps) {
  const { t } = useTranslation();
  const [isDrafting, setIsDrafting] = useState(false);
  const [description, setDescription] = useState('');

  function handleSave() {
    if (!description.trim()) {
      return;
    }

    onSaveDraft({
      jobTitle: { en: 'Working Description', es: 'Descripción de Trabajo' },
      rolePurpose: { en: description.trim(), es: description.trim() },
      roleSummary: { en: description.trim(), es: description.trim() },
      primaryResponsibilities: [],
      expectedOutcomes: [],
      coreCapabilities: [],
      requiredKnowledge: [],
      toolsAndTechnologies: [],
      applicablePolicies: [],
      regulatoryRequirements: [],
      expectedExperience: null,
      source: 'employeeProvided',
      organizationOwner: null,
      version: null,
      lastUpdatedDate: new Date().toISOString(),
      verificationStatus: 'unverified',
    });
  }

  return (
    <Card className="p-8 text-center sm:p-10">
      <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-gradient-to-br from-blue-500/10 to-violet-500/10 text-blue-600 dark:text-blue-300">
        <Sparkles className="h-6 w-6" />
      </div>

      <p className="mt-4 font-semibold text-slate-900 dark:text-white">
        {t('growthPlan.roleExperience.missingJobDescription.heading')}
      </p>
      <p className="mx-auto mt-2 max-w-md text-sm text-slate-500 dark:text-white/50">
        {t('growthPlan.roleExperience.missingJobDescription.message')}
      </p>

      {!isDrafting ? (
        <button
          type="button"
          onClick={() => setIsDrafting(true)}
          className="mt-6 inline-flex min-h-11 items-center justify-center rounded-full bg-slate-900 px-6 py-2.5 text-sm font-medium text-white transition hover:bg-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
        >
          {t('growthPlan.roleExperience.missingJobDescription.startDraft')}
        </button>
      ) : (
        <div className="mx-auto mt-6 max-w-md text-left">
          <label className="block">
            <span className="text-sm font-medium text-slate-700 dark:text-white/80">
              {t('growthPlan.roleExperience.missingJobDescription.describeWhatYouDoLabel')}
            </span>
            <textarea
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              rows={4}
              autoFocus
              className="mt-2 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
            />
          </label>

          <button
            type="button"
            onClick={handleSave}
            disabled={!description.trim()}
            className="mt-4 inline-flex min-h-11 w-full items-center justify-center rounded-full bg-slate-900 px-6 py-2.5 text-sm font-medium text-white transition hover:bg-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
          >
            {t('growthPlan.roleExperience.missingJobDescription.saveDraft')}
          </button>
        </div>
      )}
    </Card>
  );
}
