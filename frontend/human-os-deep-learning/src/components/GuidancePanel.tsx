import { useState } from 'react'

interface GuidancePanelProps {
  title: string
  content: string
}

export default function GuidancePanel({ title, content }: GuidancePanelProps) {
  const [isExpanded, setIsExpanded] = useState(false)

  return (
    <div className="mt-6 border-t border-gray-200 pt-4">
      <button
        onClick={() => setIsExpanded(!isExpanded)}
        className="flex items-center gap-2 text-gray-600 hover:text-gray-900 font-medium"
      >
        <span>{isExpanded ? '↑' : '↓'}</span>
        {title}
      </button>
      
      {isExpanded && (
        <div className="mt-4 p-4 bg-gray-50 rounded-lg border border-gray-200">
          <p className="text-gray-700 whitespace-pre-wrap">{content}</p>
        </div>
      )}
    </div>
  )
}
