import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { DeclaredExperienceItem, JobDescription, ResumeDocument, ResumeUploadStatus } from '../types';

interface RoleExperienceState {
  /** An employee-authored working draft, used when no official
   *  organization job description exists (see MissingJobDescription). */
  employeeProvidedJobDescription: JobDescription | null;
  jobDescriptionFeedback: 'reflects' | 'different' | null;

  resumeUploadStatus: ResumeUploadStatus;
  resumeDocument: ResumeDocument | null;
  declaredExperience: DeclaredExperienceItem[];
  resumeErrorMessage: string | null;

  setEmployeeProvidedJobDescription: (description: JobDescription) => void;
  setJobDescriptionFeedback: (feedback: 'reflects' | 'different') => void;
  setResumeUploadStatus: (status: ResumeUploadStatus) => void;
  setResumeDocument: (document: ResumeDocument | null) => void;
  setDeclaredExperience: (items: DeclaredExperienceItem[]) => void;
  setResumeErrorMessage: (message: string | null) => void;
  resetResume: () => void;
}

export const useRoleExperienceStore = create<RoleExperienceState>()(
  persist(
    (set) => ({
      employeeProvidedJobDescription: null,
      jobDescriptionFeedback: null,
      resumeUploadStatus: 'idle',
      resumeDocument: null,
      declaredExperience: [],
      resumeErrorMessage: null,

      setEmployeeProvidedJobDescription: (description) => set({ employeeProvidedJobDescription: description }),
      setJobDescriptionFeedback: (feedback) => set({ jobDescriptionFeedback: feedback }),
      setResumeUploadStatus: (status) => set({ resumeUploadStatus: status }),
      setResumeDocument: (document) => set({ resumeDocument: document }),
      setDeclaredExperience: (items) => set({ declaredExperience: items }),
      setResumeErrorMessage: (message) => set({ resumeErrorMessage: message }),
      resetResume: () =>
        set({
          resumeUploadStatus: 'idle',
          resumeDocument: null,
          declaredExperience: [],
          resumeErrorMessage: null,
        }),
    }),
    { name: 'human-os-role-experience' },
  ),
);
