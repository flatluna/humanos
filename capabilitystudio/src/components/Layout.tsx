import { useRef, useState, useEffect, type ReactNode } from 'react';
import { Link, NavLink, Outlet } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Plus, LayoutGrid, GraduationCap, Coins, LogOut, ArrowLeft, FileText, ChevronDown, Menu, X, Tags, Bot, UserCircle2, Clapperboard, Sparkles, Network, FlaskConical, Video } from 'lucide-react';
import ActiveRunBanner from './ActiveRunBanner';
import ThemeSwitcher from './ThemeSwitcher';
import LanguageSwitcher from './LanguageSwitcher';
import { useAuth } from '../auth/AuthContext';

// Studio is opened from human-os-web/human-os-enterprise (usually in a new
// tab), but users who land here directly (bookmark, typed URL, refresh after
// closing the origin tab) have no way back without this. Only Consumer and
// HRAdmin personas reach Studio (canAuthorCourses gate), so the origin app
// is one of these two depending on whether the account belongs to a tenant.
const HUMAN_OS_WEB_URL = import.meta.env.VITE_HUMAN_OS_WEB_URL ?? 'http://localhost:5178';
const HUMAN_OS_ENTERPRISE_URL = import.meta.env.VITE_HUMAN_OS_ENTERPRISE_URL ?? 'http://localhost:5179';

