export interface FamilyMasterRecord {
  familyId: string;
  familyName: string;
  familySlug: string;
  householdType: string;
  adminFullName: string;
  adminEmail: string;
  adminPhone: string | null;
  coAdminFullName: string | null;
  coAdminEmail: string | null;
  primaryStreet: string | null;
  primaryCity: string | null;
  primaryState: string | null;
  primaryZipCode: string | null;
  primaryCountry: string;
  primaryFullAddress?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  secondaryLocation: string | null;
  timeZone: string;
  preferredCurrency: string;
  primaryLanguage: string;
  isBilingualHousehold: boolean;
  annualEducationBudget: number | null;
  digitalSunsetTime: string | null;
  familyMissionStatement: string | null;
  subscriptionTier: string;
  isActive: boolean;
  createdDate: string;
  updatedDate: string;
}

export interface FamilyMemberRecord {
  memberId: string;
  fullName: string;
  preferredName: string;
  familyRole: string;
  roleTitleEs: string;
  roleTitleEn: string;
  dateOfBirth: string | null;
  age: number | null;
  educationLevelEs: string | null;
  educationLevelEn: string | null;
  institution: string | null;
  primaryLocation: string | null;
  primaryStreet?: string | null;
  primaryCity?: string | null;
  primaryState?: string | null;
  primaryZipCode?: string | null;
  primaryCountry?: string | null;
  primaryFullAddress?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  email: string | null;
  phoneNumber?: string | null;
  phoneCountryCode?: string | null;
  azureOid?: string | null;
  azureLoginEmail?: string | null;
  azureLinkedDate?: string | null;
  platformRole?: 'AdminEngrams' | 'AdminFamily' | 'User';
  avatarBg: string;
  languages: string[];
  topFocus: string[];
  weeklyHoursTarget: number;
  learningStreakDays: number;
  targetMilestoneEs: string | null;
  targetMilestoneEn: string | null;
  isActive: boolean;
}

export interface FamilyMemberEducationRecord {
  educationId: string;
  tenantId: string;
  familyId: string;
  memberId: string;
  educationLevel: string; // e.g. "Primaria", "Secundaria", "Preparatoria", "Universidad", "Posgrado"
  schoolName: string;
  gradeOrDegree: string | null;
  fieldOfStudy: string | null;
  startYear: number | null;
  endYear: number | null;
  isCurrent: boolean;
  city: string | null;
  state?: string | null;
  country: string | null;
  gpa?: string | null;
  degree?: string | null;
  status?: string | null;
  notes: string | null;
  isActive: boolean;
  createdDate?: string;
  updatedDate?: string;
}

export interface MemberDetailResponse {
  member: FamilyMemberRecord;
  educations: FamilyMemberEducationRecord[];
}

export interface FamilyMasterData {
  family: FamilyMasterRecord;
  members: FamilyMemberRecord[];
}

export interface AddressSuggestion {
  address: string;
  streetName?: string | null;
  streetNumber?: string | null;
  city?: string | null;
  region?: string | null;
  countryCode?: string | null;
  postalCode?: string | null;
  entityId?: string | null;
  latitude?: number | null;
  longitude?: number | null;
}

