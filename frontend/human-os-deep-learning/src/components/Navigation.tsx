import { useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { useTranslation } from '@i18n/translations'

const ICONS: Record<string, string> = {
  '/': '🏠',
  '/capabilities': '🧬',
  '/sessions': '⚡',
  '/evidence': '🏆',
  '/settings': '⚙️',
}

export default function Navigation() {
  const location = useLocation()
  const t = useTranslation('common')
  const [collapsed, setCollapsed] = useState(false)

  const navItems = [
    { path: '/', label: t.navigation?.home || '🏠 Home' },
    { path: '/capabilities', label: t.navigation?.capabilities || '🧬 Capabilities' },
    { path: '/sessions', label: t.navigation?.sessions || '⚡ Sessions' },
    { path: '/evidence', label: t.navigation?.evidence || '🏆 Evidence' },
    { path: '/settings', label: t.navigation?.settings || '⚙️ Settings' },
  ]

  return (
    <nav
      className={`shrink-0 bg-gray-50 border-r border-gray-200 p-4 transition-all duration-200 ${
        collapsed ? 'w-16' : 'w-64'
      }`}
    >
      <div className={`mb-8 flex items-center ${collapsed ? 'justify-center' : 'justify-between'}`}>
        {!collapsed && (
          <div>
            <h1 className="text-xl font-bold text-gray-900">Human OS</h1>
            <p className="text-sm text-gray-500">Deep Learning</p>
          </div>
        )}
        <button
          onClick={() => setCollapsed((c) => !c)}
          title={collapsed ? 'Expandir menú' : 'Colapsar menú'}
          className="p-2 rounded-lg text-gray-500 hover:bg-gray-200 transition-colors"
        >
          ☰
        </button>
      </div>

      <ul className="space-y-2">
        {navItems.map((item) => (
          <li key={item.path}>
            <Link
              to={item.path}
              title={collapsed ? item.label : undefined}
              className={`flex items-center rounded-lg transition ${
                collapsed ? 'justify-center px-2 py-2' : 'px-4 py-2'
              } ${
                location.pathname === item.path
                  ? 'bg-blue-100 text-blue-700 font-medium'
                  : 'text-gray-700 hover:bg-gray-100'
              }`}
            >
              {collapsed ? ICONS[item.path] ?? item.label.charAt(0) : item.label}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  )
}
