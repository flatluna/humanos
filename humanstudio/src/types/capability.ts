export interface Capability {
  id: string
  name: string
  description: string
  category: string
  domain: string
  status: 'active' | 'inactive' | 'pending'
  createdAt: Date
  updatedAt: Date
}

export interface CapabilityFormData {
  name: string
  description: string
  category: string
  domain: string
}
