// Domain types (shared across app)

export type HumanEvolutionLevel = 'Foundation' | 'Exploration' | 'Mastery'

export type CapabilityMetric = 
  | 'Knowledge'
  | 'Recall'
  | 'Application'
  | 'Confidence'
  | 'Independence'
  | 'Retention'
  | 'Fluency'

export interface Capability {
  id: string
  name: string
  description: string
  domain: string
  levels: {
    level: HumanEvolutionLevel
    progress: number
    modulesCompleted: number
  }[]
  metrics: {
    metric: CapabilityMetric
    status: 'Verified' | 'InProgress' | 'NotStarted'
  }[]
}

export interface Session {
  id: string
  capabilityId: string
  capabilityName: string
  currentStep: number
  totalSteps: number
  targetMetric: CapabilityMetric
  status: 'Active' | 'Completed' | 'RequiresRevision'
  module: {
    id: string
    title: string
    description: string
    script: string
  }
}

export interface RuntimeStep {
  stepNumber: number
  stepType: 'Recall' | 'InitialAnswer' | 'Instruction' | 'Evidence' | 'Assessment' | 'Reflection'
  instruction: string
  userInput?: string
  status: 'NotStarted' | 'InProgress' | 'Completed'
}

export interface Evidence {
  id: string
  capabilityId: string
  moduleName: string
  type: string
  content: string
  createdAt: string
  verificationStatus: 'Verified' | 'Pending' | 'Rejected'
  targetMetric: CapabilityMetric
}