export default function Layout() {
  const { t } = useTranslation();
  const { user, logout } = useAuth();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [identityModalOpen, setIdentityModalOpen] = useState(false);
  const inheritedTenantId = sessionStorage.getItem('humanos.studio.inheritedTenantId');
  const inheritedTenantName = sessionStorage.getItem('humanos.studio.inheritedTenantName');
  const backToMainAppUrl = user?.returnUrl ?? (user?.tenantId ? HUMAN_OS_ENTERPRISE_URL : HUMAN_OS_WEB_URL);
  const backToAppName = inheritedTenantName || user?.tenantName || user?.sourceApplication;
  const backToAppLabel = backToAppName ? t('nav.backToApp', { appName: backToAppName }) : t('nav.backToHumanOS');

  return (
    <div className="min-h-full flex flex-col bg-slate-950">
      <header className="sticky top-0 z-40 border-b border-white/10 bg-slate-950/90 backdrop-blur-xl">
        <div className="mx-auto flex max-w-[120rem] items-center justify-between gap-4 px-4 py-3 sm:px-6">
          <div className="flex items-center gap-3">
            {/* Mobile hamburger (opens drawer) */}
            <button
              type="button"
              onClick={() => setSidebarOpen((open) => !open)}
              className="rounded-lg p-2 text-slate-400 hover:bg-white/5 hover:text-white lg:hidden"
              aria-label={sidebarOpen ? 'Close navigation' : 'Open navigation'}
            >
              {sidebarOpen ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
            </button>

            {/* Desktop collapse toggle */}
            <button
              type="button"
              onClick={() => setSidebarCollapsed((collapsed) => !collapsed)}
              className="hidden rounded-lg p-2 text-slate-400 hover:bg-white/5 hover:text-white lg:block"
              aria-label={sidebarCollapsed ? 'Expand navigation' : 'Collapse navigation'}
              title={sidebarCollapsed ? 'Expand navigation' : 'Collapse navigation'}
            >
              <Menu className="h-5 w-5" />
            </button>

            <Link to="/" className="flex items-center gap-3.5 group">
              <img
                src="/HumanOsLogoBlack.png"
                alt="HUMAN OS"
                className="h-8 sm:h-9 w-auto object-contain object-left transition-transform group-hover:scale-105 drop-shadow-[0_0_12px_rgba(59,130,246,0.3)]"
              />
              <div className="leading-tight border-l border-white/15 pl-3">
                <div className="flex items-center gap-1.5 font-bold tracking-wider text-white text-xs sm:text-sm">
                  <span>STUDIO</span>
                  <Sparkles className="h-3.5 w-3.5 text-brand-400 drop-shadow-[0_0_8px_rgba(96,165,250,0.6)]" />
                </div>
                <div className="italic text-[11px] sm:text-xs font-semibold text-brand-400">Use AI to protect what is human from AI &mdash; Usa IA para proteger lo humano de la IA</div>
              </div>
            </Link>
          </div>

          <nav className="hidden">
            <NavLink
              to="/catalog"
              end
              className={({ isActive }) =>
                `flex items-center gap-1.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
                  isActive ? 'bg-white/10 text-white' : 'text-slate-400 hover:text-white hover:bg-white/5'
                }`
              }
            >
              <LayoutGrid className="h-4 w-4 text-sky-400" />
              {t('nav.catalog')}
            </NavLink>
            <NavLink
              to="/programs"
              className={({ isActive }) =>
                `flex items-center gap-1.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
                  isActive ? 'bg-white/10 text-white' : 'text-slate-400 hover:text-white hover:bg-white/5'
                }`
              }
            >
              <GraduationCap className="h-4 w-4 text-violet-400" />
              {t('nav.programs')}
            </NavLink>
            <NavLink
              to="/costs"
              className={({ isActive }) =>
                `flex items-center gap-1.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
                  isActive ? 'bg-white/10 text-white' : 'text-slate-400 hover:text-white hover:bg-white/5'
                }`
              }
            >
              <Coins className="h-4 w-4 text-amber-400" />
              {t('nav.costs')}
            </NavLink>
            <NavLink
              to="/structured-learning-topics"
              className={({ isActive }) =>
                `flex items-center gap-1.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
                  isActive ? 'bg-white/10 text-white' : 'text-slate-400 hover:text-white hover:bg-white/5'
                }`
              }
            >
              <LayoutGrid className="h-4 w-4 text-emerald-400" />
              {t('nav.structuredLearningTopics')}
            </NavLink>
          </nav>

          <div className="flex items-center gap-2">
            <a
              href={backToMainAppUrl}
              title={backToAppLabel}
              className="flex items-center gap-1.5 rounded-lg px-2 py-2 text-xs font-medium text-slate-400 transition-colors hover:bg-white/5 hover:text-white"
            >
              <ArrowLeft className="h-4 w-4" />
              <span className="hidden md:inline">{backToAppLabel}</span>
            </a>
            <LanguageSwitcher />
            <ThemeSwitcher />
            {user && (
              <button
                type="button"
                onClick={() => setIdentityModalOpen(true)}
                title={t('common.viewIdentityTitle')}
                className="flex items-center gap-1.5 rounded-lg px-2 py-2 text-violet-400 transition-colors hover:bg-violet-500/10 hover:text-violet-300"
              >
                <UserCircle2 className="h-5 w-5" />
              </button>
            )}
            {user && (
              <button
                type="button"
                onClick={() => void logout()}
                title={user.email}
                className="flex items-center gap-1.5 rounded-lg px-2 py-2 text-xs font-medium text-slate-400 transition-colors hover:bg-white/5 hover:text-white"
              >
                <span className="hidden md:inline max-w-[10rem] truncate">{user.email}</span>
                <LogOut className="h-4 w-4" />
              </button>
            )}
          </div>
        </div>
      </header>

      <aside className={`fixed inset-y-0 left-0 z-50 border-r border-white/10 bg-slate-950/95 px-4 py-5 backdrop-blur-xl transition-all duration-200 lg:top-[57px] lg:h-[calc(100vh-57px)] lg:translate-x-0 ${sidebarCollapsed ? 'w-20' : 'w-64'} ${sidebarOpen ? 'translate-x-0' : '-translate-x-full'}`}>
        <div className="flex items-center justify-between pb-3 lg:hidden">
          <div className="flex items-center gap-2.5">
            <img src="/HumanOsLogoBlack.png" alt="HUMAN OS" className="h-7 w-auto object-contain" />
            <div className="flex items-center gap-1.5 font-bold text-xs tracking-wider text-white border-l border-white/15 pl-2">
              <span>STUDIO</span>
              <Sparkles className="h-3 w-3 text-brand-400" />
            </div>
          </div>
          <button type="button" onClick={() => setSidebarOpen(false)} className="rounded-lg p-1.5 text-slate-400 hover:bg-white/5 hover:text-white">
            <X className="h-5 w-5" />
          </button>
        </div>

        <nav className="space-y-1">
          <SidebarLink collapsed={sidebarCollapsed} to="/catalog" end label={t('nav.catalog')} color="text-sky-400" activeColor="bg-sky-500/15" onNavigate={() => setSidebarOpen(false)}><LayoutGrid className="h-5 w-5" /></SidebarLink>
          <SidebarLink collapsed={sidebarCollapsed} to="/programs" label={t('nav.programs')} color="text-violet-400" activeColor="bg-violet-500/15" onNavigate={() => setSidebarOpen(false)}><GraduationCap className="h-5 w-5" /></SidebarLink>
          <SidebarLink collapsed={sidebarCollapsed} to="/program-entities" label={t('nav.programEntities')} color="text-amber-400" activeColor="bg-amber-500/15" onNavigate={() => setSidebarOpen(false)}><Network className="h-5 w-5" /></SidebarLink>
          <SidebarLink collapsed={sidebarCollapsed} to="/costs" label={t('nav.costs')} color="text-amber-400" activeColor="bg-amber-500/15" onNavigate={() => setSidebarOpen(false)}><Coins className="h-5 w-5" /></SidebarLink>
          <SidebarLink collapsed={sidebarCollapsed} to="/structured-learning-topics" label={t('nav.structuredLearningTopics')} color="text-emerald-400" activeColor="bg-emerald-500/15" onNavigate={() => setSidebarOpen(false)}><Tags className="h-5 w-5" /></SidebarLink>
          <SidebarLink collapsed={sidebarCollapsed} to="/ai-agents" label={t('nav.aiAgents')} color="text-cyan-400" activeColor="bg-cyan-500/15" onNavigate={() => setSidebarOpen(false)}><Bot className="h-5 w-5" /></SidebarLink>
          <SidebarLink collapsed={sidebarCollapsed} to="/film-productions" label={t('nav.filmProductions')} color="text-amber-400" activeColor="bg-amber-500/15" onNavigate={() => setSidebarOpen(false)}><Clapperboard className="h-5 w-5" /></SidebarLink>
          <SidebarLink collapsed={sidebarCollapsed} to="/video-productions" label={t('nav.videoProductions')} color="text-cyan-400" activeColor="bg-cyan-500/15" onNavigate={() => setSidebarOpen(false)}><Video className="h-5 w-5" /></SidebarLink>
          <SidebarLink collapsed={sidebarCollapsed} to="/labs" label={t('nav.labs')} color="text-teal-400" activeColor="bg-teal-500/15" onNavigate={() => setSidebarOpen(false)}><FlaskConical className="h-5 w-5" /></SidebarLink>
        </nav>
        <div className="mt-8 border-t border-white/10 pt-4">
          {!sidebarCollapsed && <p className="px-2 text-[10px] font-semibold uppercase tracking-[0.18em] text-slate-600">Create</p>}
          <div className="mt-2"><NewCapabilityMenu collapsed={sidebarCollapsed} /></div>
        </div>
      </aside>
      {sidebarOpen && <button type="button" aria-label="Close navigation" onClick={() => setSidebarOpen(false)} className="fixed inset-0 z-40 bg-black/50 lg:hidden" />}

      <ActiveRunBanner />

      <main className={`relative flex-1 transition-all duration-200 ${sidebarCollapsed ? 'lg:pl-20' : 'lg:pl-64'}`}>
        <div className="mx-auto max-w-[120rem]"><Outlet /></div>
      </main>

      <footer className="border-t border-white/10 py-6 text-center text-xs text-slate-500">
        {t('nav.footer')}
      </footer>

      {identityModalOpen && user && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 px-4" onClick={() => setIdentityModalOpen(false)}>
          <div className="w-full max-w-md rounded-2xl border border-white/10 bg-[#0f1a1a] p-6 shadow-2xl" onClick={(event) => event.stopPropagation()}>
            <div className="mb-4 flex items-center justify-between">
              <h2 className="text-sm font-semibold text-white">{t('common.identityModalTitle')}</h2>
              <button type="button" onClick={() => setIdentityModalOpen(false)} className="rounded-lg p-1 text-slate-400 hover:bg-white/5 hover:text-white" aria-label={t('common.close')}>
                <X className="h-4 w-4" />
              </button>
            </div>
            <dl className="space-y-3 text-sm">
              <div>
                <dt className="text-[11px] font-medium uppercase tracking-wider text-slate-500">{t('common.identityEmail')}</dt>
                <dd className="mt-0.5 break-all font-mono text-slate-200">{user.email}</dd>
              </div>
              <div>
                <dt className="text-[11px] font-medium uppercase tracking-wider text-slate-500">{t('common.identityOid')}</dt>
                <dd className="mt-0.5 break-all font-mono text-slate-200">{user.oid ?? '—'}</dd>
              </div>
              <div>
                <dt className="text-[11px] font-medium uppercase tracking-wider text-slate-500">{t('common.identityTenantId')}</dt>
                <dd className="mt-0.5 break-all font-mono text-slate-200">{inheritedTenantId ?? user.tenantId ?? '—'}</dd>
              </div>
              <div>
                <dt className="text-[11px] font-medium uppercase tracking-wider text-slate-500">{t('common.identityTenantCompany')}</dt>
                <dd className="mt-0.5 text-slate-200">{inheritedTenantName ?? user.tenantName ?? t('common.individualAccount')}</dd>
              </div>
              <div>
                <dt className="text-[11px] font-medium uppercase tracking-wider text-slate-500">{t('common.identityBackTo')}</dt>
                <dd className="mt-0.5 break-all font-mono text-slate-200">{backToMainAppUrl}</dd>
              </div>
            </dl>
          </div>
        </div>
      )}
    </div>
  );
}

