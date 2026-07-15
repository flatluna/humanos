import { useMutation, useQuery } from '@tanstack/react-query';
import { getJobDescription, submitJobDescriptionFeedback } from '../services/jobDescriptionService';
import { uploadResume, simulateResumeExtraction } from '../services/professionalProfileService';
import { useRoleExperienceStore } from '../store/useRoleExperienceStore';
import type { JobDescription } from '../types';

export function useJobDescription() {
  const employeeProvidedJobDescription = useRoleExperienceStore((state) => state.employeeProvidedJobDescription);

  const query = useQuery({
    queryKey: ['job-description'],
    queryFn: getJobDescription,
  });

  // The organization-provided job description always takes precedence;
  // fall back to the employee's own working draft if one was created.
  const data: JobDescription | null | undefined = query.data ?? employeeProvidedJobDescription;

  return { ...query, data };
}

export function useSubmitJobDescriptionFeedback() {
  const setJobDescriptionFeedback = useRoleExperienceStore((state) => state.setJobDescriptionFeedback);

  return useMutation({
    mutationFn: (feedback: { type: 'reflects' | 'different'; note?: string }) =>
      submitJobDescriptionFeedback(feedback),
    onSuccess: (_data, variables) => setJobDescriptionFeedback(variables.type),
  });
}

export function useResumeUpload() {
  const setResumeUploadStatus = useRoleExperienceStore((state) => state.setResumeUploadStatus);
  const setResumeDocument = useRoleExperienceStore((state) => state.setResumeDocument);
  const setDeclaredExperience = useRoleExperienceStore((state) => state.setDeclaredExperience);
  const setResumeErrorMessage = useRoleExperienceStore((state) => state.setResumeErrorMessage);

  return useMutation({
    mutationFn: async (file: File) => {
      setResumeUploadStatus('uploading');
      setResumeErrorMessage(null);

      const document = await uploadResume(file);
      setResumeDocument(document);

      setResumeUploadStatus('processing');
      const items = await simulateResumeExtraction();
      setDeclaredExperience(items);
      setResumeUploadStatus('extracted');

      return document;
    },
    onError: (error: unknown) => {
      setResumeUploadStatus('error');
      setResumeErrorMessage(error instanceof Error ? error.message : 'Unknown error');
    },
  });
}
