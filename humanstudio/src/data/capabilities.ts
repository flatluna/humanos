import { Capability } from '../types/capability'

export const capabilities: Capability[] = [
  {
    id: '1',
    name: 'Natural Language Processing',
    description: 'Process and analyze human language',
    category: 'AI',
    domain: 'Language',
    status: 'active',
    createdAt: new Date(),
    updatedAt: new Date()
  },
  {
    id: '2',
    name: 'Computer Vision',
    description: 'Visual recognition and analysis',
    category: 'AI',
    domain: 'Vision',
    status: 'active',
    createdAt: new Date(),
    updatedAt: new Date()
  }
]