export const familyApi = {
  async getFamilyMasterData(): Promise<FamilyMasterData> {
    const response = await fetch('/api/family/master-data', {
      cache: 'no-store',
      headers: { Accept: 'application/json' },
    });

    if (!response.ok) {
      const message = await response.text();
      throw new Error(message || `Unable to load family master data (${response.status}).`);
    }

    return response.json() as Promise<FamilyMasterData>;
  },

  async updateFamilyMasterData(payload: Partial<FamilyMasterRecord>): Promise<FamilyMasterData> {
    const response = await fetch('/api/family/master-data', {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
      },
      body: JSON.stringify(payload),
    });

    if (!response.ok) {
      const message = await response.text();
      throw new Error(message || `Unable to update family master data (${response.status}).`);
    }

    return response.json() as Promise<FamilyMasterData>;
  },

  async searchAddresses(query: string): Promise<AddressSuggestion[]> {
    if (!query || query.trim().length < 3) return [];
    const response = await fetch(`/api/addresses/search?query=${encodeURIComponent(query.trim())}`, {
      headers: { Accept: 'application/json' },
    });
    if (!response.ok) return [];
    const data = await response.json();
    if (Array.isArray(data)) return data;
    if (data && Array.isArray((data as { value: AddressSuggestion[] }).value)) {
      return (data as { value: AddressSuggestion[] }).value;
    }
    return [];
  },

  async getMember(memberId: string): Promise<MemberDetailResponse> {
    const response = await fetch(`/api/family/members/${memberId}`, {
      cache: 'no-store',
      headers: { Accept: 'application/json' },
    });

    if (!response.ok) {
      const message = await response.text();
      throw new Error(message || `Unable to load member profile (${response.status}).`);
    }

    return response.json() as Promise<MemberDetailResponse>;
  },

  async createMember(payload: Partial<FamilyMemberRecord>): Promise<FamilyMemberRecord> {
    const response = await fetch('/api/family/members', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
      },
      body: JSON.stringify(payload),
    });

    if (!response.ok) {
      const message = await response.text();
      throw new Error(message || `Unable to create member profile (${response.status}).`);
    }

    return response.json() as Promise<FamilyMemberRecord>;
  },

  async updateMember(memberId: string, payload: Partial<FamilyMemberRecord>): Promise<FamilyMemberRecord> {
    const response = await fetch(`/api/family/members/${memberId}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
      },
      body: JSON.stringify(payload),
    });

    if (!response.ok) {
      const message = await response.text();
      throw new Error(message || `Unable to update member profile (${response.status}).`);
    }

    return response.json() as Promise<FamilyMemberRecord>;
  },

  /** Soft-deletes a family member (sets IsActive = false server-side). */
  async deleteMember(memberId: string): Promise<void> {
    const response = await fetch(`/api/family/members/${memberId}`, { method: 'DELETE' });
    if (!response.ok) {
      const message = await response.text();
      throw new Error(message || `Unable to delete member (${response.status}).`);
    }
  },

  /**
   * Links the real MSAL (Entra CIAM) account currently signed in to this
   * member record: saves the Azure object id (azureOid) together with the
   * actual email/UPN the account was created with (azureLoginEmail) — which
   * can differ from the member's household Email, e.g. demo accounts reused
   * across business use cases. Backend enforces the 12+ age policy.
   */
  async linkMemberAccount(
    memberId: string,
    payload: { azureOid: string; azureLoginEmail: string },
  ): Promise<{ memberId: string; azureOid: string; azureLoginEmail: string; azureLinkedDate: string }> {
    const response = await fetch(`/api/family/members/${memberId}/link-account`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
      },
      body: JSON.stringify(payload),
    });

    if (!response.ok) {
      const message = await response.text();
      throw new Error(message || `Unable to link MSAL account (${response.status}).`);
    }

    return response.json();
  },

  async getMemberEducations(memberId: string): Promise<FamilyMemberEducationRecord[]> {
    const response = await fetch(`/api/family/members/${memberId}/education`, {
      cache: 'no-store',
      headers: { Accept: 'application/json' },
    });

    if (!response.ok) {
      const message = await response.text();
      throw new Error(message || `Unable to load education records (${response.status}).`);
    }

    return response.json() as Promise<FamilyMemberEducationRecord[]>;
  },

  async createMemberEducation(
    memberId: string,
    payload: Partial<FamilyMemberEducationRecord>
  ): Promise<FamilyMemberEducationRecord> {
    const response = await fetch(`/api/family/members/${memberId}/education`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
      },
      body: JSON.stringify(payload),
    });

    if (!response.ok) {
      const message = await response.text();
      throw new Error(message || `Unable to create education record (${response.status}).`);
    }

    return response.json() as Promise<FamilyMemberEducationRecord>;
  },

  async updateMemberEducation(
    memberId: string,
    educationId: string,
    payload: Partial<FamilyMemberEducationRecord>
  ): Promise<FamilyMemberEducationRecord> {
    const response = await fetch(`/api/family/members/${memberId}/education/${educationId}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
      },
      body: JSON.stringify(payload),
    });

    if (!response.ok) {
      const message = await response.text();
      throw new Error(message || `Unable to update education record (${response.status}).`);
    }

    return response.json() as Promise<FamilyMemberEducationRecord>;
  },

  async deleteMemberEducation(memberId: string, educationId: string): Promise<void> {
    const response = await fetch(`/api/family/members/${memberId}/education/${educationId}`, {
      method: 'DELETE',
      headers: { Accept: 'application/json' },
    });

    if (!response.ok) {
      const message = await response.text();
      throw new Error(message || `Unable to delete education record (${response.status}).`);
    }
  },
};