function SidebarLink({
  to,
  end,
  label,
  collapsed,
  color,
  activeColor,
  onNavigate,
  children,
}: {
  to: string;
  end?: boolean;
  label: string;
  collapsed?: boolean;
  color: string;
  activeColor: string;
  onNavigate: () => void;
  children: ReactNode;
}) {
  return (
    <NavLink
      to={to}
      end={end}
      onClick={onNavigate}
      title={collapsed ? label : undefined}
      className={({ isActive }) => `group flex items-center rounded-xl px-3 py-3 text-sm font-medium transition-colors ${collapsed ? 'justify-center' : 'gap-3'} ${isActive ? `${activeColor} text-white` : 'text-slate-400 hover:bg-white/5 hover:text-white'}`}
    >
      <span className={`${color} transition-transform group-hover:scale-110`}>{children}</span>
      {!collapsed && <span>{label}</span>}
    </NavLink>
  );
}

/** "Nueva ruta de aprendizaje" CTA: only the modern Studio creation flows remain
 * visible here to avoid confusion with the legacy curator/blueprint pipeline. */
function NewCapabilityMenu({ collapsed = false }: { collapsed?: boolean }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  return (
    <div ref={containerRef} className="relative">
      <button
        type="button"
        onClick={() => setOpen((prev) => !prev)}
        title={collapsed ? t('nav.newCapability') : undefined}
        className={`flex items-center rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.03] active:scale-[0.98] ${collapsed ? 'mx-auto justify-center px-2.5' : 'gap-2 px-4'}`}
      >
        <Plus className="h-4 w-4" strokeWidth={2.5} />
        {!collapsed && <>{t('nav.newCapability')}<ChevronDown className={`h-3.5 w-3.5 transition-transform ${open ? 'rotate-180' : ''}`} /></>}
      </button>

      {open && (
        <div className="animate-fade-in absolute right-0 top-[calc(100%+0.5rem)] z-50 w-64 overflow-hidden rounded-2xl border border-white/10 bg-slate-900/95 p-1.5 shadow-2xl backdrop-blur-xl">
          <Link
            to="/new-structured-learning"
            onClick={() => setOpen(false)}
            className="flex items-start gap-3 rounded-xl px-3 py-2.5 text-left transition-colors hover:bg-white/5"
          >
            <div className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-emerald-500/15">
              <FileText className="h-4 w-4 text-emerald-400" />
            </div>
            <div>
              <div className="text-sm font-medium text-white">{t('nav.newStructuredLearning')}</div>
            </div>
          </Link>
        </div>
      )}
    </div>
  );
}
