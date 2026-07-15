import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type {
  EmployeeRoleContext,
  JobDescriptionEmployeeComment,
  JobDescriptionEmployeeCommentType,
  JobDescriptionReviewStatus,
  JobDescriptionSourceType,
} from '../types';

interface JobDescriptionSourceState {
  employeeContext: EmployeeRoleContext | null;
  comments: JobDescriptionEmployeeComment[];
  sourceType: JobDescriptionSourceType;
  reviewStatus: JobDescriptionReviewStatus;
  isConfirmed: boolean;
  confirmedDate: string | null;

  setEmployeeContext: (context: EmployeeRoleContext) => void;
  addComment: (type: JobDescriptionEmployeeCommentType, text: string) => void;
  setReviewStatus: (status: JobDescriptionReviewStatus) => void;
  requestOrganizationReview: () => void;
  confirmSource: () => void;
  reset: () => void;
}

const INITIAL_STATE: Pick<
  JobDescriptionSourceState,
  'employeeContext' | 'comments' | 'sourceType' | 'reviewStatus' | 'isConfirmed' | 'confirmedDate'
> = {
  employeeContext: null,
  comments: [],
  sourceType: 'employeeProvided',
  reviewStatus: 'notReviewed',
  isConfirmed: false,
  confirmedDate: null,
};

/** Local, client-side state for the Job Description Source screen. Kept
 *  separate from `useRoleExperienceStore` (which backs the existing
 *  inline Job Description section on `/growth-plan/role-experience`)
 *  since this screen introduces a richer, distinct review model
 *  (3-state review status, comments kept separately, explicit
 *  confirmation) rather than extending the older 2-state one.
 */
export const useJobDescriptionSourceStore = create<JobDescriptionSourceState>()(
  persist(
    (set) => ({
      ...INITIAL_STATE,

      setEmployeeContext: (context) => set({ employeeContext: context, sourceType: 'employeeProvided' }),

      addComment: (type, text) =>
        set((state) => ({
          comments: [
            ...state.comments,
            { id: `comment-${Date.now()}`, type, text, createdDate: new Date().toISOString() },
          ],
        })),

      setReviewStatus: (status) => set({ reviewStatus: status }),

      requestOrganizationReview: () => set({ sourceType: 'pendingOrganizationReview' }),

      confirmSource: () => set({ isConfirmed: true, confirmedDate: new Date().toISOString() }),

      reset: () => set({ ...INITIAL_STATE }),
    }),
    { name: 'human-os-job-description-source' },
  ),
);
