import { useEffect, useState, type CSSProperties, type ReactNode } from 'react';
import { Activity, BarChart3, Bell, BookOpen, BookOpenCheck, Bot, Brain, Building2, Calendar, Clock, Coins, DollarSign, Download, FlaskConical, GraduationCap, Headphones, Image as ImageIcon, Layers3, Menu, LayoutDashboard, LogOut, PieChart, Settings, Sparkles, Target, TrendingUp, Users, Video, Wallet } from 'lucide-react';
import { BrowserRouter, Link, Route, Routes, useLocation, useNavigate, useParams } from 'react-router-dom';
import { AuthGate } from './auth/AuthGate';
import { useAuth } from './auth/AuthContext';
import { FamilySkillsAdminPage } from './FamilySkillsAdminPage';
import { FamilySkillsWizardPage } from './FamilySkillsWizardPage';

const navigation = {
  es: [
    { label: 'Resumen', icon: LayoutDashboard, path: '/' },
    { label: 'Clientes empresariales', icon: Building2, path: '/business-customers' },
    { label: 'Cursos', icon: BookOpen, path: '/courses' },
    { label: 'Actividad', icon: Activity, path: '/activity' },
    { label: 'Simulations', icon: FlaskConical, path: '/simulations' },
    { label: 'Business Case', icon: BarChart3, path: '/business-case' },
    { label: 'Family Skills', icon: Layers3, path: '/family-skills' },
  ],
  en: [
    { label: 'Overview', icon: LayoutDashboard, path: '/' },
    { label: 'Business Customers', icon: Building2, path: '/business-customers' },
    { label: 'Courses', icon: BookOpen, path: '/courses' },
    { label: 'Activity', icon: Activity, path: '/activity' },
    { label: 'Simulations', icon: FlaskConical, path: '/simulations' },
    { label: 'Business Case', icon: BarChart3, path: '/business-case' },
    { label: 'Family Skills', icon: Layers3, path: '/family-skills' },
  ],
} as const;

type Tenant = { tenantId: string; name: string; slug: string; orgType: string; isActive: boolean };

type TenantDeletionSummary = {
  tenantId: string;
  tenantName: string;
  deletedRowsByTable: Record<string, number>;
  deletedBlobCount: number;
  totalRowsDeleted: number;
};

function App() {
  const [language, setLanguage] = useState<'es' | 'en'>('es');
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const isEnglish = language === 'en';

  return (
    <div className="app-shell">
      <aside className={sidebarCollapsed ? 'sidebar collapsed' : 'sidebar'}>
        <div className="sidebar-top-row">
          <div className="brand-mark">H</div>
          <button
            type="button"
            className="sidebar-toggle"
            onClick={() => setSidebarCollapsed((prev) => !prev)}
            aria-label={isEnglish ? 'Toggle main menu' : 'Alternar menú principal'}
            aria-expanded={!sidebarCollapsed}
          >
            <Menu size={18} />
          </button>
        </div>
        {!sidebarCollapsed && (
          <div>
            <p className="eyebrow">FlatBit Inc</p>
            <h1>HumanOS Admin</h1>
          </div>
        )}
        <nav aria-label={isEnglish ? 'Main navigation' : 'Navegacion principal'}>
          {navigation[language].map(({ label, icon: Icon, path }) => (
            <NavItem key={label} label={label} icon={Icon} path={path} collapsed={sidebarCollapsed} />
          ))}
        </nav>
      </aside>
      <div className="workspace">
        <Header isEnglish={isEnglish} language={language} setLanguage={setLanguage} />
        <main className="main-window">
          <Routes>
            <Route path="/business-customers" element={<BusinessCustomersPage isEnglish={isEnglish} />} />
            <Route path="/simulations" element={<SimulationsPage isEnglish={isEnglish} />} />
            <Route path="/business-case" element={<BusinessCasePage isEnglish={isEnglish} />} />
            <Route path="/family-skills" element={<FamilySkillsAdminPage isEnglish={isEnglish} />} />
            <Route path="/family-skills/wizard" element={<FamilySkillsWizardPage isEnglish={isEnglish} />} />
            <Route path="/tenants/:tenantId" element={<TenantDetailPage isEnglish={isEnglish} />} />
            <Route path="/tenants/:tenantId/people/:personId" element={<PersonDetailPage isEnglish={isEnglish} />} />
            <Route path="/tenants/:tenantId/people/:personId/brain/:courseId" element={<PersonBrainCourseDetailPage isEnglish={isEnglish} />} />
            <Route path="/tenants/:tenantId/people/:personId/courses/:courseId" element={<PersonCourseDetailPage isEnglish={isEnglish} />} />
            <Route path="/tenants/:tenantId/courses/:courseType/:courseId" element={<CourseDetailPage isEnglish={isEnglish} />} />
            <Route path="/tenants/:tenantId/courses/:courseType/:courseId/engrams/:sectionId" element={<EngramDetailPage isEnglish={isEnglish} />} />
            <Route path="*" element={<DashboardPage isEnglish={isEnglish} />} />
          </Routes>
        </main>
      </div>
    </div>
  );
}

function NavItem({ label, icon: Icon, path, collapsed }: { label: string; icon: typeof LayoutDashboard; path: string; collapsed?: boolean }) {
  const location = useLocation();
  const active = path === '/' ? location.pathname === '/' : location.pathname.startsWith(path);
  return (
    <Link className={active ? 'nav-item active' : 'nav-item'} to={path} title={collapsed ? label : undefined}>
              <Icon size={18} strokeWidth={1.8} />
              {!collapsed && <span>{label}</span>}
    </Link>
  );
}

function Header({ isEnglish, language, setLanguage }: { isEnglish: boolean; language: 'es' | 'en'; setLanguage: (language: 'es' | 'en') => void }) {
  const { account, error, logout } = useAuth();
  const displayName = account?.name ?? account?.username ?? 'HumanOS Admin';
  const initials = displayName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('');

  return (
    <header className="topbar">
      <div className="topbar-heading">
        <p className="company-name">FlatBit Inc</p>
        <p className="eyebrow">{isEnglish ? 'Control center' : 'Centro de control'}</p>
      </div>
      <div className="topbar-actions">
        <button className="icon-button" type="button" aria-label="Notificaciones"><Bell size={18} /></button>
        <button className="icon-button" type="button" aria-label="Configuracion"><Settings size={18} /></button>
        <div className="language-switch" aria-label={isEnglish ? 'Language' : 'Idioma'}>
          <button className={language === 'es' ? 'selected' : ''} onClick={() => setLanguage('es')} type="button">ES</button>
          <button className={language === 'en' ? 'selected' : ''} onClick={() => setLanguage('en')} type="button">EN</button>
        </div>
        <span className="status-pill"><span /> {isEnglish ? 'Development environment' : 'Entorno de desarrollo'}</span>
        {error && <span className="auth-inline-error" role="alert">{error}</span>}
        <div className="user-menu"><div className="profile-chip">{initials}</div><div className="user-details"><strong>{displayName}</strong><small>{account?.username}</small></div><button className="logout-button" type="button" onClick={() => void logout()}><LogOut size={16} /> {isEnglish ? 'Log out' : 'Salir'}</button></div>
      </div>
    </header>
  );
}

function DashboardPage({ isEnglish }: { isEnglish: boolean }) {
  // "Resumen" IS the platform-wide usage dashboard (2026-10-06) — see
  // UsageDashboardPage below for the 6-panel Power-BI-style breakdown
  // (courses created, courses taken, Buddy, in-course agents, Labs, Brain).
  return <UsageDashboardPage isEnglish={isEnglish} />;
}

/** Step 1 — DESIGNING the course in HumanOS Studio (Extraction of
 *  entities/facts (KnowledgeGraph), facts generation (KnowledgeFacts),
 *  Pedagogy lesson writing + Learn-stage Professor Lecture TTS narration,
 *  Deep Dive images). Distinct from CourseRuntimeCostBaseline below, which
 *  prices a STUDENT taking the FINISHED course. */
type CourseAuthoringCostBaseline = {
  costPerEngramExtraction: number | null;
  costPerEngramKnowledgeGraph: number | null;
  costPerEngramKnowledgeFacts: number | null;
  costPerEngramPedagogy: number | null;
  costPerImage: number | null;
  sampledEngramCount: number;
  sampledImageCount: number;
};

/** Step 2 — a STUDENT taking the already-built course (Learn, Recall,
 *  Assessment, "In your role"). */
type CourseRuntimeCostBaseline = {
  costPerEngramWithVoice: number | null;
  costPerEngramTextOnly: number | null;
  costPerRecall: number | null;
  costPerAssessment: number | null;
  costPerInYourRoleRun: number | null;
  sampledRuntimeRowCount: number;
};

/** Step 3 — Buddy (the voice/text AI companion) at max quota, per employee
 *  per month. Flat config rates, not usage averages — see
 *  TenantQuotaService.GetBuddyCostBaseline. */
type BuddyCostBaseline = {
  voicePerMinuteUsd: number;
  textInputPerMillionTokensUsd: number;
  textOutputPerMillionTokensUsd: number;
  defaultVoiceMinutesPerMonth: number;
  defaultTextInputTokensPerMonth: number;
  defaultTextOutputTokensPerMonth: number;
};

/** Step 5 — "AI Agents"/"Grow With Us" per-course chat agent, used
 *  freely during the day (text + voice). Brand-new tracking (2026-10-04)
 *  — costPerTextMessage is null until real usage exists. */
type AiAgentCostBaseline = {
  costPerTextMessage: number | null;
  textInputPerMillionTokensUsd: number;
  textOutputPerMillionTokensUsd: number;
  voicePerMinuteUsd: number;
  sampledTextMessageCount: number;
  usedBuddyProxy: boolean;
};

/** Quantities shared by both steps (same course/Engram counts), plus each
 *  step's own knobs — unit $ rates always come from the baselines above,
 *  never invented here. */
type ScenarioInputs = {
  totalCourses: string;
  engramsPerCourse: string;
  imagesPerEngram: string;
  useVoice: boolean;
  totalEmployees: string;
  voiceMinutesPerEmployee: string;
  textInputTokensPerEmployee: string;
  textOutputTokensPerEmployee: string;
  engramReviewsPerEmployee: string;
  aiAgentMessagesPerEmployeeDay: string;
  aiAgentVoiceMinutesPerEmployeeDay: string;
  aiAgentWorkDaysPerMonth: string;
  aiAgentInputTokensPerConversation: string;
  aiAgentOutputTokensPerConversation: string;
  functionExecutionsPerInteraction: string;
  gbSecondsPerExecution: string;
  avgMediaSizeMb: string;
  sqlActiveHoursPerWorkDay: string;
  salesCostPercent: string;
  administrationCostPercent: string;
  marketingCostPercent: string;
  salariesCostPercent: string;
  profitMarginPercent: string;
  taxRatePercent: string;
  basicCoursesIncluded: string;
  professionalCoursesIncluded: string;
  enterpriseCoursesIncluded: string;
  buddyMaxVoiceMinutesPerDay: string;
  buddyWorkDaysPerMonth: string;
  creditValueUsd: string;
  wasteBufferPercent: string;
  bulkBlockDiscountPercent: string;
  annualDiscountPercent: string;
  creditSafetyMarginPercent: string;
  simCoursesCreated: string;
  simCoursesTaken: string;
  simBuddyMinutes: string;
  simAgentMessages: string;
  simBrainReviews: string;
};

const DEFAULT_SCENARIO_INPUTS: ScenarioInputs = {
  totalCourses: '10',
  engramsPerCourse: '40',
  imagesPerEngram: '2',
  useVoice: true,
  totalEmployees: '50',
  voiceMinutesPerEmployee: '15',
  textInputTokensPerEmployee: '300000',
  textOutputTokensPerEmployee: '120000',
  engramReviewsPerEmployee: '20',
  aiAgentMessagesPerEmployeeDay: '5',
  aiAgentVoiceMinutesPerEmployeeDay: '5',
  aiAgentWorkDaysPerMonth: '22',
  aiAgentInputTokensPerConversation: '800',
  aiAgentOutputTokensPerConversation: '2000',
  functionExecutionsPerInteraction: '2',
  gbSecondsPerExecution: '0.05',
  avgMediaSizeMb: '0.3',
  sqlActiveHoursPerWorkDay: '6',
  salesCostPercent: '20',
  administrationCostPercent: '10',
  marketingCostPercent: '5',
  salariesCostPercent: '10',
  profitMarginPercent: '30',
  taxRatePercent: '25',
  basicCoursesIncluded: '5',
  professionalCoursesIncluded: '10',
  enterpriseCoursesIncluded: '20',
  buddyMaxVoiceMinutesPerDay: '5',
  buddyWorkDaysPerMonth: '22',
  creditValueUsd: '0.137',
  wasteBufferPercent: '10',
  bulkBlockDiscountPercent: '8',
  annualDiscountPercent: '20',
  creditSafetyMarginPercent: '10',
  simCoursesCreated: '1',
  simCoursesTaken: '5',
  simBuddyMinutes: '200',
  simAgentMessages: '100',
  simBrainReviews: '50',
};

/** Step 7 — REAL Azure Cost Management billing snapshot (2026-10-04,
 *  resource group "HumanOSProdc" + the HumanOSDev database on the shared
 *  "flatsqlserver"), queried directly via `az rest` against the Cost
 *  Management API — NOT a SKU-rate estimate. Month-to-date figures covered
 *  Oct 1–4 (4 days); Projected30d = MTD × 30/4, a straight-line projection,
 *  not a guarantee (both Function Apps run on FlexConsumption — true
 *  consumption pricing with $0 idle baseline — and the SQL database is
 *  Serverless with auto-pause, so a quiet month could cost less and a busy
 *  one more). Static snapshot by explicit choice (2026-10-04) — refresh by
 *  re-running the same Cost Management query and updating these numbers,
 *  rather than wiring a live Azure integration into the admin backend.
 *  Both Cognitive Services accounts are confirmed real HumanOS dependencies
 *  (verified against humanos-api-prod's actual production app settings):
 *  engramsediucation-resource = GPT text/vision/embeddings/images;
 *  twinet-resource = image generation + Realtime voice endpoint. */
const AZURE_FIXED_COST_SNAPSHOT = {
  snapshotDate: '2026-10-04',
  daysElapsed: 4,
  rows: [
    { labelEn: 'engramsediucation-resource (GPT/vision/embeddings/images)', labelEs: 'engramsediucation-resource (GPT/visión/embeddings/imágenes)', mtd: 0.67314735 },
    { labelEn: 'twinet-resource (image generation + Realtime voice)', labelEs: 'twinet-resource (generación de imágenes + voz Realtime)', mtd: 0.66811905 },
    { labelEn: 'HumanOSDev SQL database (Serverless, flatsqlserver)', labelEs: 'Base de datos SQL HumanOSDev (Serverless, flatsqlserver)', mtd: 0.135032258064516 },
    { labelEn: 'humanosprodstg (Storage account)', labelEs: 'humanosprodstg (cuenta de Storage)', mtd: 0.0086083336 },
    { labelEn: 'humanos-api-prod (Function App, FlexConsumption)', labelEs: 'humanos-api-prod (Function App, FlexConsumption)', mtd: 0 },
    { labelEn: 'humanos-labs-api-prod (Function App, FlexConsumption)', labelEs: 'humanos-labs-api-prod (Function App, FlexConsumption)', mtd: 0 },
    { labelEn: 'humanos-employee-prod + humanos-enterprise-prod (Static Web Apps, Free tier)', labelEs: 'humanos-employee-prod + humanos-enterprise-prod (Static Web Apps, tier Free)', mtd: 0 },
  ],
};

/** Step 8 — REAL Azure Retail Prices API rates (queried 2026-10-04, eastus)
 *  for the INFRASTRUCTURE layer that scales with real traffic — Function
 *  executions, Storage transactions/data, SQL Serverless compute hours, and
 *  Internet egress. Deliberately excludes AI/token costs (GPT, Realtime
 *  voice) — those are ALREADY counted in Steps 1-5 via the same Cognitive
 *  Services accounts, so re-adding them here would double-count. */
// Sourced from Azure Retail Prices API (eastus, serviceName=Functions),
// FlexConsumption "On Demand" meters — matches the REAL SKU of
// humanos-api-prod / humanos-labs-api-prod (confirmed FC1/FlexConsumption
// in Step 7). "Total Executions" is billed per 10 executions, not per 1 —
// $0.000004 / 10 = $0.0000004/execution (NOT the classic Consumption-plan
// "Standard" meter of $0.000002/10, which is 5x cheaper and doesn't apply
// to our real production plan).
const AZURE_VARIABLE_RATES = {
  functionPerExecutionUsd: 0.000004 / 10,
  functionPerGbSecondUsd: 0.000026,
  storageWritePer10kUsd: 0.05,
  storageReadPer10kUsd: 0.004,
  storageDataPerGbMonthUsd: 0.0208,
  sqlServerlessPerVCoreHourUsd: 0.521758,
  sqlVCores: 0.5, // HumanOSDev's configured min capacity
  egressFreeGbPerMonth: 100,
  egressPerGbUsd: 0.08, // first paid tier (100GB–10TB/month), "Internet" routing
};

function toNumber(value: string): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : 0;
}

/** Distinct icon + accent color per Simulations step, so the steps sidebar
 *  reads at a glance instead of showing the same flask icon 12 times. */
const STEP_ICON_META: Record<number, { icon: typeof FlaskConical; color: string }> = {
  1: { icon: Sparkles, color: '#7c3aed' },       // Studio authoring — AI creation
  2: { icon: GraduationCap, color: '#1f8a4c' },  // Student runtime — learning
  3: { icon: Headphones, color: '#e3792b' },     // Buddy — voice companion
  4: { icon: Brain, color: '#0f9ba6' },          // Brain reviews — retention
  5: { icon: Bot, color: '#d6336c' },            // AI Agents — chat
  6: { icon: Video, color: '#4f46e5' },          // Labs — practice simulations
  7: { icon: DollarSign, color: '#64748b' },     // Azure fixed costs
  8: { icon: TrendingUp, color: '#64748b' },     // Azure variable costs
  9: { icon: Building2, color: '#334155' },      // Company costs
  10: { icon: Coins, color: '#b8860b' },         // Sale pricing
  11: { icon: Layers3, color: '#1f5bb7' },       // Rate plans / packages
  12: { icon: Wallet, color: '#0e7490' },        // Credit system
};

function SimulationsPage({ isEnglish }: { isEnglish: boolean }) {
  const [step, setStep] = useState<number>(1);
  const [inputs, setInputs] = useState<ScenarioInputs>(DEFAULT_SCENARIO_INPUTS);

  // In-memory running summary (2026-10-04) — NOT persisted to any backend;
  // each step's subtotal is snapshotted here the moment the admin leaves
  // that step (moves to the next one), so Steps 2/3/4 accumulate on top of
  // whatever earlier steps already contributed, instead of only ever
  // showing the currently-active step's own total.
  const [stepTotals, setStepTotals] = useState<Record<number, number>>({});
  // Step 11 billing-cycle view toggle (Deevid/SaaS-style) — purely a
  // display choice, not a scenario cost input, so it lives in its own
  // useState rather than ScenarioInputs.
  const [billingCycle, setBillingCycle] = useState<'monthly' | 'yearly'>('monthly');
  // Steps sidebar (Paso 1-12 nav) collapse toggle — separate from the
  // app-wide sidebar; purely a display preference for this page.
  const [stepsSidebarCollapsed, setStepsSidebarCollapsed] = useState(false);

  const [authoringBaseline, setAuthoringBaseline] = useState<CourseAuthoringCostBaseline | null>(null);
  const [authoringError, setAuthoringError] = useState<string | null>(null);
  const [runtimeBaseline, setRuntimeBaseline] = useState<CourseRuntimeCostBaseline | null>(null);
  const [runtimeError, setRuntimeError] = useState<string | null>(null);
  const [buddyBaseline, setBuddyBaseline] = useState<BuddyCostBaseline | null>(null);
  const [buddyError, setBuddyError] = useState<string | null>(null);
  const [aiAgentBaseline, setAiAgentBaseline] = useState<AiAgentCostBaseline | null>(null);
  const [aiAgentError, setAiAgentError] = useState<string | null>(null);

  useEffect(() => {
    fetch('/api/simulations/course-authoring-cost-baseline')
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load Studio authoring cost baseline.');
        return response.json() as Promise<Record<string, unknown>>;
      })
      .then((row) => setAuthoringBaseline({
        costPerEngramExtraction: (row.CostPerEngramExtraction ?? row.costPerEngramExtraction ?? null) as number | null,
        costPerEngramKnowledgeGraph: (row.CostPerEngramKnowledgeGraph ?? row.costPerEngramKnowledgeGraph ?? null) as number | null,
        costPerEngramKnowledgeFacts: (row.CostPerEngramKnowledgeFacts ?? row.costPerEngramKnowledgeFacts ?? null) as number | null,
        costPerEngramPedagogy: (row.CostPerEngramPedagogy ?? row.costPerEngramPedagogy ?? null) as number | null,
        costPerImage: (row.CostPerImage ?? row.costPerImage ?? null) as number | null,
        sampledEngramCount: Number(row.SampledEngramCount ?? row.sampledEngramCount ?? 0),
        sampledImageCount: Number(row.SampledImageCount ?? row.sampledImageCount ?? 0),
      }))
      .catch((err: Error) => setAuthoringError(err.message));

    fetch('/api/simulations/course-cost-baseline')
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load runtime cost baseline.');
        return response.json() as Promise<Record<string, unknown>>;
      })
      .then((row) => setRuntimeBaseline({
        costPerEngramWithVoice: (row.CostPerEngramWithVoice ?? row.costPerEngramWithVoice ?? null) as number | null,
        costPerEngramTextOnly: (row.CostPerEngramTextOnly ?? row.costPerEngramTextOnly ?? null) as number | null,
        costPerRecall: (row.CostPerRecall ?? row.costPerRecall ?? null) as number | null,
        costPerAssessment: (row.CostPerAssessment ?? row.costPerAssessment ?? null) as number | null,
        costPerInYourRoleRun: (row.CostPerInYourRoleRun ?? row.costPerInYourRoleRun ?? null) as number | null,
        sampledRuntimeRowCount: Number(row.SampledRuntimeRowCount ?? row.sampledRuntimeRowCount ?? 0),
      }))
      .catch((err: Error) => setRuntimeError(err.message));

    fetch('/api/simulations/buddy-cost-baseline')
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load Buddy cost baseline.');
        return response.json() as Promise<Record<string, unknown>>;
      })
      .then((row) => setBuddyBaseline({
        voicePerMinuteUsd: Number(row.VoicePerMinuteUsd ?? row.voicePerMinuteUsd ?? 0),
        textInputPerMillionTokensUsd: Number(row.TextInputPerMillionTokensUsd ?? row.textInputPerMillionTokensUsd ?? 0),
        textOutputPerMillionTokensUsd: Number(row.TextOutputPerMillionTokensUsd ?? row.textOutputPerMillionTokensUsd ?? 0),
        defaultVoiceMinutesPerMonth: Number(row.DefaultVoiceMinutesPerMonth ?? row.defaultVoiceMinutesPerMonth ?? 0),
        defaultTextInputTokensPerMonth: Number(row.DefaultTextInputTokensPerMonth ?? row.defaultTextInputTokensPerMonth ?? 0),
        defaultTextOutputTokensPerMonth: Number(row.DefaultTextOutputTokensPerMonth ?? row.defaultTextOutputTokensPerMonth ?? 0),
      }))
      .catch((err: Error) => setBuddyError(err.message));

    fetch('/api/simulations/ai-agent-cost-baseline')
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load AI Agent cost baseline.');
        return response.json() as Promise<Record<string, unknown>>;
      })
      .then((row) => setAiAgentBaseline({
        costPerTextMessage: (row.CostPerTextMessage ?? row.costPerTextMessage ?? null) as number | null,
        textInputPerMillionTokensUsd: Number(row.TextInputPerMillionTokensUsd ?? row.textInputPerMillionTokensUsd ?? 0),
        textOutputPerMillionTokensUsd: Number(row.TextOutputPerMillionTokensUsd ?? row.textOutputPerMillionTokensUsd ?? 0),
        voicePerMinuteUsd: Number(row.VoicePerMinuteUsd ?? row.voicePerMinuteUsd ?? 0),
        sampledTextMessageCount: Number(row.SampledTextMessageCount ?? row.sampledTextMessageCount ?? 0),
        usedBuddyProxy: Boolean(row.UsedBuddyProxy ?? row.usedBuddyProxy ?? false),
      }))
      .catch((err: Error) => setAiAgentError(err.message));
  }, []);

  const update = (key: keyof ScenarioInputs) => (value: string | boolean) =>
    setInputs((current) => ({ ...current, [key]: value }));

  const totalCourses = toNumber(inputs.totalCourses);
  const engramsPerCourse = toNumber(inputs.engramsPerCourse);
  const imagesPerEngram = toNumber(inputs.imagesPerEngram);
  const totalEngrams = totalCourses * engramsPerCourse;

  // --- Step 1: Studio authoring (designing the course) ---
  const extractionCost = (authoringBaseline?.costPerEngramExtraction ?? 0) * totalEngrams;
  const knowledgeGraphCost = (authoringBaseline?.costPerEngramKnowledgeGraph ?? 0) * totalEngrams;
  const knowledgeFactsCost = (authoringBaseline?.costPerEngramKnowledgeFacts ?? 0) * totalEngrams;
  const pedagogyCost = (authoringBaseline?.costPerEngramPedagogy ?? 0) * totalEngrams;
  const authoringImagesCost = (authoringBaseline?.costPerImage ?? 0) * imagesPerEngram * totalEngrams;
  const totalAuthoringCost = extractionCost + knowledgeGraphCost + knowledgeFactsCost + pedagogyCost + authoringImagesCost;

  // --- Step 2: student runtime (taking the course) ---
  const learnRate = (inputs.useVoice ? runtimeBaseline?.costPerEngramWithVoice : runtimeBaseline?.costPerEngramTextOnly) ?? 0;
  // Each rate here is the REAL worst-case TOTAL already observed for that
  // whole stage on ONE Engram (sums every round/turn a real student had —
  // see TenantQuotaService.GetPlatformWideMaxRuntimeCostPerEngramAsync), so
  // it is multiplied ONLY by the number of Engrams, never by an extra
  // "rounds" count (that would double-count rounds already summed in).
  const learnCost = learnRate * totalEngrams;
  const recallsCost = (runtimeBaseline?.costPerRecall ?? 0) * totalEngrams;
  const assessmentsCost = (runtimeBaseline?.costPerAssessment ?? 0) * totalEngrams;
  const inYourRoleCost = (runtimeBaseline?.costPerInYourRoleRun ?? 0) * totalEngrams;
  const totalRuntimeCost = learnCost + recallsCost + assessmentsCost + inYourRoleCost;

  // --- Step 3: Buddy at max quota (per employee / month) ---
  const totalEmployees = toNumber(inputs.totalEmployees);
  const voiceMinutesPerEmployee = toNumber(inputs.voiceMinutesPerEmployee);
  const textInputTokensPerEmployee = toNumber(inputs.textInputTokensPerEmployee);
  const textOutputTokensPerEmployee = toNumber(inputs.textOutputTokensPerEmployee);
  const buddyVoiceCost = (buddyBaseline?.voicePerMinuteUsd ?? 0) * voiceMinutesPerEmployee * totalEmployees;
  const buddyTextInputCost = (buddyBaseline?.textInputPerMillionTokensUsd ?? 0) / 1_000_000 * textInputTokensPerEmployee * totalEmployees;
  const buddyTextOutputCost = (buddyBaseline?.textOutputPerMillionTokensUsd ?? 0) / 1_000_000 * textOutputTokensPerEmployee * totalEmployees;
  const totalBuddyCost = buddyVoiceCost + buddyTextInputCost + buddyTextOutputCost;

  // --- Step 4: Brain "Reactivar Engrama" (re-taking/reviewing one Engram) ---
  // SAME proxy formula the real QuotaCatalog.MaxEngramReviewsPerPersonPerMonth
  // quota already uses (see TenantQuotaService.EstimateMaxMonthlyCostUsd):
  // "Reactivar Engrama" walks the ENTIRE Engram again (every pedagogy stage),
  // so it's priced with the exact SAME per-Engram runtime rate as Step 2
  // (Learn + Recall + Assessment + "In your role", with/without voice) — a
  // PROXY, not yet a real measurement, since this specific feature doesn't
  // record its own call duration/tokens today.
  const engramReviewsPerEmployee = toNumber(inputs.engramReviewsPerEmployee);
  const fullEngramReviewRate = learnRate + (runtimeBaseline?.costPerRecall ?? 0) + (runtimeBaseline?.costPerAssessment ?? 0) + (runtimeBaseline?.costPerInYourRoleRun ?? 0);
  const totalBrainReviewCost = fullEngramReviewRate * engramReviewsPerEmployee * totalEmployees;

  // --- Step 5: "AI Agents"/"Grow With Us" per-course chat agent (text + voice, used freely during the day) ---
  const aiAgentMessagesPerEmployeeDay = toNumber(inputs.aiAgentMessagesPerEmployeeDay);
  const aiAgentVoiceMinutesPerEmployeeDay = toNumber(inputs.aiAgentVoiceMinutesPerEmployeeDay);
  const aiAgentWorkDaysPerMonth = toNumber(inputs.aiAgentWorkDaysPerMonth);
  const aiAgentInputTokensPerConversation = toNumber(inputs.aiAgentInputTokensPerConversation);
  const aiAgentOutputTokensPerConversation = toNumber(inputs.aiAgentOutputTokensPerConversation);
  // Text cost = real gpt-5-mini per-million-token rates × admin-entered
  // tokens/conversation (same "real rate × editable quantity" pattern as
  // Buddy's Step 3) — more transparent than a single blended $/message
  // average, and doesn't depend on real AiAgentChat usage existing yet.
  const costInPerConversation = (aiAgentBaseline?.textInputPerMillionTokensUsd ?? 0) / 1_000_000 * aiAgentInputTokensPerConversation;
  const costOutPerConversation = (aiAgentBaseline?.textOutputPerMillionTokensUsd ?? 0) / 1_000_000 * aiAgentOutputTokensPerConversation;
  const costPerConversation = costInPerConversation + costOutPerConversation;
  const aiAgentTextCost = costPerConversation * aiAgentMessagesPerEmployeeDay * aiAgentWorkDaysPerMonth * totalEmployees;
  const aiAgentVoiceCost = (aiAgentBaseline?.voicePerMinuteUsd ?? 0) * aiAgentVoiceMinutesPerEmployeeDay * aiAgentWorkDaysPerMonth * totalEmployees;
  const totalAiAgentCost = aiAgentTextCost + aiAgentVoiceCost;

  // --- Step 8: Azure VARIABLE infrastructure costs (derived from Steps 1-5's
  // own quantities — no new usage numbers invented, only technical
  // assumptions about how each interaction touches Functions/Storage/SQL/
  // network). Deliberately EXCLUDES AI/token costs — already in Steps 1-5.
  const functionExecutionsPerInteraction = toNumber(inputs.functionExecutionsPerInteraction);
  const gbSecondsPerExecution = toNumber(inputs.gbSecondsPerExecution);
  const avgMediaSizeMb = toNumber(inputs.avgMediaSizeMb);
  const sqlActiveHoursPerWorkDay = toNumber(inputs.sqlActiveHoursPerWorkDay);

  // Total real "interactions" already defined across Steps 1/2/4/5 (Buddy's
  // Step 3 is excluded — it's metered in tokens/month, not a message count,
  // so no interaction count can be derived from it without inventing one).
  const totalInteractions = totalEngrams /* Step 1: authoring, one pipeline run/Engram */
    + totalEngrams /* Step 2: one course-take/Engram */
    + (engramReviewsPerEmployee * totalEmployees) /* Step 4: Brain reviews */
    + (aiAgentMessagesPerEmployeeDay * aiAgentWorkDaysPerMonth * totalEmployees); /* Step 5: AI Agent chat turns */

  const azureFunctionExecutionsCost = totalInteractions * functionExecutionsPerInteraction * AZURE_VARIABLE_RATES.functionPerExecutionUsd;
  const azureFunctionComputeCost = totalInteractions * functionExecutionsPerInteraction * gbSecondsPerExecution * AZURE_VARIABLE_RATES.functionPerGbSecondUsd;

  // Storage: Step 1's images are written once (authoring), then read once
  // per employee who views that course (rough "everyone sees everything"
  // upper bound, deliberately conservative).
  const totalImages = imagesPerEngram * totalEngrams;
  const storageWriteCost = (totalImages / 10_000) * AZURE_VARIABLE_RATES.storageWritePer10kUsd;
  const storageReadCost = ((totalImages * totalEmployees) / 10_000) * AZURE_VARIABLE_RATES.storageReadPer10kUsd;
  const storageDataGb = (totalImages * avgMediaSizeMb) / 1024;
  const storageDataCost = storageDataGb * AZURE_VARIABLE_RATES.storageDataPerGbMonthUsd;
  const azureStorageCost = storageWriteCost + storageReadCost + storageDataCost;

  // Egress: images delivered to employees viewing a course, minus the
  // real 100GB/month free tier.
  const egressGb = (totalImages * totalEmployees * avgMediaSizeMb) / 1024;
  const billableEgressGb = Math.max(0, egressGb - AZURE_VARIABLE_RATES.egressFreeGbPerMonth);
  const azureEgressCost = billableEgressGb * AZURE_VARIABLE_RATES.egressPerGbUsd;

  // SQL: active (non-paused) compute hours. Grounded in the REAL Azure SQL
  // Serverless setting on flatsqlserver/HumanOSDev — auto-pause delay = 60
  // min (confirmed via `az sql db show`). Each interaction keeps the DB
  // awake for ~1 auto-pause cycle, so active time SCALES WITH VOLUME (more
  // interactions/workday → DB never fully cools down between them) up to
  // a ceiling: the configured operating-window hours/workday (can't exceed
  // the hours the business actually runs, or a full day).
  const realAutoPauseDelayHours = 60 / 60; // 1 hour — real setting, not invented.
  const interactionsPerWorkDay = aiAgentWorkDaysPerMonth > 0 ? totalInteractions / aiAgentWorkDaysPerMonth : 0;
  const volumeDrivenActiveHoursPerWorkDay = interactionsPerWorkDay * realAutoPauseDelayHours;
  const sqlActiveHoursPerWorkDayEffective = Math.min(sqlActiveHoursPerWorkDay, volumeDrivenActiveHoursPerWorkDay);
  const sqlActiveHours = sqlActiveHoursPerWorkDayEffective * aiAgentWorkDaysPerMonth;
  const azureSqlCost = sqlActiveHours * AZURE_VARIABLE_RATES.sqlVCores * AZURE_VARIABLE_RATES.sqlServerlessPerVCoreHourUsd;

  const totalAzureVariableCost = azureFunctionExecutionsCost + azureFunctionComputeCost + azureStorageCost + azureEgressCost + azureSqlCost;

  // --- Step 7: fixed Azure monthly projection ---
  const totalAzureFixedCost = AZURE_FIXED_COST_SNAPSHOT.rows.reduce(
    (total, row) => total + row.mtd * 30 / AZURE_FIXED_COST_SNAPSHOT.daysElapsed,
    0,
  );

  // --- Step 9: company operating costs ---
  // Selling costs are based only on the non-Azure course/product costs from
  // Steps 1-5. Admin, marketing and salaries are overhead percentages of the
  // full cost after adding sales costs (including Azure). All four % are
  // admin-editable scenario inputs, not fixed constants, so different cost
  // structures can be simulated.
  const salesCostPercent = toNumber(inputs.salesCostPercent) / 100;
  const administrationCostPercent = toNumber(inputs.administrationCostPercent) / 100;
  const marketingCostPercent = toNumber(inputs.marketingCostPercent) / 100;
  const salariesCostPercent = toNumber(inputs.salariesCostPercent) / 100;
  const nonAzureVariableCost = totalAuthoringCost + totalRuntimeCost + totalBuddyCost + totalBrainReviewCost + totalAiAgentCost;
  const salesCost = nonAzureVariableCost * salesCostPercent;
  const totalBeforeCompanyCosts = nonAzureVariableCost + totalAzureFixedCost + totalAzureVariableCost;
  const overheadBase = totalBeforeCompanyCosts + salesCost;
  const administrationCost = overheadBase * administrationCostPercent;
  const marketingCost = overheadBase * marketingCostPercent;
  const salariesCost = overheadBase * salariesCostPercent;
  const totalCompanyCosts = salesCost + administrationCost + marketingCost + salariesCost;

  const COST_STEP_DEFINITIONS: { id: number; labelEn: string; labelEs: string; liveCost: number }[] = [
    { id: 1, labelEn: 'Step 1 · Design course (Studio)', labelEs: 'Paso 1 · Diseñar curso (Studio)', liveCost: totalAuthoringCost },
    { id: 2, labelEn: 'Step 2 · Take course (runtime)', labelEs: 'Paso 2 · Tomar el curso (runtime)', liveCost: totalRuntimeCost },
    { id: 3, labelEn: 'Step 3 · Buddy at max quota', labelEs: 'Paso 3 · Buddy al máximo de cuota', liveCost: totalBuddyCost },
    { id: 4, labelEn: 'Step 4 · Brain "Reactivate Engram"', labelEs: 'Paso 4 · Brain "Reactivar Engrama"', liveCost: totalBrainReviewCost },
    { id: 5, labelEn: 'Step 5 · AI Agents ("Grow With Us")', labelEs: 'Paso 5 · AI Agents ("Grow With Us")', liveCost: totalAiAgentCost },
    { id: 6, labelEn: 'Step 6 · Labs (coming soon)', labelEs: 'Paso 6 · Labs (próximamente)', liveCost: 0 },
    { id: 7, labelEn: 'Step 7 · Azure fixed costs', labelEs: 'Paso 7 · Costos fijos de Azure', liveCost: totalAzureFixedCost },
    { id: 8, labelEn: 'Step 8 · Azure variable costs', labelEs: 'Paso 8 · Costos variables de Azure', liveCost: totalAzureVariableCost },
    { id: 9, labelEn: 'Step 9 · Company costs', labelEs: 'Paso 9 · Costos de empresa', liveCost: totalCompanyCosts },
  ];

  // --- Step 10: sale pricing, built on top of EVERY cost step (1-9, which
  // already includes Azure fixed + variable costs and company overhead) ---
  // Pricing is expressed as % of the FINAL sale price (not a markup on cost):
  //   price = cost / (1 − marginRate)
  // so that once the admin collects `price`, exactly marginRate is real
  // profit, with the remainder equal to the original cost. Margin is an
  // admin-editable scenario input (not a fixed constant), so different
  // margin scenarios can be simulated. Taxes are intentionally OMITTED for
  // now (TAX_RATE pinned at 0) — re-enable by wiring inputs.taxRatePercent
  // back into TAX_RATE once tax handling is revisited.
  const PROFIT_MARGIN_RATE = toNumber(inputs.profitMarginPercent) / 100;
  const TAX_RATE = 0;
  const PRICING_COST_SHARE = 1 - PROFIT_MARGIN_RATE - TAX_RATE; // e.g. 0.70 at 30% margin
  const toSalePrice = (cost: number) => (PRICING_COST_SHARE > 0 ? cost / PRICING_COST_SHARE : 0);
  const pricingRows = COST_STEP_DEFINITIONS.map((s) => {
    const salePrice = toSalePrice(s.liveCost);
    return {
      ...s,
      cost: s.liveCost,
      salePrice,
      profitAmount: salePrice * PROFIT_MARGIN_RATE,
    };
  });
  const totalCostAllSteps = COST_STEP_DEFINITIONS.reduce((sum, s) => sum + s.liveCost, 0);
  const totalSalePrice = toSalePrice(totalCostAllSteps);
  const totalProfitAmount = totalSalePrice * PROFIT_MARGIN_RATE;

  // --- Step 10 (per-unit view): same cost → sale price → profit logic,
  // but divided by the real quantities already entered across Steps 1-5
  // (Engrams, courses, employees), to answer "what do we actually charge
  // per Engram / per course / per employee/month?" instead of only a
  // scenario-wide total. Guards against divide-by-zero when a quantity is 0.
  const perUnit = (cost: number, units: number) => (units > 0 ? cost / units : 0);
  const unitPricingRows = [
    {
      labelEn: 'Course — design only (Studio authoring)',
      labelEs: 'Curso — solo diseño (autoría en Studio)',
      cost: perUnit(totalAuthoringCost, totalCourses),
    },
    {
      labelEn: 'Course — full (design + one student taking it)',
      labelEs: 'Curso — completo (diseño + un estudiante tomándolo)',
      cost: perUnit(totalAuthoringCost + totalRuntimeCost, totalCourses),
    },
    {
      labelEn: 'Engram — design only (Studio authoring)',
      labelEs: 'Engram — solo diseño (autoría en Studio)',
      cost: perUnit(totalAuthoringCost, totalEngrams),
    },
    {
      labelEn: 'Engram — one student taking it (runtime)',
      labelEs: 'Engram — un estudiante tomándolo (runtime)',
      cost: perUnit(totalRuntimeCost, totalEngrams),
    },
    {
      labelEn: 'Employee / month — Buddy at max quota',
      labelEs: 'Empleado / mes — Buddy al máximo de cuota',
      cost: perUnit(totalBuddyCost, totalEmployees),
    },
    {
      labelEn: 'Employee / month — Brain "Reactivate Engram" reviews',
      labelEs: 'Empleado / mes — repasos "Reactivar Engrama" de Brain',
      cost: perUnit(totalBrainReviewCost, totalEmployees),
    },
    {
      labelEn: 'Employee / month — AI Agents chat',
      labelEs: 'Empleado / mes — chat de AI Agents',
      cost: perUnit(totalAiAgentCost, totalEmployees),
    },
    {
      labelEn: 'Employee / month — all per-employee services combined',
      labelEs: 'Empleado / mes — todos los servicios por empleado combinados',
      cost: perUnit(totalBuddyCost + totalBrainReviewCost + totalAiAgentCost, totalEmployees),
    },
  ].map((row) => {
    const salePrice = toSalePrice(row.cost);
    return { ...row, salePrice, profitAmount: salePrice * PROFIT_MARGIN_RATE };
  });

  // --- Step 11: client-facing rate plans/packages, built from the SAME
  // per-unit costs already computed above — just bundled into tiers so the
  // admin can quote a client a single $/employee/month price instead of a
  // per-feature breakdown. Each tier is additive (Pro = Basic + more,
  // Enterprise = Pro + more), matching how the product actually unlocks
  // features at higher quotas. Features are described in PLAIN BUSINESS
  // TERMS (courses, Buddy, My Brain, Labs, Agents) — not internal pedagogy
  // jargon (Learn/Recall/Assessment) — since clients don't know that
  // vocabulary; the real scenario numbers (courses, reviews/month, agent
  // interactions/month) are pulled straight from the inputs above.
  //
  // "Courses included" is CATALOG BREADTH (how many distinct courses the
  // client gets access to), NOT a per-person consumption cap — it scales
  // UP per tier (5 → 10 → 20, admin-editable below), unlike the flat
  // per-employee services (Buddy/My Brain/Agents) which are either
  // on or off per tier. Any course beyond the tier's included count is
  // priced separately as pay-as-you-go overflow (below the table).
  const costPerCourseUnit = perUnit(totalAuthoringCost + totalRuntimeCost, totalCourses); // avg real cost to design + deliver ONE catalog course
  const basicCoursesIncluded = toNumber(inputs.basicCoursesIncluded);
  const professionalCoursesIncluded = toNumber(inputs.professionalCoursesIncluded);
  const enterpriseCoursesIncluded = toNumber(inputs.enterpriseCoursesIncluded);
  // GLOBAL (whole-company) package cost — NOT per employee. Course catalog
  // access (costPerCourseUnit × courses included) is inherently company-
  // wide already; Buddy/My Brain/AI Agents use their FULL company-wide
  // totals (totalBuddyCost etc., which already sum every employee), so the
  // final plan price is ONE flat number for the entire organization, like
  // a real SaaS subscription — never multiplied or divided by headcount.
  const basicCost = costPerCourseUnit * basicCoursesIncluded;
  const professionalCost = costPerCourseUnit * professionalCoursesIncluded + totalBuddyCost + totalBrainReviewCost;
  const enterpriseCost = costPerCourseUnit * enterpriseCoursesIncluded + totalBuddyCost + totalBrainReviewCost + totalAiAgentCost;
  const agentMessagesPerMonth = Math.round(aiAgentMessagesPerEmployeeDay * aiAgentWorkDaysPerMonth);
  const agentVoiceMinutesPerMonth = Math.round(aiAgentVoiceMinutesPerEmployeeDay * aiAgentWorkDaysPerMonth);
  /** Psychological pricing (industry standard: $9/$29/$99-style anchors) —
   *  rounds the computed sale price UP to a "clean" number ending in 9, at
   *  a granularity that scales with magnitude, with a SAFETY FLOOR: the
   *  result can never be below the real computed sale price (so margin is
   *  never silently eaten by the rounding — worst case we round UP and
   *  make slightly MORE profit, never less). */
  const roundToPsychologicalPrice = (salePrice: number): number => {
    if (salePrice <= 0) return 0;
    const step = salePrice < 300 ? 10 : salePrice < 1000 ? 50 : 100;
    let candidate = Math.ceil(salePrice / step) * step - 1;
    if (candidate < salePrice) candidate += step;
    return candidate;
  };
  const ratePlans = [
    {
      id: 'basic',
      nameEn: 'Basic',
      nameEs: 'Básico',
      taglineEn: 'For small teams who want to try it out',
      taglineEs: 'Para equipos pequeños que quieren probarlo',
      featuresEn: [
        'Courses built automatically from YOUR own PDFs, manuals and process documents — using advanced AI: knowledge graphs, Engrams and vector embeddings designed to maximize employee performance, not generic templates',
        'Every course is auto-personalized per employee — pulled from your HR module\'s profile, résumé, job description and skill set, so each person learns exactly what their role needs',
        'Smarter employees from day one — applied, real-world "In your role" practice, not just theory',
        'Built-in gamification keeps employees engaged and motivated, with zero extra effort from your team',
      ],
      featuresEs: [
        'Cursos construidos automáticamente a partir de TUS propios PDFs, manuales y documentos de proceso — con IA avanzada: grafos de conocimiento, Engrams y vectores diseñados para maximizar el desempeño de tus empleados, no plantillas genéricas',
        'Cada curso se personaliza automáticamente por empleado — tomando el perfil, currículum, descripción de puesto y habilidades de tu módulo de RRHH, para que cada persona aprenda exactamente lo que su rol necesita',
        'Empleados más preparados desde el primer día — práctica aplicada y real "En tu rol", no solo teoría',
        'Gamificación incluida: mantiene a tus empleados motivados y comprometidos, sin esfuerzo extra de tu equipo',
      ],
      perksEn: ['Access to Studio courses / Engrams', 'Email support'],
      perksEs: ['Acceso a cursos / Engrams de Studio', 'Soporte por correo'],
      recommended: false,
      coursesIncluded: basicCoursesIncluded,
      cost: basicCost,
    },
    {
      id: 'professional',
      nameEn: 'Professional',
      nameEs: 'Profesional',
      taglineEn: 'For growing teams building real capability',
      taglineEs: 'Para equipos en crecimiento que construyen capacidad real',
      featuresEn: [
        'Everything in Basic, personalized the same way from your HR module (profile, résumé, job description, skills)',
        'Buddy: a REAL voice AI companion for every employee — answers questions instantly, reducing costly mistakes in day-to-day processes instead of employees guessing or waiting on a coworker',
        'My Brain keeps knowledge alive automatically, protecting your training investment from the natural forgetting curve that quietly costs companies real money',
        'Employees build more advanced, role-aligned skills — connected to real capability graphs and career roadmaps, which drives retention: people who see a real growth path stay longer',
      ],
      featuresEs: [
        'Todo lo de Básico, personalizado de la misma forma desde tu módulo de RRHH (perfil, currículum, descripción de puesto, habilidades)',
        'Buddy: un compañero de IA con voz REAL para cada empleado — responde dudas al instante, reduciendo errores costosos en los procesos del día a día en vez de que el empleado adivine o espere a un compañero',
        'My Brain mantiene el conocimiento vivo automáticamente, protegiendo tu inversión en capacitación de la curva natural del olvido, que silenciosamente le cuesta dinero real a las empresas',
        'Empleados con habilidades más avanzadas y alineadas a su rol — conectado a grafos de capacidades y rutas de carrera reales, lo que mejora la retención: la gente que ve una ruta real de crecimiento se queda más tiempo',
      ],
      perksEn: ['Access to Studio courses / Engrams', 'Access to Buddy (voice + text)', 'Access to My Brain', 'Priority chat support', 'Monthly usage reports'],
      perksEs: ['Acceso a cursos / Engrams de Studio', 'Acceso a Buddy (voz + texto)', 'Acceso a My Brain', 'Soporte prioritario por chat', 'Reportes de uso mensuales'],
      recommended: true,
      coursesIncluded: professionalCoursesIncluded,
      cost: professionalCost,
    },
    {
      id: 'enterprise',
      nameEn: 'Enterprise',
      nameEs: 'Empresarial',
      taglineEn: 'For whole organizations at scale',
      taglineEs: 'Para toda la organización, a escala',
      featuresEn: [
        'Everything in Professional, at full organization scale',
        'Dedicated AI Agents grounded in your real content answer operational questions on the spot — fewer process errors, less time lost asking coworkers or supervisors',
        'Labs: realistic, voice-based practice for the toughest real-world moments — job interviews, client negotiations, angry-customer de-escalation, executive pitches, difficult feedback, sales discovery, onboarding, conflict resolution and more — with automatic AI grading, so your team rehearses costly mistakes BEFORE they happen with a real customer or client',
        'Turn any process document into hands-on training automatically — saves hundreds of hours of manual documentation and training work',
        'Full enterprise administration — roles, skills, certifications and reporting, all aligned to your real organizational structure',
        'The bottom line: smarter employees, lower turnover, fewer process errors, and real savings worth thousands of dollars a year',
      ],
      featuresEs: [
        'Todo lo de Profesional, a toda la escala de la organización',
        'Agentes de IA dedicados, fundamentados en tu contenido real, resuelven dudas operativas al instante — menos errores de proceso, menos tiempo perdido preguntando a compañeros o supervisores',
        'Labs: práctica realista con voz para los momentos más difíciles del trabajo real — entrevistas de trabajo, negociaciones con clientes, clientes enojados, pitches ejecutivos, feedback difícil, prospección de ventas, onboarding, resolución de conflictos y más — con calificación automática por IA, para que tu equipo ensaye los errores costosos ANTES de que pasen con un cliente real',
        'Convierte cualquier documento de proceso en entrenamiento práctico automáticamente — ahorra cientos de horas de documentación y capacitación manual',
        'Administración empresarial completa — roles, habilidades, certificaciones y reportes, todo alineado a tu estructura organizacional real',
        'El resultado: empleados más inteligentes, menor rotación, menos errores de proceso, y ahorros reales que valen miles de dólares al año',
      ],
      perksEn: ['Access to Studio courses / Engrams', 'Access to Buddy (voice + text)', 'Access to My Brain', 'Access to AI Agents', 'Access to Labs (voice simulations + AI rubric grading)', 'Priority chat + call support', 'Usage reports + export', 'Priority queue for AI Agents', 'Dedicated account manager'],
      perksEs: ['Acceso a cursos / Engrams de Studio', 'Acceso a Buddy (voz + texto)', 'Acceso a My Brain', 'Acceso a Agentes IA', 'Acceso a Labs (simulaciones de voz + calificación por IA)', 'Soporte prioritario por chat + llamada', 'Reportes de uso + exportación', 'Cola prioritaria para Agentes IA', 'Gerente de cuenta dedicado'],
      recommended: false,
      coursesIncluded: enterpriseCoursesIncluded,
      cost: enterpriseCost,
    },
  ].map((plan) => {
    const salePrice = toSalePrice(plan.cost);
    const displayPrice = roundToPsychologicalPrice(salePrice);
    const extraCoursesToFullCatalog = Math.max(0, totalCourses - plan.coursesIncluded);
    const fullCatalogUnlockCost = extraCoursesToFullCatalog * costPerCourseUnit;
    return {
      ...plan,
      salePrice,
      displayPrice,
      profitAmount: salePrice * PROFIT_MARGIN_RATE,
      extraCoursesToFullCatalog,
      fullCatalogUnlockSalePrice: toSalePrice(fullCatalogUnlockCost),
    };
  });
  // Pay-as-you-go: price of ONE individual extra course beyond whatever a
  // tier already includes — same unit cost for every tier (it's the same
  // catalog course), just priced with the Step 10 margin.
  const pricePerExtraCourse = toSalePrice(costPerCourseUnit);

  /** Plain-business-terms feature comparison — one row per question a
   *  non-technical client actually asks ("¿cuántos cursos?", "¿incluye
   *  Buddy?"), each cell either a yes/no flag or a real quantity string
   *  pulled from the live scenario inputs (not invented). */
  type PlanFeatureCell = boolean | string;
  const planFeatureRows: { labelEn: string; labelEs: string; values: [PlanFeatureCell, PlanFeatureCell, PlanFeatureCell] }[] = [
    {
      labelEn: 'Courses included (catalog access, not per person)',
      labelEs: 'Cursos incluidos (acceso al catálogo, no por persona)',
      values: [`${basicCoursesIncluded} cursos`, `${professionalCoursesIncluded} cursos`, `${enterpriseCoursesIncluded} cursos`],
    },
    {
      labelEn: 'Includes Buddy (personal AI companion, voice + text)',
      labelEs: 'Incluye Buddy (acompañante de IA personal, voz + texto)',
      values: [false, true, true],
    },
    {
      labelEn: 'Includes My Brain (review past courses so you don\'t forget)',
      labelEs: 'Incluye My Brain (repasar cursos pasados para no olvidar)',
      values: [false, `Sí — hasta ${engramReviewsPerEmployee} repasos/mes`, `Sí — hasta ${engramReviewsPerEmployee} repasos/mes`],
    },
    {
      labelEn: 'Includes Labs (hands-on practice simulations) — coming soon',
      labelEs: 'Incluye Labs (simulaciones de práctica) — próximamente',
      values: [false, false, true],
    },
    {
      labelEn: 'Includes AI Agents (daily Q&A chat) — max interactions/month',
      labelEs: 'Incluye Agentes IA (chat de dudas del día a día) — máximo de interacciones/mes',
      values: [false, false, `Sí — hasta ${agentMessagesPerMonth} mensajes + ${agentVoiceMinutesPerMonth} min voz/mes`],
    },
  ];

  const STEP_DEFINITIONS: { id: number; labelEn: string; labelEs: string; liveCost: number }[] = [
    ...COST_STEP_DEFINITIONS,
    { id: 10, labelEn: 'Step 10 · Sale pricing (margin + tax)', labelEs: 'Paso 10 · Precios de venta (margen + impuesto)', liveCost: 0 },
    { id: 11, labelEn: 'Step 11 · Rate plans / packages', labelEs: 'Paso 11 · Paquetes / Planes', liveCost: 0 },
    { id: 12, labelEn: 'Step 12 · Credit system', labelEs: 'Paso 12 · Sistema de créditos', liveCost: 0 },
  ];

  // --- Step 12: credit system — converts EVERY billable interaction (not
  // just courses) into a single internal currency ($0.10 USD/credit by
  // default, admin-editable). Unlike a naive "cost + margin" conversion,
  // each interaction's RAW AI cost is first scaled up by a COST LOAD
  // FACTOR that proportionally folds in Azure fixed + variable
  // infrastructure (Steps 7-8) and company overhead — sales, admin,
  // marketing, salaries (Step 9) — exactly like real unit economics: those
  // costs exist because of the AI usage, so every credit sold should carry
  // its fair share of them, not just the naked OpenAI/Azure token cost.
  // A separate WASTE/RETRY BUFFER (admin-editable, like Sora/Runway/HeyGen
  // pricing in failed renders & regenerations) is applied on top of the
  // load factor, BEFORE margin — it's a cost-side cushion (covers wasted
  // compute), not profit, so it must stay distinct from the margin %.
  //   loadFactor = (grand total cost, ALL steps) ÷ (raw AI/service cost only)
  //   loadedCost = rawCost × loadFactor × (1 + wasteBuffer%)
  //   salePrice  = loadedCost ÷ (1 − margin)      [Step 10 margin]
  //   credits    = ceil(salePrice ÷ creditValueUsd), always rounded UP
  const creditValueUsd = Math.max(0.0001, toNumber(inputs.creditValueUsd));
  const wasteBufferRate = toNumber(inputs.wasteBufferPercent) / 100;
  const costLoadFactor = nonAzureVariableCost > 0 ? totalCostAllSteps / nonAzureVariableCost : 1;
  const toCredits = (rawCost: number) => {
    const loadedCost = rawCost * costLoadFactor * (1 + wasteBufferRate);
    const salePrice = toSalePrice(loadedCost);
    const credits = creditValueUsd > 0 ? Math.ceil(salePrice / creditValueUsd) : 0;
    return { loadedCost, salePrice, credits };
  };
  const buddyMaxVoiceMinutesPerDay = toNumber(inputs.buddyMaxVoiceMinutesPerDay);
  const buddyWorkDaysPerMonth = toNumber(inputs.buddyWorkDaysPerMonth);
  const buddyMonthlyVoiceMinutesCap = buddyMaxVoiceMinutesPerDay * buddyWorkDaysPerMonth;
  const buddyQuotaCost = (buddyBaseline?.voicePerMinuteUsd ?? 0) * buddyMonthlyVoiceMinutesCap
    + (buddyBaseline?.textInputPerMillionTokensUsd ?? 0) / 1_000_000 * textInputTokensPerEmployee
    + (buddyBaseline?.textOutputPerMillionTokensUsd ?? 0) / 1_000_000 * textOutputTokensPerEmployee;
  // How many credits a SINGLE employee's Buddy quota translates to — the
  // direct answer to "so how many credits does the client actually need to
  // buy to cover this?", not just the underlying $ math.
  const buddyQuotaCreditsPerPerson = toCredits(buddyQuotaCost).credits;
  const totalBrainReviewsPerMonth = engramReviewsPerEmployee * totalEmployees;

  // --- Credit purchase simulator — INVERTED from a naive "pick a credits
  // number and see what it covers" flow: the admin instead says WHAT THE
  // CLIENT WANTS TO DO this month (quantities below), and the system
  // CALCULATES how many credits that requires — then adds an editable
  // safety margin (so the client doesn't run out mid-month) to arrive at
  // "credits to purchase". That number then drives cost/price/profit.
  const costPerCreditUsd = creditValueUsd * (1 - PROFIT_MARGIN_RATE);
  const profitPerCreditUsd = creditValueUsd * PROFIT_MARGIN_RATE;

  const createCourseCreditsEachSim = toCredits(perUnit(totalAuthoringCost, totalCourses)).credits;
  const takeCourseCreditsEachSim = toCredits(costPerCourseUnit).credits;
  const buddyVoiceMinuteCreditsSim = toCredits(buddyBaseline?.voicePerMinuteUsd ?? 0).credits;
  const aiAgentMessageCreditsSim = toCredits(costPerConversation).credits;
  const myBrainReviewCreditsSim = toCredits(perUnit(totalBrainReviewCost, totalBrainReviewsPerMonth)).credits;

  // "What does the client want to do this month?" — the admin types ANY
  // quantity for each of the 5 services; this is the DEMAND side, and it
  // drives everything else below (not a fixed scenario, not a separate
  // manual credits guess).
  const simCoursesCreated = toNumber(inputs.simCoursesCreated);
  const simCoursesTaken = toNumber(inputs.simCoursesTaken);
  const simBuddyMinutes = toNumber(inputs.simBuddyMinutes);
  const simAgentMessages = toNumber(inputs.simAgentMessages);
  const simBrainReviews = toNumber(inputs.simBrainReviews);
  const simRows = [
    { labelEn: 'Create new courses', labelEs: 'Crear cursos nuevos', quantity: simCoursesCreated, creditsEach: createCourseCreditsEachSim },
    { labelEn: 'Employees take courses (Engrams)', labelEs: 'Empleados toman cursos (Engrams)', quantity: simCoursesTaken, creditsEach: takeCourseCreditsEachSim },
    { labelEn: 'Buddy — voice minutes', labelEs: 'Buddy — minutos de voz', quantity: simBuddyMinutes, creditsEach: buddyVoiceMinuteCreditsSim },
    { labelEn: 'AI Agents — chat messages', labelEs: 'Agentes IA — mensajes de chat', quantity: simAgentMessages, creditsEach: aiAgentMessageCreditsSim },
    { labelEn: 'My Brain — reviews', labelEs: 'My Brain — repasos', quantity: simBrainReviews, creditsEach: myBrainReviewCreditsSim },
  ].map((row) => {
    const creditsUsed = row.quantity * row.creditsEach;
    return {
      ...row,
      creditsUsed,
      costToUs: creditsUsed * costPerCreditUsd,
      salePrice: creditsUsed * creditValueUsd,
    };
  });
  // The actual credits NEEDED to cover what was typed above — this is the
  // result of the calculation, not an independent input.
  const simTotalCreditsNeeded = simRows.reduce((sum, row) => sum + row.creditsUsed, 0);

  // Safety margin (editable) on top of the exact need, then the final
  // "credits to purchase" — what the client should actually buy so they
  // don't run dry before the month ends.
  const creditSafetyMarginRate = toNumber(inputs.creditSafetyMarginPercent) / 100;
  const creditsToPurchase = Math.ceil(simTotalCreditsNeeded * (1 + creditSafetyMarginRate));
  const creditsSafetyBuffer = creditsToPurchase - simTotalCreditsNeeded;
  const purchaseCostToUs = creditsToPurchase * costPerCreditUsd;
  const purchaseSalePrice = creditsToPurchase * creditValueUsd;
  const purchaseProfit = creditsToPurchase * profitPerCreditUsd;

  // "Up to X of just this one" — reference info: the ceiling if ALL the
  // credits being purchased went to a single service instead of the mix
  // typed above, so the admin can sanity-check the numbers.
  const purchaseEquivalenceRows = [
    {
      labelEn: 'Create new courses',
      labelEs: 'Crear cursos nuevos',
      creditsEach: createCourseCreditsEachSim,
    },
    {
      labelEn: 'Employees take courses (Engrams)',
      labelEs: 'Empleados toman cursos (Engrams)',
      creditsEach: takeCourseCreditsEachSim,
    },
    {
      labelEn: 'Buddy — voice minutes',
      labelEs: 'Buddy — minutos de voz',
      creditsEach: buddyVoiceMinuteCreditsSim,
    },
    {
      labelEn: 'AI Agents — chat messages',
      labelEs: 'Agentes IA — mensajes de chat',
      creditsEach: aiAgentMessageCreditsSim,
    },
    {
      labelEn: 'My Brain — reviews',
      labelEs: 'My Brain — repasos',
      creditsEach: myBrainReviewCreditsSim,
    },
  ].map((row) => ({
    ...row,
    maxIfOnlyThis: row.creditsEach > 0 ? Math.floor(creditsToPurchase / row.creditsEach) : 0,
  }));

  const creditPriceRows = [
    {
      labelEn: 'Create a new course (Studio design, one-time)',
      labelEs: 'Crear un curso nuevo (diseño en Studio, una vez)',
      rawCost: perUnit(totalAuthoringCost, totalCourses),
    },
    {
      labelEn: 'Take one full course (1 person)',
      labelEs: 'Tomar un curso completo (1 persona)',
      rawCost: costPerCourseUnit,
    },
    {
      labelEn: 'Take one individual Engram (1 person)',
      labelEs: 'Tomar un Engram individual (1 persona)',
      rawCost: perUnit(totalRuntimeCost, totalEngrams),
    },
    {
      labelEn: `Buddy — monthly quota / person (max ${buddyMaxVoiceMinutesPerDay} min/day × ${buddyWorkDaysPerMonth} days = ${buddyMonthlyVoiceMinutesCap} min/month + text)`,
      labelEs: `Buddy — cuota mensual / persona (máx. ${buddyMaxVoiceMinutesPerDay} min/día × ${buddyWorkDaysPerMonth} días = ${buddyMonthlyVoiceMinutesCap} min/mes + texto)`,
      rawCost: buddyQuotaCost,
    },
    {
      labelEn: 'Buddy — 1 voice minute (à la carte, beyond quota)',
      labelEs: 'Buddy — 1 minuto de voz (a la carta, fuera de cuota)',
      rawCost: buddyBaseline?.voicePerMinuteUsd ?? 0,
    },
    {
      labelEn: 'My Brain — 1 past-course review',
      labelEs: 'My Brain — 1 repaso de curso pasado',
      rawCost: perUnit(totalBrainReviewCost, totalBrainReviewsPerMonth),
    },
    {
      labelEn: 'Labs — 1 practice session (coming soon, no real cost yet)',
      labelEs: 'Labs — 1 sesión de práctica (próximamente, sin costo real aún)',
      rawCost: 0,
    },
    {
      labelEn: 'AI Agents — 1 chat message (pay-as-you-go)',
      labelEs: 'Agentes IA — 1 mensaje de chat (pay-as-you-go)',
      rawCost: costPerConversation,
    },
    {
      labelEn: 'AI Agents — 1 voice minute (pay-as-you-go)',
      labelEs: 'Agentes IA — 1 minuto de voz (pay-as-you-go)',
      rawCost: aiAgentBaseline?.voicePerMinuteUsd ?? 0,
    },
  ].map((row) => ({ ...row, ...toCredits(row.rawCost) }));
  // Pre-paid bulk blocks — same "cost → credits" pricing as the à la carte
  // rows above, but bundled with a volume discount (admin-editable), so
  // buying the block is a REAL deal vs. paying unit-by-unit (otherwise a
  // "block" is mathematically identical to N individual purchases and
  // gives the client no reason to prepay — same logic Twilio/mobile data
  // plans use). Credits are rounded UP after the discount (still in the
  // company's favor), and "savedCredits" is the discount made visible.
  // Covers EVERY recurring à la carte unit from the table above — NOT the
  // one-time "create a course" row, and not Buddy's monthly quota or
  // course-taking (those are already bundles of their own, in Steps 9-11).
  const bulkBlockDiscountRate = toNumber(inputs.bulkBlockDiscountPercent) / 100;
  const engramRunCredits = toCredits(perUnit(totalRuntimeCost, totalEngrams)).credits;
  const buddyVoiceMinuteCredits = toCredits(buddyBaseline?.voicePerMinuteUsd ?? 0).credits;
  const myBrainReviewCredits = toCredits(perUnit(totalBrainReviewCost, totalBrainReviewsPerMonth)).credits;
  const aiAgentMessageCredits = toCredits(costPerConversation).credits;
  const aiAgentVoiceMinuteCredits = toCredits(aiAgentBaseline?.voicePerMinuteUsd ?? 0).credits;
  const bulkBlocks = [
    {
      labelEn: 'Take 50 individual Engrams (pre-paid block)',
      labelEs: 'Tomar 50 Engrams individuales (bloque prepago)',
      unitCredits: engramRunCredits,
      blockSize: 50,
    },
    {
      labelEn: 'Buddy — 100 voice minutes (pre-paid block)',
      labelEs: 'Buddy — bloque de 100 minutos de voz',
      unitCredits: buddyVoiceMinuteCredits,
      blockSize: 100,
    },
    {
      labelEn: 'My Brain — 10 reviews (pre-paid block)',
      labelEs: 'My Brain — bloque de 10 repasos',
      unitCredits: myBrainReviewCredits,
      blockSize: 10,
    },
    {
      labelEn: 'AI Agents — 100 chat messages (pre-paid block)',
      labelEs: 'Agentes IA — bloque de 100 mensajes',
      unitCredits: aiAgentMessageCredits,
      blockSize: 100,
    },
    {
      labelEn: 'AI Agents — 100 voice minutes (pre-paid block)',
      labelEs: 'Agentes IA — bloque de 100 minutos de voz',
      unitCredits: aiAgentVoiceMinuteCredits,
      blockSize: 100,
    },
  ].map((block) => {
    const aLaCarteTotalCredits = block.unitCredits * block.blockSize;
    const blockCredits = Math.ceil(aLaCarteTotalCredits * (1 - bulkBlockDiscountRate));
    return { ...block, aLaCarteTotalCredits, blockCredits, savedCredits: aLaCarteTotalCredits - blockCredits };
  });
  // Packages (Step 11), expressed as credits included per month — based on
  // the REAL psychological price the client actually pays (displayPrice,
  // e.g. $249). Two deliberate obfuscation layers so a client can't easily
  // back out the $/credit rate just by inspecting the numbers:
  //   1) creditValueUsd is a non-round number (e.g. $0.137, not $0.10) —
  //      not a "clean" value someone would guess on sight.
  //   2) The raw price÷creditValue result is rounded DOWN to a "marketing
  //      round" number whose step size grows with plan size (25/100/250),
  //      which deliberately breaks exact proportionality between tiers —
  //      comparing any two plans' price and credits no longer reveals a
  //      clean, consistent $/credit ratio. Always rounds DOWN (company's
  //      favor), so clients never receive more credits than what they paid for.
  const roundCreditsForDisplay = (rawCredits: number): number => {
    if (rawCredits <= 0) return 0;
    const step = rawCredits < 2000 ? 25 : rawCredits < 10000 ? 100 : 250;
    return Math.floor(rawCredits / step) * step;
  };
  const ratePlansInCredits = ratePlans.map((plan) => ({
    ...plan,
    creditsIncluded: creditValueUsd > 0 ? roundCreditsForDisplay(plan.displayPrice / creditValueUsd) : 0,
  }));

  // --- Step 11 card display (Deevid/SaaS-style): adds the annual-billing
  // price (monthly-equivalent, discounted, with the monthly list price
  // struck through) and a human-readable credits equivalence that matches
  // what HumanOS credits are ACTUALLY spent on. Credits are a SINGLE
  // FUNGIBLE pool that covers every billable activity in the platform —
  // creating courses, employees taking courses, Buddy (voice + text),
  // AI Agents (chat + voice), and My Brain reviews. The client must
  // understand ALL FIVE draw from the same balance and can be mixed
  // however they want — not just "create vs. take courses".
  const annualDiscountRate = toNumber(inputs.annualDiscountPercent) / 100;
  const createCourseCreditsEach = toCredits(perUnit(totalAuthoringCost, totalCourses)).credits;
  const takeCourseCreditsEach = toCredits(costPerCourseUnit).credits;
  const ratePlansDisplay = ratePlansInCredits.map((plan) => {
    const annualMonthlyEquivalent = Math.max(1, Math.round(plan.displayPrice * (1 - annualDiscountRate)));
    const annualTotalBilled = annualMonthlyEquivalent * 12;
    const equivCoursesCreated = createCourseCreditsEach > 0 ? Math.floor(plan.creditsIncluded / createCourseCreditsEach) : 0;
    const equivCoursesTaken = takeCourseCreditsEach > 0 ? Math.floor(plan.creditsIncluded / takeCourseCreditsEach) : 0;
    const equivBuddyMinutes = buddyVoiceMinuteCredits > 0 ? Math.floor(plan.creditsIncluded / buddyVoiceMinuteCredits) : 0;
    const equivAgentMessages = aiAgentMessageCredits > 0 ? Math.floor(plan.creditsIncluded / aiAgentMessageCredits) : 0;
    const equivBrainReviews = myBrainReviewCredits > 0 ? Math.floor(plan.creditsIncluded / myBrainReviewCredits) : 0;
    return { ...plan, annualMonthlyEquivalent, annualTotalBilled, equivCoursesCreated, equivCoursesTaken, equivBuddyMinutes, equivAgentMessages, equivBrainReviews };
  });

  /** Snapshots the step being left (so later steps accumulate on top of it
   *  in memory, never recomputed away once visited) before switching. */
  const goToStep = (nextStep: number) => {
    const current = STEP_DEFINITIONS.find((s) => s.id === step);
    if (current) {
      setStepTotals((prev) => ({ ...prev, [step]: current.liveCost }));
    }
    setStep(nextStep);
  };

  /** "Calcular" button: snapshots EVERY step's current live value in one
   *  shot, so the summary table below shows all cost steps recomputed with
   *  whatever scenario inputs are set right now — no need to visit each
   *  tab individually first. */
  const recalculateAll = () => {
    setStepTotals(() => {
      const next: Record<number, number> = {};
      for (const s of STEP_DEFINITIONS) next[s.id] = s.liveCost;
      return next;
    });
  };

  // Running summary (2026-10-05) — shows every step once it has been
  // snapshotted (either by visiting it, or by pressing "Calcular" to
  // snapshot all of them at once), each row showing its OWN subtotal plus
  // the cumulative sum through that row.
  let cumulative = 0;
  const summaryRows = STEP_DEFINITIONS
    .filter((s) => s.id <= Math.max(step, ...Object.keys(stepTotals).map(Number), 0))
    .map((s) => {
      const subtotal = stepTotals[s.id] ?? (s.id === step ? s.liveCost : 0);
      cumulative += subtotal;
      return { ...s, subtotal, cumulative };
    });
  const grandTotal = cumulative;

  return <>
    <div className="main-title-row">
      <div>
        <p className="eyebrow">{isEnglish ? 'Business scenario simulator' : 'Simulador de escenarios de negocio'}</p>
        <h2>Simulations</h2>
      </div>
    </div>

    <div className="steps-layout">
      <aside className={stepsSidebarCollapsed ? 'steps-sidebar collapsed' : 'steps-sidebar'}>
        <button
          type="button"
          className="steps-sidebar-toggle"
          onClick={() => setStepsSidebarCollapsed((prev) => !prev)}
          aria-label={isEnglish ? 'Toggle steps menu' : 'Alternar menú de pasos'}
          aria-expanded={!stepsSidebarCollapsed}
        >
          <Menu size={16} />
          {!stepsSidebarCollapsed && <span>{isEnglish ? 'Steps' : 'Pasos'}</span>}
        </button>
        <nav className="steps-nav">
          {STEP_DEFINITIONS.map((s) => {
            const meta = STEP_ICON_META[s.id] ?? { icon: FlaskConical, color: '#55708f' };
            const StepIcon = meta.icon;
            const isActive = step === s.id;
            return (
              <button
                key={s.id}
                type="button"
                className={`steps-nav-item${isActive ? ' active' : ''}`}
                onClick={() => goToStep(s.id)}
                title={stepsSidebarCollapsed ? (isEnglish ? s.labelEn : s.labelEs) : undefined}
                style={isActive ? { background: meta.color, boxShadow: `0 6px 14px ${meta.color}4d` } : undefined}
              >
                <span className="steps-nav-icon" style={{ color: isActive ? '#ffffff' : meta.color }}>
                  <StepIcon size={16} />
                </span>
                {!stepsSidebarCollapsed && <span>{isEnglish ? s.labelEn : s.labelEs}</span>}
              </button>
            );
          })}
        </nav>
      </aside>

      <div className="steps-content">

    <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
      <p className="customer-label" style={{ marginBottom: 14 }}>
        {isEnglish ? 'Scenario quantities' : 'Cantidades del escenario'}
      </p>
      <div className="stat-card-grid">
        {(step === 1 || step === 2) && (
          <>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Total courses' : 'Total cursos'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.totalCourses}
                onChange={(e) => update('totalCourses')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Engrams per course' : 'Engrams por curso'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.engramsPerCourse}
                onChange={(e) => update('engramsPerCourse')(e.target.value)} />
            </label>
          </>
        )}
        {step === 1 && (
          <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
            <span className="customer-label">{isEnglish ? 'Images per Engram (Deep Dive cards)' : 'Imágenes por Engram (tarjetas Deep Dive)'}</span>
            <input type="number" min={0} className="modal-confirm-input" value={inputs.imagesPerEngram}
              onChange={(e) => update('imagesPerEngram')(e.target.value)} />
          </label>
        )}
        {step === 2 && (
          <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
            <span className="customer-label">{isEnglish ? 'Realtime voice for Learn stage?' : '¿Voz Realtime en la etapa Learn?'}</span>
            <span style={{ display: 'flex', gap: 10 }}>
              <button type="button" className={`tenant-subtab${inputs.useVoice ? ' active' : ''}`} onClick={() => update('useVoice')(true)}>
                {isEnglish ? 'With voice' : 'Con voz'}
              </button>
              <button type="button" className={`tenant-subtab${!inputs.useVoice ? ' active' : ''}`} onClick={() => update('useVoice')(false)}>
                {isEnglish ? 'Text only' : 'Solo texto'}
              </button>
            </span>
          </label>
        )}
        {step === 3 && (
          <>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Total employees' : 'Total empleados'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.totalEmployees}
                onChange={(e) => update('totalEmployees')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Voice minutes / employee / month' : 'Minutos de voz / empleado / mes'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.voiceMinutesPerEmployee}
                onChange={(e) => update('voiceMinutesPerEmployee')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Text input tokens / employee / month' : 'Tokens de entrada de texto / empleado / mes'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.textInputTokensPerEmployee}
                onChange={(e) => update('textInputTokensPerEmployee')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Text output tokens / employee / month' : 'Tokens de salida de texto / empleado / mes'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.textOutputTokensPerEmployee}
                onChange={(e) => update('textOutputTokensPerEmployee')(e.target.value)} />
            </label>
          </>
        )}
        {step === 4 && (
          <>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Total employees' : 'Total empleados'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.totalEmployees}
                onChange={(e) => update('totalEmployees')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? '"Reactivate Engram" reviews / employee / month' : 'Repasos "Reactivar Engrama" / empleado / mes'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.engramReviewsPerEmployee}
                onChange={(e) => update('engramReviewsPerEmployee')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Realtime voice during review?' : '¿Voz Realtime durante el repaso?'}</span>
              <span style={{ display: 'flex', gap: 10 }}>
                <button type="button" className={`tenant-subtab${inputs.useVoice ? ' active' : ''}`} onClick={() => update('useVoice')(true)}>
                  {isEnglish ? 'With voice' : 'Con voz'}
                </button>
                <button type="button" className={`tenant-subtab${!inputs.useVoice ? ' active' : ''}`} onClick={() => update('useVoice')(false)}>
                  {isEnglish ? 'Text only' : 'Solo texto'}
                </button>
              </span>
            </label>
          </>
        )}
        {step === 5 && (
          <>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Total employees' : 'Total empleados'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.totalEmployees}
                onChange={(e) => update('totalEmployees')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Text messages / employee / day' : 'Mensajes de texto / empleado / día'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.aiAgentMessagesPerEmployeeDay}
                onChange={(e) => update('aiAgentMessagesPerEmployeeDay')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Voice minutes / employee / day' : 'Minutos de voz / empleado / día'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.aiAgentVoiceMinutesPerEmployeeDay}
                onChange={(e) => update('aiAgentVoiceMinutesPerEmployeeDay')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Work days / month' : 'Días laborables / mes'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.aiAgentWorkDaysPerMonth}
                onChange={(e) => update('aiAgentWorkDaysPerMonth')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Input tokens / conversation' : 'Tokens de entrada / conversación'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.aiAgentInputTokensPerConversation}
                onChange={(e) => update('aiAgentInputTokensPerConversation')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Output tokens / conversation' : 'Tokens de salida / conversación'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.aiAgentOutputTokensPerConversation}
                onChange={(e) => update('aiAgentOutputTokensPerConversation')(e.target.value)} />
            </label>
          </>
        )}
        {step === 8 && (
          <>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Function executions / interaction' : 'Ejecuciones de Function / interacción'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.functionExecutionsPerInteraction}
                onChange={(e) => update('functionExecutionsPerInteraction')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'GB-seconds / execution' : 'GB-segundos / ejecución'}</span>
              <input type="number" min={0} step="0.01" className="modal-confirm-input" value={inputs.gbSecondsPerExecution}
                onChange={(e) => update('gbSecondsPerExecution')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Avg media size (MB) / image' : 'Tamaño prom. (MB) / imagen'}</span>
              <input type="number" min={0} step="0.01" className="modal-confirm-input" value={inputs.avgMediaSizeMb}
                onChange={(e) => update('avgMediaSizeMb')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Max operating window (ceiling) / work day' : 'Ventana operativa máxima (techo) / día laborable'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.sqlActiveHoursPerWorkDay}
                onChange={(e) => update('sqlActiveHoursPerWorkDay')(e.target.value)} />
            </label>
          </>
        )}
        {step === 9 && (
          <>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Sales / selling cost (% of non-Azure cost)' : 'Costo de ventas (% del costo no-Azure)'}</span>
              <input type="number" min={0} max={100} step="0.1" className="modal-confirm-input" value={inputs.salesCostPercent}
                onChange={(e) => update('salesCostPercent')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Administration (% of total after sales)' : 'Administración (% del total después de ventas)'}</span>
              <input type="number" min={0} max={100} step="0.1" className="modal-confirm-input" value={inputs.administrationCostPercent}
                onChange={(e) => update('administrationCostPercent')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Marketing (% of total after sales)' : 'Marketing (% del total después de ventas)'}</span>
              <input type="number" min={0} max={100} step="0.1" className="modal-confirm-input" value={inputs.marketingCostPercent}
                onChange={(e) => update('marketingCostPercent')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Salaries (% of total after sales)' : 'Salarios (% del total después de ventas)'}</span>
              <input type="number" min={0} max={100} step="0.1" className="modal-confirm-input" value={inputs.salariesCostPercent}
                onChange={(e) => update('salariesCostPercent')(e.target.value)} />
            </label>
          </>
        )}
        {step === 10 && (
          <>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Target profit margin (% of sale price)' : 'Margen de ganancia esperado (% del precio de venta)'}</span>
              <input type="number" min={0} max={99} step="0.1" className="modal-confirm-input" value={inputs.profitMarginPercent}
                onChange={(e) => update('profitMarginPercent')(e.target.value)} />
            </label>
          </>
        )}
        {step === 11 && (
          <>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Basic — courses included (catalog access)' : 'Básico — cursos incluidos (acceso al catálogo)'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.basicCoursesIncluded}
                onChange={(e) => update('basicCoursesIncluded')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Professional — courses included (catalog access)' : 'Profesional — cursos incluidos (acceso al catálogo)'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.professionalCoursesIncluded}
                onChange={(e) => update('professionalCoursesIncluded')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Enterprise — courses included (catalog access)' : 'Empresarial — cursos incluidos (acceso al catálogo)'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.enterpriseCoursesIncluded}
                onChange={(e) => update('enterpriseCoursesIncluded')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Annual billing discount (%)' : 'Descuento por facturación anual (%)'}</span>
              <input type="number" min={0} max={90} step="1" className="modal-confirm-input" value={inputs.annualDiscountPercent}
                onChange={(e) => update('annualDiscountPercent')(e.target.value)} />
            </label>
          </>
        )}
        {step === 12 && (
          <>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Credit value (USD / credit)' : 'Valor del crédito (USD / crédito)'}</span>
              <input type="number" min={0.01} step="0.01" className="modal-confirm-input" value={inputs.creditValueUsd}
                onChange={(e) => update('creditValueUsd')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Buddy — max voice minutes / DAY (quota)' : 'Buddy — máx. minutos de voz / DÍA (cuota)'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.buddyMaxVoiceMinutesPerDay}
                onChange={(e) => update('buddyMaxVoiceMinutesPerDay')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Buddy — work days / month (quota)' : 'Buddy — días laborables / mes (cuota)'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.buddyWorkDaysPerMonth}
                onChange={(e) => update('buddyWorkDaysPerMonth')(e.target.value)} />
            </label>
            <div className="stat-card" style={{ display: 'grid', gap: 6, background: '#f7fafd' }}>
              <span className="customer-label">{isEnglish ? '→ Resulting monthly quota' : '→ Cuota mensual resultante'}</span>
              <p style={{ margin: 0, fontSize: 13, color: '#55708f' }}>
                {isEnglish
                  ? `${buddyMaxVoiceMinutesPerDay} min/day × ${buddyWorkDaysPerMonth} work days = `
                  : `${buddyMaxVoiceMinutesPerDay} min/día × ${buddyWorkDaysPerMonth} días laborables = `}
                <strong style={{ color: '#1f8a4c' }}>{buddyMonthlyVoiceMinutesCap} {isEnglish ? 'min/month' : 'min/mes'}</strong>
              </p>
              <p style={{ margin: 0, fontSize: 13, color: '#55708f' }}>
                {isEnglish ? 'Credits needed for 1 person\'s Buddy quota: ' : 'Créditos necesarios para la cuota de Buddy de 1 persona: '}
                <strong style={{ color: '#1f8a4c' }}>{buddyQuotaCreditsPerPerson.toLocaleString()} {isEnglish ? 'credits/month' : 'créditos/mes'}</strong>
              </p>
              <p style={{ margin: 0, fontSize: 11, color: '#9aa7b8' }}>
                {isEnglish
                  ? 'This monthly total (not the daily number) is what the "Buddy — monthly quota" row below is priced on. To combine Buddy with other services for a whole purchase, use the calculator further down ("What does the client want to do this month?").'
                  : 'Este total mensual (no el número diario) es con el que se cotiza la fila "Buddy — cuota mensual" de abajo. Para combinar Buddy con otros servicios en una sola compra, usa la calculadora de más abajo ("¿Qué quiere hacer el cliente este mes?").'}
              </p>
            </div>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Waste / retry buffer (% of cost, before margin)' : 'Buffer de desperdicio / reintentos (% del costo, antes del margen)'}</span>
              <input type="number" min={0} max={100} step="0.1" className="modal-confirm-input" value={inputs.wasteBufferPercent}
                onChange={(e) => update('wasteBufferPercent')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Pre-paid block volume discount (%)' : 'Descuento por volumen en bloques prepago (%)'}</span>
              <input type="number" min={0} max={90} step="0.1" className="modal-confirm-input" value={inputs.bulkBlockDiscountPercent}
                onChange={(e) => update('bulkBlockDiscountPercent')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Safety margin on top of exact need (%)' : 'Margen de seguridad sobre la necesidad exacta (%)'}</span>
              <input type="number" min={0} max={100} step="1" className="modal-confirm-input" value={inputs.creditSafetyMarginPercent}
                onChange={(e) => update('creditSafetyMarginPercent')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'What does the client want? New courses created' : '¿Qué quiere el cliente? Cursos nuevos creados'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.simCoursesCreated}
                onChange={(e) => update('simCoursesCreated')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Courses taken (Engrams)' : 'Cursos tomados (Engrams)'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.simCoursesTaken}
                onChange={(e) => update('simCoursesTaken')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'Buddy voice minutes' : 'Minutos de voz de Buddy'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.simBuddyMinutes}
                onChange={(e) => update('simBuddyMinutes')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'AI Agent messages' : 'Mensajes de Agentes IA'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.simAgentMessages}
                onChange={(e) => update('simAgentMessages')(e.target.value)} />
            </label>
            <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
              <span className="customer-label">{isEnglish ? 'My Brain reviews' : 'Repasos de My Brain'}</span>
              <input type="number" min={0} className="modal-confirm-input" value={inputs.simBrainReviews}
                onChange={(e) => update('simBrainReviews')(e.target.value)} />
            </label>
          </>
        )}
      </div>
    </section>

    {step === 1 ? (
      authoringError ? <div className="data-message error">{authoringError}</div>
      : authoringBaseline === null ? <div className="data-message">{isEnglish ? 'Loading real Studio authoring cost rates...' : 'Cargando tarifas reales de autoría en Studio...'}</div>
      : <>
        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish
              ? `Real Studio DESIGN $ figures — already computed from actual Studio pipeline usage (${authoringBaseline.sampledEngramCount} Engram(s), ${authoringBaseline.sampledImageCount} image(s)). This is the cost of CREATING the course (PDF extraction, knowledge graph, facts, lesson writing + Learn-stage narration) — NOT a student taking it (see Step 2).`
              : `Cifras $ reales de DISEÑO en Studio — ya calculadas del uso real del pipeline de Studio (${authoringBaseline.sampledEngramCount} Engram(s), ${authoringBaseline.sampledImageCount} imagen(es)). Este es el costo de CREAR el curso (extracción del PDF, grafo de conocimiento, facts, redacción de la lección + narración de Learn) — NO el de un estudiante tomándolo (ver Paso 2).`}
          </p>
          <div className="stat-card-grid">
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'PDF extraction' : 'Extracción del PDF'}</p>
              <h3>{authoringBaseline.costPerEngramExtraction !== null ? formatUsd(authoringBaseline.costPerEngramExtraction) : '—'}</h3>
            </div>
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Knowledge graph (entities + connections)' : 'Grafo de conocimiento (entidades + conexiones)'}</p>
              <h3>{authoringBaseline.costPerEngramKnowledgeGraph !== null ? formatUsd(authoringBaseline.costPerEngramKnowledgeGraph) : '—'}</h3>
            </div>
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Knowledge facts' : 'Facts de conocimiento'}</p>
              <h3>{authoringBaseline.costPerEngramKnowledgeFacts !== null ? formatUsd(authoringBaseline.costPerEngramKnowledgeFacts) : '—'}</h3>
            </div>
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Pedagogy (lesson writing + Learn narration/TTS)' : 'Pedagogía (redacción + narración/TTS de Learn)'}</p>
              <h3>{authoringBaseline.costPerEngramPedagogy !== null ? formatUsd(authoringBaseline.costPerEngramPedagogy) : '—'}</h3>
            </div>
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Image / Deep Dive card' : 'Imagen / tarjeta Deep Dive'}</p>
              <h3>{authoringBaseline.costPerImage !== null ? formatUsd(authoringBaseline.costPerImage) : '—'}</h3>
            </div>
          </div>
        </section>

        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish ? 'Step 1 cost breakdown — designing the course' : 'Paso 1 — desglose de costo de diseñar el curso'}
          </p>
          <table className="cost-table">
            <thead>
              <tr>
                <th>{isEnglish ? 'Component' : 'Componente'}</th>
                <th>{isEnglish ? 'Est. cost' : 'Costo est.'}</th>
              </tr>
            </thead>
            <tbody>
              <tr><td>{isEnglish ? `PDF extraction (${totalEngrams} Engrams)` : `Extracción del PDF (${totalEngrams} Engrams)`}</td><td>{formatUsd(extractionCost)}</td></tr>
              <tr><td>{isEnglish ? `Knowledge graph (${totalEngrams} Engrams)` : `Grafo de conocimiento (${totalEngrams} Engrams)`}</td><td>{formatUsd(knowledgeGraphCost)}</td></tr>
              <tr><td>{isEnglish ? `Knowledge facts (${totalEngrams} Engrams)` : `Facts de conocimiento (${totalEngrams} Engrams)`}</td><td>{formatUsd(knowledgeFactsCost)}</td></tr>
              <tr><td>{isEnglish ? `Pedagogy + Learn narration (${totalEngrams} Engrams)` : `Pedagogía + narración de Learn (${totalEngrams} Engrams)`}</td><td>{formatUsd(pedagogyCost)}</td></tr>
              <tr><td>{isEnglish ? `Images (${imagesPerEngram * totalEngrams} image(s))` : `Imágenes (${imagesPerEngram * totalEngrams} imagen(es))`}</td><td>{formatUsd(authoringImagesCost)}</td></tr>
            </tbody>
          </table>
          <div className="stat-card stat-card-wide tenant-section-gap">
            <p className="customer-label">{isEnglish ? 'Total design (Studio) cost' : 'Costo total de diseño (Studio)'}</p>
            <h3>{formatUsd(totalAuthoringCost)}</h3>
          </div>
        </section>
      </>
    ) : step === 2 ? (
      runtimeError ? <div className="data-message error">{runtimeError}</div>
      : runtimeBaseline === null ? <div className="data-message">{isEnglish ? 'Loading real runtime cost rates...' : 'Cargando tarifas reales de runtime...'}</div>
      : <>
        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish
              ? `Real STUDENT runtime $ figures — already computed from actual recorded student usage (${runtimeBaseline.sampledRuntimeRowCount} row(s)). This is the cost of a student TAKING the already-built course (Learn, Recall, Assessment, "In your role") — NOT the cost of creating it (see Step 1). Each figure is the worst-case TOTAL for that whole stage on one Engram (already includes every round/turn observed), multiplied below only by the Engram count.`
              : `Cifras $ reales de un ESTUDIANTE tomando el curso — ya calculadas del uso real registrado (${runtimeBaseline.sampledRuntimeRowCount} fila(s)). Este es el costo de un estudiante TOMANDO el curso ya construido (Learn, Recall, Assessment, "En tu rol") — NO el de crearlo (ver Paso 1). Cada cifra es el total de peor caso de toda esa etapa en un Engram (ya incluye cada ronda/turno observado), multiplicada abajo solo por el número de Engrams.`}
          </p>
          <div className="stat-card-grid">
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Learn — with voice' : 'Learn — con voz'}</p>
              <h3>{runtimeBaseline.costPerEngramWithVoice !== null ? formatUsd(runtimeBaseline.costPerEngramWithVoice) : '—'}</h3>
            </div>
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Learn — text only' : 'Learn — solo texto'}</p>
              <h3>{runtimeBaseline.costPerEngramTextOnly !== null ? formatUsd(runtimeBaseline.costPerEngramTextOnly) : '—'}</h3>
            </div>
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Recall — worst case / Engram' : 'Recall — peor caso / Engram'}</p>
              <h3>{runtimeBaseline.costPerRecall !== null ? formatUsd(runtimeBaseline.costPerRecall) : '—'}</h3>
            </div>
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Assessment — worst case / Engram' : 'Assessment — peor caso / Engram'}</p>
              <h3>{runtimeBaseline.costPerAssessment !== null ? formatUsd(runtimeBaseline.costPerAssessment) : '—'}</h3>
            </div>
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? '"In your role" — worst case / Engram' : '"En tu rol" — peor caso / Engram'}</p>
              <h3>{runtimeBaseline.costPerInYourRoleRun !== null ? formatUsd(runtimeBaseline.costPerInYourRoleRun) : '—'}</h3>
            </div>
          </div>
        </section>

        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish ? 'Step 2 cost breakdown — one student taking the course' : 'Paso 2 — desglose de costo de un estudiante tomando el curso'}
          </p>
          <table className="cost-table">
            <thead>
              <tr>
                <th>{isEnglish ? 'Component' : 'Componente'}</th>
                <th>{isEnglish ? 'Est. cost' : 'Costo est.'}</th>
              </tr>
            </thead>
            <tbody>
              <tr><td>{isEnglish ? `Learn (${totalEngrams} Engrams)` : `Learn (${totalEngrams} Engrams)`}</td><td>{formatUsd(learnCost)}</td></tr>
              <tr><td>{isEnglish ? `Recall — worst case (${totalEngrams} Engrams)` : `Recall — peor caso (${totalEngrams} Engrams)`}</td><td>{formatUsd(recallsCost)}</td></tr>
              <tr><td>{isEnglish ? `Assessment — worst case (${totalEngrams} Engrams)` : `Assessment — peor caso (${totalEngrams} Engrams)`}</td><td>{formatUsd(assessmentsCost)}</td></tr>
              <tr><td>{isEnglish ? `"In your role" — worst case (${totalEngrams} Engrams)` : `"En tu rol" — peor caso (${totalEngrams} Engrams)`}</td><td>{formatUsd(inYourRoleCost)}</td></tr>
            </tbody>
          </table>
          <div className="stat-card stat-card-wide tenant-section-gap">
            <p className="customer-label">{isEnglish ? 'Total runtime (one student) cost' : 'Costo total de runtime (un estudiante)'}</p>
            <h3>{formatUsd(totalRuntimeCost)}</h3>
          </div>
        </section>
      </>
    ) : step === 3 ? (
      buddyError ? <div className="data-message error">{buddyError}</div>
      : buddyBaseline === null ? <div className="data-message">{isEnglish ? 'Loading real Buddy cost rates...' : 'Cargando tarifas reales de Buddy...'}</div>
      : <>
        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish
              ? `Real Buddy $ rates — the exact same config-driven rates already shown on the admin "Quotas" tab's Buddy category (platform defaults: ${buddyBaseline.defaultVoiceMinutesPerMonth} voice min, ${buddyBaseline.defaultTextInputTokensPerMonth.toLocaleString()} input tokens, ${buddyBaseline.defaultTextOutputTokensPerMonth.toLocaleString()} output tokens / employee / month). Flat per-unit rates, not usage averages — multiply by whatever quota limit you want to simulate below.`
              : `Tarifas $ reales de Buddy — exactamente las mismas tarifas configuradas que ya se ven en la pestaña "Quotas" del admin, categoría Buddy (valores por defecto de plataforma: ${buddyBaseline.defaultVoiceMinutesPerMonth} min de voz, ${buddyBaseline.defaultTextInputTokensPerMonth.toLocaleString()} tokens de entrada, ${buddyBaseline.defaultTextOutputTokensPerMonth.toLocaleString()} tokens de salida / empleado / mes). Son tarifas planas por unidad, no promedios de uso — multiplica por el límite de cuota que quieras simular abajo.`}
          </p>
          <div className="stat-card-grid">
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Voice (Realtime) / minute' : 'Voz (Realtime) / minuto'}</p>
              <h3>{formatUsd(buddyBaseline.voicePerMinuteUsd)}</h3>
            </div>
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Text input / 1M tokens (gpt-5-mini)' : 'Texto entrada / 1M tokens (gpt-5-mini)'}</p>
              <h3>{formatUsd(buddyBaseline.textInputPerMillionTokensUsd)}</h3>
            </div>
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Text output / 1M tokens (gpt-5-mini)' : 'Texto salida / 1M tokens (gpt-5-mini)'}</p>
              <h3>{formatUsd(buddyBaseline.textOutputPerMillionTokensUsd)}</h3>
            </div>
          </div>
        </section>

        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish ? 'Step 3 cost breakdown — Buddy at max quota, all employees' : 'Paso 3 — desglose de costo de Buddy al máximo de cuota, todos los empleados'}
          </p>
          <table className="cost-table">
            <thead>
              <tr>
                <th>{isEnglish ? 'Component' : 'Componente'}</th>
                <th>{isEnglish ? 'Est. cost' : 'Costo est.'}</th>
              </tr>
            </thead>
            <tbody>
              <tr><td>{isEnglish ? `Voice (${totalEmployees} employee(s) × ${voiceMinutesPerEmployee} min)` : `Voz (${totalEmployees} empleado(s) × ${voiceMinutesPerEmployee} min)`}</td><td>{formatUsd(buddyVoiceCost)}</td></tr>
              <tr><td>{isEnglish ? `Text input (${totalEmployees} employee(s) × ${textInputTokensPerEmployee.toLocaleString()} tokens)` : `Texto entrada (${totalEmployees} empleado(s) × ${textInputTokensPerEmployee.toLocaleString()} tokens)`}</td><td>{formatUsd(buddyTextInputCost)}</td></tr>
              <tr><td>{isEnglish ? `Text output (${totalEmployees} employee(s) × ${textOutputTokensPerEmployee.toLocaleString()} tokens)` : `Texto salida (${totalEmployees} empleado(s) × ${textOutputTokensPerEmployee.toLocaleString()} tokens)`}</td><td>{formatUsd(buddyTextOutputCost)}</td></tr>
            </tbody>
          </table>
          <div className="stat-card stat-card-wide tenant-section-gap">
            <p className="customer-label">{isEnglish ? 'Total Buddy cost (all employees / month)' : 'Costo total de Buddy (todos los empleados / mes)'}</p>
            <h3>{formatUsd(totalBuddyCost)}</h3>
          </div>
        </section>
      </>
    ) : step === 4 ? (
      runtimeError ? <div className="data-message error">{runtimeError}</div>
      : runtimeBaseline === null ? <div className="data-message">{isEnglish ? 'Loading real runtime cost rates...' : 'Cargando tarifas reales de runtime...'}</div>
      : <>
        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish
              ? `Real STUDENT runtime $ figures (same rates as Step 2) — reused here as a PROXY for Brain's "Reactivate Engram" full-lesson review, since that feature doesn't record its own call duration/tokens today (unlike Voice Professor/Buddy). "Reactivate Engram" replays the ENTIRE Engram (every pedagogy stage), so it's priced the same as taking it once.`
              : `Cifras $ reales de un ESTUDIANTE (mismas tarifas del Paso 2) — reutilizadas aquí como PROXY para el repaso completo "Reactivar Engrama" de Brain, ya que esa función no registra su propia duración de llamada/tokens hoy (a diferencia de Voice Professor/Buddy). "Reactivar Engrama" recorre TODO el Engram de nuevo (cada etapa pedagógica), así que se cobra igual que tomarlo una vez.`}
          </p>
          <div className="stat-card-grid">
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Full Engram review — with voice' : 'Repaso completo de Engram — con voz'}</p>
              <h3>{formatUsd((runtimeBaseline?.costPerEngramWithVoice ?? 0) + (runtimeBaseline?.costPerRecall ?? 0) + (runtimeBaseline?.costPerAssessment ?? 0) + (runtimeBaseline?.costPerInYourRoleRun ?? 0))}</h3>
            </div>
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Full Engram review — text only' : 'Repaso completo de Engram — solo texto'}</p>
              <h3>{formatUsd((runtimeBaseline?.costPerEngramTextOnly ?? 0) + (runtimeBaseline?.costPerRecall ?? 0) + (runtimeBaseline?.costPerAssessment ?? 0) + (runtimeBaseline?.costPerInYourRoleRun ?? 0))}</h3>
            </div>
          </div>
        </section>

        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish ? 'Step 4 cost breakdown — Brain "Reactivate Engram", all employees' : 'Paso 4 — desglose de costo de Brain "Reactivar Engrama", todos los empleados'}
          </p>
          <table className="cost-table">
            <thead>
              <tr>
                <th>{isEnglish ? 'Component' : 'Componente'}</th>
                <th>{isEnglish ? 'Est. cost' : 'Costo est.'}</th>
              </tr>
            </thead>
            <tbody>
              <tr><td>{isEnglish ? `Engram reviews (${totalEmployees} employee(s) × ${engramReviewsPerEmployee} review(s))` : `Repasos de Engram (${totalEmployees} empleado(s) × ${engramReviewsPerEmployee} repaso(s))`}</td><td>{formatUsd(totalBrainReviewCost)}</td></tr>
            </tbody>
          </table>
          <div className="stat-card stat-card-wide tenant-section-gap">
            <p className="customer-label">{isEnglish ? 'Total Brain review cost (all employees / month)' : 'Costo total de repasos Brain (todos los empleados / mes)'}</p>
            <h3>{formatUsd(totalBrainReviewCost)}</h3>
          </div>
        </section>
      </>
    ) : step === 5 ? (
      aiAgentError ? <div className="data-message error">{aiAgentError}</div>
      : aiAgentBaseline === null ? <div className="data-message">{isEnglish ? 'Loading real AI Agent cost rates...' : 'Cargando tarifas reales de AI Agent...'}</div>
      : <>
        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish
              ? `Real "AI Agents"/"Grow With Us" $ rates (gpt-5-mini, same as Buddy) — the scenario below prices each conversation as input/output TOKENS (admin-entered) × these real per-million-token rates, rather than a single blended $/message average. Real average reference: ${aiAgentBaseline.costPerTextMessage !== null ? formatUsd(aiAgentBaseline.costPerTextMessage) + (aiAgentBaseline.usedBuddyProxy ? ' (Buddy proxy — AI Agents has no usage of its own yet)' : ` (from ${aiAgentBaseline.sampledTextMessageCount} real AI Agents chat turn(s))`) : 'not available yet'}.`
              : `Tarifas $ reales de "AI Agents"/"Grow With Us" (gpt-5-mini, igual que Buddy) — el escenario de abajo cobra cada conversación como TOKENS de entrada/salida (editables) × estas tarifas reales por millón de tokens, en vez de un solo promedio $/mensaje. Referencia de promedio real: ${aiAgentBaseline.costPerTextMessage !== null ? formatUsd(aiAgentBaseline.costPerTextMessage) + (aiAgentBaseline.usedBuddyProxy ? ' (proxy de Buddy — AI Agents aún no tiene uso propio)' : ` (de ${aiAgentBaseline.sampledTextMessageCount} turno(s) de chat real(es) de AI Agents)`) : 'no disponible todavía'}.`}
          </p>
          <div className="stat-card-grid">
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Text input / 1M tokens (gpt-5-mini)' : 'Texto entrada / 1M tokens (gpt-5-mini)'}</p>
              <h3>{formatUsd(aiAgentBaseline.textInputPerMillionTokensUsd)}</h3>
            </div>
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Text output / 1M tokens (gpt-5-mini)' : 'Texto salida / 1M tokens (gpt-5-mini)'}</p>
              <h3>{formatUsd(aiAgentBaseline.textOutputPerMillionTokensUsd)}</h3>
            </div>
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Voice (Realtime) / minute' : 'Voz (Realtime) / minuto'}</p>
              <h3>{formatUsd(aiAgentBaseline.voicePerMinuteUsd)}</h3>
            </div>
          </div>
        </section>

        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish ? `Est. cost per conversation — ${aiAgentInputTokensPerConversation.toLocaleString()} input + ${aiAgentOutputTokensPerConversation.toLocaleString()} output tokens` : `Costo est. por conversación — ${aiAgentInputTokensPerConversation.toLocaleString()} tokens de entrada + ${aiAgentOutputTokensPerConversation.toLocaleString()} de salida`}
          </p>
          <table className="cost-table">
            <thead>
              <tr>
                <th>{isEnglish ? 'Component' : 'Componente'}</th>
                <th>{isEnglish ? 'Est. cost' : 'Costo est.'}</th>
              </tr>
            </thead>
            <tbody>
              <tr><td>{isEnglish ? `Input cost (${aiAgentInputTokensPerConversation.toLocaleString()} tokens × $${aiAgentBaseline.textInputPerMillionTokensUsd}/1M)` : `Costo entrada (${aiAgentInputTokensPerConversation.toLocaleString()} tokens × $${aiAgentBaseline.textInputPerMillionTokensUsd}/1M)`}</td><td>{formatUsd(costInPerConversation)}</td></tr>
              <tr><td>{isEnglish ? `Output cost (${aiAgentOutputTokensPerConversation.toLocaleString()} tokens × $${aiAgentBaseline.textOutputPerMillionTokensUsd}/1M)` : `Costo salida (${aiAgentOutputTokensPerConversation.toLocaleString()} tokens × $${aiAgentBaseline.textOutputPerMillionTokensUsd}/1M)`}</td><td>{formatUsd(costOutPerConversation)}</td></tr>
            </tbody>
          </table>
          <div className="stat-card stat-card-wide tenant-section-gap">
            <p className="customer-label">{isEnglish ? 'Total cost / conversation' : 'Costo total / conversación'}</p>
            <h3>{formatUsd(costPerConversation)}</h3>
          </div>
        </section>

        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish ? 'Step 5 cost breakdown — AI Agents chat, all employees' : 'Paso 5 — desglose de costo del chat de AI Agents, todos los empleados'}
          </p>
          <table className="cost-table">
            <thead>
              <tr>
                <th>{isEnglish ? 'Component' : 'Componente'}</th>
                <th>{isEnglish ? 'Est. cost' : 'Costo est.'}</th>
              </tr>
            </thead>
            <tbody>
              <tr><td>{isEnglish ? `Text messages (${totalEmployees} employee(s) × ${aiAgentMessagesPerEmployeeDay}/day × ${aiAgentWorkDaysPerMonth} day(s))` : `Mensajes de texto (${totalEmployees} empleado(s) × ${aiAgentMessagesPerEmployeeDay}/día × ${aiAgentWorkDaysPerMonth} día(s))`}</td><td>{formatUsd(aiAgentTextCost)}</td></tr>
              <tr><td>{isEnglish ? `Voice (${totalEmployees} employee(s) × ${aiAgentVoiceMinutesPerEmployeeDay} min/day × ${aiAgentWorkDaysPerMonth} day(s))` : `Voz (${totalEmployees} empleado(s) × ${aiAgentVoiceMinutesPerEmployeeDay} min/día × ${aiAgentWorkDaysPerMonth} día(s))`}</td><td>{formatUsd(aiAgentVoiceCost)}</td></tr>
            </tbody>
          </table>
          <div className="stat-card stat-card-wide tenant-section-gap">
            <p className="customer-label">{isEnglish ? 'Total AI Agents cost (all employees / month)' : 'Costo total de AI Agents (todos los empleados / mes)'}</p>
            <h3>{formatUsd(totalAiAgentCost)}</h3>
          </div>
        </section>
      </>
    ) : step === 7 ? (
      <>
        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish
              ? `Real Azure billing snapshot — queried live from Azure Cost Management (resource group "HumanOSProdc" + the HumanOSDev database), captured ${AZURE_FIXED_COST_SNAPSHOT.snapshotDate} (${AZURE_FIXED_COST_SNAPSHOT.daysElapsed} day(s) of actual spend this month). NOT a SKU-rate guess. Both Function Apps run on FlexConsumption (true pay-per-execution, $0 idle) and the SQL database is Serverless with auto-pause — "fixed" here means "what it costs to simply exist", not a flat monthly fee.`
              : `Snapshot real de facturación de Azure — consultado en vivo desde Azure Cost Management (resource group "HumanOSProdc" + la base de datos HumanOSDev), capturado el ${AZURE_FIXED_COST_SNAPSHOT.snapshotDate} (${AZURE_FIXED_COST_SNAPSHOT.daysElapsed} día(s) de gasto real este mes). NO es una tarifa de SKU adivinada. Ambos Function Apps corren en FlexConsumption (pago real por ejecución, $0 en reposo) y la base SQL es Serverless con auto-pausa — "fijo" aquí significa "lo que cuesta simplemente existir", no una cuota mensual plana.`}
          </p>
          <table className="cost-table">
            <thead>
              <tr>
                <th>{isEnglish ? 'Resource' : 'Recurso'}</th>
                <th>{isEnglish ? 'Month-to-date' : 'Mes a la fecha'}</th>
                <th>{isEnglish ? 'Projected / 30 days' : 'Proyectado / 30 días'}</th>
              </tr>
            </thead>
            <tbody>
              {AZURE_FIXED_COST_SNAPSHOT.rows.map((row) => (
                <tr key={row.labelEn}>
                  <td>{isEnglish ? row.labelEn : row.labelEs}</td>
                  <td>{formatUsd(row.mtd)}</td>
                  <td>{formatUsd(row.mtd * 30 / AZURE_FIXED_COST_SNAPSHOT.daysElapsed)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="stat-card stat-card-wide tenant-section-gap">
            <p className="customer-label">{isEnglish ? 'Total projected fixed Azure cost / month' : 'Costo fijo total proyectado de Azure / mes'}</p>
            <h3>{formatUsd(AZURE_FIXED_COST_SNAPSHOT.rows.reduce((sum, r) => sum + r.mtd, 0) * 30 / AZURE_FIXED_COST_SNAPSHOT.daysElapsed)}</h3>
          </div>
        </section>
      </>
    ) : step === 8 ? (
      <>
        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish
              ? 'Real Azure Retail Prices API rates (eastus) for the INFRASTRUCTURE layer that scales with traffic — Function executions, Storage transactions, SQL Serverless compute, and Internet egress. Deliberately EXCLUDES AI/token costs (already counted in Steps 1-5 via the same Cognitive Services accounts — this avoids double-counting). Quantities below are DERIVED from the interactions already defined in Steps 1/2/4/5 (Buddy/Step 3 excluded — it has no message-count input to derive from). SQL active hours scale with volume, grounded in the REAL 60-minute auto-pause setting confirmed on flatsqlserver/HumanOSDev (az sql db show) — see the Step 8 breakdown below.'
              : 'Tarifas reales de Azure Retail Prices API (eastus) para la capa de INFRAESTRUCTURA que escala con el tráfico — ejecuciones de Functions, transacciones de Storage, cómputo SQL Serverless, y salida de red (egress). EXCLUYE deliberadamente el costo de IA/tokens (ya contado en los Pasos 1-5 vía los mismos recursos de Cognitive Services — evita duplicar). Las cantidades de abajo se DERIVAN de las interacciones ya definidas en los Pasos 1/2/4/5 (Buddy/Paso 3 excluido — no tiene un input de cantidad de mensajes del cual derivar). Las horas de SQL activas ahora SÍ escalan con el volumen, fundamentado en el ajuste REAL de auto-pausa de 60 minutos confirmado en flatsqlserver/HumanOSDev (az sql db show) — ver desglose del Paso 8 abajo.'}
          </p>
          <div className="stat-card-grid">
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Function / execution' : 'Function / ejecución'}</p>
              <h3 style={{ fontSize: 18, whiteSpace: 'nowrap' }}>{formatUsdPrecise(AZURE_VARIABLE_RATES.functionPerExecutionUsd)}</h3>
            </div>
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Function / GB-second' : 'Function / GB-segundo'}</p>
              <h3 style={{ fontSize: 18, whiteSpace: 'nowrap' }}>{formatUsdPrecise(AZURE_VARIABLE_RATES.functionPerGbSecondUsd)}</h3>
            </div>
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'SQL Serverless / vCore-hour' : 'SQL Serverless / vCore-hora'}</p>
              <h3>{formatUsd(AZURE_VARIABLE_RATES.sqlServerlessPerVCoreHourUsd)}</h3>
            </div>
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Egress / GB (after 100GB free)' : 'Egress / GB (tras 100GB gratis)'}</p>
              <h3>{formatUsd(AZURE_VARIABLE_RATES.egressPerGbUsd)}</h3>
            </div>
          </div>
        </section>

        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish ? 'Step 8 cost breakdown — Azure variable infrastructure' : 'Paso 8 — desglose de costo de infraestructura variable de Azure'}
          </p>
          <table className="cost-table">
            <thead>
              <tr>
                <th>{isEnglish ? 'Component' : 'Componente'}</th>
                <th>{isEnglish ? 'Est. cost' : 'Costo est.'}</th>
              </tr>
            </thead>
            <tbody>
              <tr><td>{isEnglish ? `Function executions (${Math.round(totalInteractions * functionExecutionsPerInteraction).toLocaleString()} exec(s))` : `Ejecuciones de Function (${Math.round(totalInteractions * functionExecutionsPerInteraction).toLocaleString()} ejec.)`}</td><td>{formatUsd(azureFunctionExecutionsCost)}</td></tr>
              <tr><td>{isEnglish ? `Function compute (GB-seconds)` : `Cómputo de Function (GB-segundos)`}</td><td>{formatUsd(azureFunctionComputeCost)}</td></tr>
              <tr><td>{isEnglish ? `Storage writes/reads/data (${totalImages.toLocaleString()} image(s))` : `Escrituras/lecturas/datos Storage (${totalImages.toLocaleString()} imagen(es))`}</td><td>{formatUsd(azureStorageCost)}</td></tr>
              <tr><td>{isEnglish ? `Network egress (${billableEgressGb.toFixed(2)} billable GB of ${egressGb.toFixed(2)} GB, 100GB free)` : `Salida de red (${billableEgressGb.toFixed(2)} GB facturables de ${egressGb.toFixed(2)} GB, 100GB gratis)`}</td><td>{formatUsd(azureEgressCost)}</td></tr>
              <tr><td>{isEnglish ? `SQL Serverless compute (${sqlActiveHours.toFixed(1)} active hour(s) = min(${sqlActiveHoursPerWorkDay} ceiling, ${interactionsPerWorkDay.toFixed(1)} interactions/day × 1h auto-pause) × ${aiAgentWorkDaysPerMonth} work days)` : `Cómputo SQL Serverless (${sqlActiveHours.toFixed(1)} hora(s) activa(s) = mín(${sqlActiveHoursPerWorkDay} techo, ${interactionsPerWorkDay.toFixed(1)} interacciones/día × 1h auto-pausa) × ${aiAgentWorkDaysPerMonth} días laborables)`}</td><td>{formatUsd(azureSqlCost)}</td></tr>
            </tbody>
          </table>
          <div className="stat-card stat-card-wide tenant-section-gap">
            <p className="customer-label">{isEnglish ? 'Total Azure variable cost / month' : 'Costo variable total de Azure / mes'}</p>
            <h3>{formatUsd(totalAzureVariableCost)}</h3>
          </div>
        </section>
      </>
    ) : step === 9 ? (
      <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
        <p className="customer-label" style={{ marginBottom: 14 }}>
          {isEnglish
            ? `Step 9 adds the company costs required to sell and operate the courses. Sales is ${(salesCostPercent * 100).toFixed(1)}% of non-Azure variable course costs (Steps 1-5); administration, marketing and salaries are calculated from the total after adding sales costs — all four percentages are editable above to simulate different cost structures.`
            : `El Paso 9 agrega los costos de empresa necesarios para vender y operar los cursos. Ventas es ${(salesCostPercent * 100).toFixed(1)}% de los costos variables no-Azure de los Pasos 1-5; administración, marketing y salarios se calculan sobre el total después de agregar ventas — los cuatro porcentajes son editables arriba para simular distintas estructuras de costo.`}
        </p>
        <table className="cost-table">
          <thead>
            <tr>
              <th>{isEnglish ? 'Company cost' : 'Costo de empresa'}</th>
              <th>{isEnglish ? 'Basis' : 'Base'}</th>
              <th>{isEnglish ? 'Est. cost' : 'Costo est.'}</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td>{isEnglish ? 'Sales / course selling costs' : 'Ventas / costos de vender cursos'}</td>
              <td>{isEnglish ? `${(salesCostPercent * 100).toFixed(1)}% of non-Azure costs (${formatUsd(nonAzureVariableCost)})` : `${(salesCostPercent * 100).toFixed(1)}% de costos no-Azure (${formatUsd(nonAzureVariableCost)})`}</td>
              <td>{formatUsd(salesCost)}</td>
            </tr>
            <tr>
              <td>{isEnglish ? 'Administration' : 'Administración'}</td>
              <td>{isEnglish ? `${(administrationCostPercent * 100).toFixed(1)}% of total after sales (${formatUsd(overheadBase)})` : `${(administrationCostPercent * 100).toFixed(1)}% del total después de ventas (${formatUsd(overheadBase)})`}</td>
              <td>{formatUsd(administrationCost)}</td>
            </tr>
            <tr>
              <td>{isEnglish ? 'Marketing' : 'Marketing'}</td>
              <td>{isEnglish ? `${(marketingCostPercent * 100).toFixed(1)}% of total after sales (${formatUsd(overheadBase)})` : `${(marketingCostPercent * 100).toFixed(1)}% del total después de ventas (${formatUsd(overheadBase)})`}</td>
              <td>{formatUsd(marketingCost)}</td>
            </tr>
            <tr>
              <td>{isEnglish ? 'Salaries' : 'Salarios'}</td>
              <td>{isEnglish ? `${(salariesCostPercent * 100).toFixed(1)}% of total after sales (${formatUsd(overheadBase)})` : `${(salariesCostPercent * 100).toFixed(1)}% del total después de ventas (${formatUsd(overheadBase)})`}</td>
              <td>{formatUsd(salariesCost)}</td>
            </tr>
          </tbody>
        </table>
        <div className="stat-card stat-card-wide tenant-section-gap">
          <p className="customer-label">{isEnglish ? 'Total company costs / month' : 'Costos totales de empresa / mes'}</p>
          <h3>{formatUsd(totalCompanyCosts)}</h3>
        </div>
      </section>
    ) : step === 10 ? (
      <>
        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish
              ? `Step 10 turns every cost (Steps 1-9, already including Azure fixed/variable infrastructure and company overhead) into a SALE PRICE that guarantees a real ${(PROFIT_MARGIN_RATE * 100).toFixed(1)}% profit margin — editable above, and stated as a % of the final sale price, not a markup on cost: price = cost ÷ (1 − margin). Taxes are intentionally left out of this calculation for now.`
              : `El Paso 10 convierte cada costo (Pasos 1-9, que ya incluyen infraestructura fija/variable de Azure y los costos de empresa) en un PRECIO DE VENTA que garantiza un margen de ganancia real del ${(PROFIT_MARGIN_RATE * 100).toFixed(1)}% — editable arriba, y expresado como % del precio final de venta, no como un recargo sobre el costo: precio = costo ÷ (1 − margen). Los impuestos se dejan fuera de este cálculo por ahora.`}
          </p>
          {PRICING_COST_SHARE <= 0 && (
            <p className="data-message error" style={{ marginBottom: 14 }}>
              {isEnglish
                ? 'Margin must be less than 100% of the sale price — adjust the value above.'
                : 'El margen debe ser menor al 100% del precio de venta — ajusta el valor de arriba.'}
            </p>
          )}
          <table className="cost-table">
            <thead>
              <tr>
                <th>{isEnglish ? 'Cost item' : 'Rubro de costo'}</th>
                <th>{isEnglish ? 'Cost' : 'Costo'}</th>
                <th>{isEnglish ? 'Sale price' : 'Precio de venta'}</th>
                <th>{isEnglish ? `Profit (${(PROFIT_MARGIN_RATE * 100).toFixed(1)}%)` : `Ganancia (${(PROFIT_MARGIN_RATE * 100).toFixed(1)}%)`}</th>
              </tr>
            </thead>
            <tbody>
              {pricingRows.map((row) => (
                <tr key={row.id}>
                  <td>{isEnglish ? row.labelEn : row.labelEs}</td>
                  <td>{formatUsd(row.cost)}</td>
                  <td><strong>{formatUsd(row.salePrice)}</strong></td>
                  <td>{formatUsd(row.profitAmount)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td><strong>{isEnglish ? 'Total (all steps)' : 'Total (todos los pasos)'}</strong></td>
                <td><strong>{formatUsd(totalCostAllSteps)}</strong></td>
                <td><strong>{formatUsd(totalSalePrice)}</strong></td>
                <td><strong>{formatUsd(totalProfitAmount)}</strong></td>
              </tr>
            </tfoot>
          </table>
        </section>

        <div className="stat-card stat-card-wide tenant-section-gap">
          <p className="customer-label">{isEnglish ? `Total sale price / month (${(PROFIT_MARGIN_RATE * 100).toFixed(1)}% margin)` : `Precio de venta total / mes (${(PROFIT_MARGIN_RATE * 100).toFixed(1)}% margen)`}</p>
          <h3>{formatUsd(totalSalePrice)}</h3>
        </div>

        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish
              ? 'Per-unit pricing — same cost → sale price logic, divided by the real quantities entered in each step (courses, Engrams, employees), to compare cost vs. proposed sale price per unit.'
              : 'Precio por unidad — misma lógica de costo → precio de venta, dividida entre las cantidades reales ingresadas en cada paso (cursos, Engrams, empleados), para comparar costo vs. precio de venta propuesto por unidad.'}
          </p>
          <table className="cost-table">
            <thead>
              <tr>
                <th>{isEnglish ? 'Unit' : 'Unidad'}</th>
                <th>{isEnglish ? 'Cost / unit' : 'Costo / unidad'}</th>
                <th>{isEnglish ? 'Sale price / unit' : 'Precio de venta / unidad'}</th>
                <th>{isEnglish ? `Profit / unit (${(PROFIT_MARGIN_RATE * 100).toFixed(1)}%)` : `Ganancia / unidad (${(PROFIT_MARGIN_RATE * 100).toFixed(1)}%)`}</th>
              </tr>
            </thead>
            <tbody>
              {unitPricingRows.map((row) => (
                <tr key={row.labelEn}>
                  <td>{isEnglish ? row.labelEn : row.labelEs}</td>
                  <td>{formatUsdPrecise(row.cost)}</td>
                  <td><strong>{formatUsdPrecise(row.salePrice)}</strong></td>
                  <td>{formatUsdPrecise(row.profitAmount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      </>
    ) : step === 11 ? (
      <>
        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish
              ? 'Client-facing rate plans, in plain business terms — each tier is additive (Professional includes everything in Basic, plus more; Enterprise includes everything in Professional, plus more), priced with the real cost + margin from Step 10, then rounded to a psychological price point (never below the computed floor).'
              : 'Paquetes para clientes, en términos de negocio simples — cada nivel es aditivo (Profesional incluye todo lo de Básico, más funciones; Empresarial incluye todo lo de Profesional, más funciones), cotizado con el costo real + margen del Paso 10, y luego redondeado a un precio psicológico (nunca por debajo del piso calculado).'}
          </p>
          <div style={{ display: 'flex', gap: 10, marginBottom: 18 }}>
            <button
              type="button"
              className={`tenant-subtab${billingCycle === 'yearly' ? ' active' : ''}`}
              onClick={() => setBillingCycle('yearly')}
            >
              {isEnglish ? `Yearly — ${(annualDiscountRate * 100).toFixed(0)}% off` : `Anual — ${(annualDiscountRate * 100).toFixed(0)}% off`}
            </button>
            <button
              type="button"
              className={`tenant-subtab${billingCycle === 'monthly' ? ' active' : ''}`}
              onClick={() => setBillingCycle('monthly')}
            >
              {isEnglish ? 'Monthly' : 'Mensual'}
            </button>
          </div>
          <div className="stat-card-grid">
            {ratePlansDisplay.map((plan) => (
              <div
                className="stat-card"
                key={plan.id}
                style={{
                  display: 'grid',
                  gap: 10,
                  border: plan.recommended ? '2px solid #1f8a4c' : undefined,
                  position: 'relative',
                }}
              >
                {plan.recommended && (
                  <span style={{
                    position: 'absolute', top: -11, right: 14, padding: '3px 10px',
                    background: '#1f8a4c', color: '#fff', borderRadius: 999,
                    fontSize: 10, fontWeight: 700, letterSpacing: '.04em', textTransform: 'uppercase',
                  }}>
                    {isEnglish ? 'Most popular' : 'Más popular'}
                  </span>
                )}
                <p className="customer-label" style={{ fontSize: 13, fontWeight: 700, color: '#1d3557', margin: 0 }}>{isEnglish ? plan.nameEn : plan.nameEs}</p>
                <p style={{ margin: 0, color: '#55708f', fontSize: 12, fontStyle: 'italic' }}>{isEnglish ? plan.taglineEn : plan.taglineEs}</p>

                {billingCycle === 'yearly' ? (
                  <div style={{ margin: '4px 0 0' }}>
                    <h3 style={{ margin: 0, color: '#1f8a4c', display: 'inline' }}>{formatUsd(plan.annualMonthlyEquivalent)}</h3>
                    <span style={{ fontSize: 12, fontWeight: 500, color: '#7b8da5' }}> USD/{isEnglish ? 'mo' : 'mes'} </span>
                    <span style={{ fontSize: 13, color: '#9aa7b8', textDecoration: 'line-through' }}>{formatUsd(plan.displayPrice)}</span>
                    <p style={{ margin: '2px 0 0', fontSize: 11, color: '#7b8da5' }}>{formatUsd(plan.annualTotalBilled)} {isEnglish ? 'billed annually' : 'facturado anualmente'}</p>
                  </div>
                ) : (
                  <h3 style={{ margin: '4px 0 0', color: '#1f8a4c' }}>{formatUsd(plan.displayPrice)}<span style={{ fontSize: 12, fontWeight: 500, color: '#7b8da5' }}> USD/{isEnglish ? 'mo' : 'mes'}</span></h3>
                )}
                <p style={{ margin: 0, fontSize: 11, color: '#9aa7b8' }}>{isEnglish ? '(whole company, not per person)' : '(toda la empresa, no por persona)'}</p>

                <div style={{ marginTop: 8, padding: '10px 12px', background: '#f7fafd', border: '1px solid #dce6f2', borderRadius: 10 }}>
                  <p className="customer-label" style={{ margin: '0 0 4px', fontSize: 10 }}>{isEnglish ? 'CREDITS' : 'CRÉDITOS'}</p>
                  <p style={{ margin: 0, fontWeight: 700, color: '#1d3557', fontSize: 13 }}>{plan.creditsIncluded.toLocaleString()} {isEnglish ? 'credits/mo' : 'créditos/mes'}</p>
                  <p style={{ margin: '4px 0 2px', fontSize: 10, fontWeight: 700, color: '#7b8da5', textTransform: 'uppercase', letterSpacing: '.03em' }}>
                    {isEnglish ? 'Every service draws from this SAME balance:' : 'Todo servicio consume de este MISMO saldo:'}
                  </p>
                  <ul style={{ margin: 0, paddingLeft: 16, color: '#55708f', fontSize: 11, lineHeight: 1.65 }}>
                    <li>
                      {isEnglish
                        ? `Create courses — up to ${plan.equivCoursesCreated.toLocaleString()} new courses (up to ${engramsPerCourse} Engrams each)`
                        : `Crear cursos — hasta ${plan.equivCoursesCreated.toLocaleString()} cursos nuevos (de hasta ${engramsPerCourse} Engrams cada uno)`}
                    </li>
                    <li>
                      {isEnglish
                        ? `Employees take courses — up to ${plan.equivCoursesTaken.toLocaleString()} courses completed`
                        : `Empleados toman cursos — hasta ${plan.equivCoursesTaken.toLocaleString()} cursos completados`}
                    </li>
                    <li>
                      {isEnglish
                        ? `Buddy (voice companion) — up to ${plan.equivBuddyMinutes.toLocaleString()} voice minutes`
                        : `Buddy (compañero de voz) — hasta ${plan.equivBuddyMinutes.toLocaleString()} minutos de voz`}
                    </li>
                    <li>
                      {isEnglish
                        ? `AI Agents (daily Q&A) — up to ${plan.equivAgentMessages.toLocaleString()} chat messages`
                        : `Agentes IA (dudas del día a día) — hasta ${plan.equivAgentMessages.toLocaleString()} mensajes de chat`}
                    </li>
                    <li>
                      {isEnglish
                        ? `My Brain (knowledge reviews) — up to ${plan.equivBrainReviews.toLocaleString()} reviews`
                        : `My Brain (repasos de conocimiento) — hasta ${plan.equivBrainReviews.toLocaleString()} repasos`}
                    </li>
                  </ul>
                  <p style={{ margin: '6px 0 0', fontSize: 10, color: '#9aa7b8', fontStyle: 'italic' }}>
                    {isEnglish
                      ? '* These are each the MAXIMUM if you spent ALL your credits on just that one service — in practice you mix and match across all five however your company needs, every month.'
                      : '* Cada uno es el MÁXIMO si gastaras TODOS tus créditos solo en ese servicio — en la práctica combinas los cinco como tu empresa los necesite, cada mes.'}
                  </p>
                  <p style={{ margin: '6px 0 0', fontSize: 11, color: '#55708f' }}>{isEnglish ? 'Buy more credit packs as needed (Step 12)' : 'Compra más bloques de créditos cuando los necesites (Paso 12)'}</p>
                </div>

                <p className="customer-label" style={{ margin: '8px 0 0', fontSize: 10 }}>{isEnglish ? 'FEATURES' : 'FEATURES'}</p>
                <ul style={{ margin: 0, paddingLeft: 18, color: '#55708f', fontSize: 12, lineHeight: 1.6 }}>
                  {(isEnglish ? plan.featuresEn : plan.featuresEs).map((feature) => (
                    <li key={feature}>{feature}</li>
                  ))}
                </ul>

                <p className="customer-label" style={{ margin: '8px 0 0', fontSize: 10 }}>{isEnglish ? 'BENEFITS' : 'BENEFICIOS'}</p>
                <ul style={{ margin: 0, paddingLeft: 18, color: '#55708f', fontSize: 12, lineHeight: 1.6 }}>
                  {(isEnglish ? plan.perksEn : plan.perksEs).map((perk) => (
                    <li key={perk}>{perk}</li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        </section>

        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <table className="cost-table">
            <thead>
              <tr>
                <th>{isEnglish ? 'What\'s included' : 'Qué incluye'}</th>
                {ratePlans.map((plan) => (
                  <th key={plan.id}>{isEnglish ? plan.nameEn : plan.nameEs}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {planFeatureRows.map((row) => (
                <tr key={row.labelEn}>
                  <td>{isEnglish ? row.labelEn : row.labelEs}</td>
                  {row.values.map((value, planIndex) => (
                    <td key={planIndex}>
                      {typeof value === 'boolean'
                        ? (value
                          ? <span style={{ color: '#1f8a4c', fontWeight: 700 }}>✓ {isEnglish ? 'Yes' : 'Sí'}</span>
                          : <span style={{ color: '#9aa7b8' }}>— {isEnglish ? 'No' : 'No'}</span>)
                        : value}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td><strong>{isEnglish ? 'Cost / month (whole company)' : 'Costo / mes (toda la empresa)'}</strong></td>
                {ratePlans.map((plan) => (
                  <td key={plan.id}><strong>{formatUsdPrecise(plan.cost)}</strong></td>
                ))}
              </tr>
              <tr>
                <td>{isEnglish ? `Profit (${(PROFIT_MARGIN_RATE * 100).toFixed(1)}%)` : `Ganancia (${(PROFIT_MARGIN_RATE * 100).toFixed(1)}%)`}</td>
                {ratePlans.map((plan) => (
                  <td key={plan.id}>{formatUsdPrecise(plan.profitAmount)}</td>
                ))}
              </tr>
              <tr>
                <td>{isEnglish ? 'Exact computed sale price' : 'Precio de venta exacto calculado'}</td>
                {ratePlans.map((plan) => (
                  <td key={plan.id}>{formatUsdPrecise(plan.salePrice)}</td>
                ))}
              </tr>
              <tr>
                <td><strong>{isEnglish ? 'Final price (psychological rounding)' : 'Precio final (redondeo psicológico)'}</strong></td>
                {ratePlans.map((plan) => (
                  <td key={plan.id}><strong style={{ color: '#1f8a4c' }}>{formatUsd(plan.displayPrice)}</strong></td>
                ))}
              </tr>
            </tfoot>
          </table>
        </section>

        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish
              ? `Pay-as-you-go — extra courses beyond what a plan already includes, priced individually, or as a one-shot "unlock the full catalog" add-on. The catalog has ${totalCourses} course(s) total.`
              : `Pay-as-you-go — cursos adicionales más allá de lo que ya incluye cada plan, cobrados de forma individual, o como un add-on único de "desbloquear el catálogo completo". El catálogo tiene ${totalCourses} curso(s) en total.`}
          </p>
          <table className="cost-table">
            <thead>
              <tr>
                <th>{isEnglish ? 'Overflow pricing' : 'Precio por excedente'}</th>
                {ratePlans.map((plan) => (
                  <th key={plan.id}>{isEnglish ? plan.nameEn : plan.nameEs}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>{isEnglish ? 'Price per individual extra course' : 'Precio por curso individual adicional'}</td>
                {ratePlans.map((plan) => (
                  <td key={plan.id}><strong style={{ color: '#1f8a4c' }}>{formatUsdPrecise(pricePerExtraCourse)}</strong></td>
                ))}
              </tr>
              <tr>
                <td>{isEnglish ? 'Extra courses needed to unlock the full catalog' : 'Cursos adicionales para desbloquear el catálogo completo'}</td>
                {ratePlans.map((plan) => (
                  <td key={plan.id}>{plan.extraCoursesToFullCatalog} {isEnglish ? 'course(s)' : 'curso(s)'}{plan.extraCoursesToFullCatalog === 0 ? (isEnglish ? ' (already full catalog)' : ' (ya tiene el catálogo completo)') : ''}</td>
                ))}
              </tr>
              <tr>
                <td><strong>{isEnglish ? 'Add-on price: unlock full catalog' : 'Precio del add-on: desbloquear catálogo completo'}</strong></td>
                {ratePlans.map((plan) => (
                  <td key={plan.id}><strong style={{ color: '#1f8a4c' }}>{formatUsdPrecise(plan.fullCatalogUnlockSalePrice)}</strong></td>
                ))}
              </tr>
            </tbody>
          </table>
        </section>
      </>
    ) : step === 12 ? (
      <>
        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish
              ? 'What does the client want to do this month? Type ANY combination of quantities above — there is no single "correct" usage pattern. Each row shows the credits, cost to us, and sale price for that piece; the totals below are the combined result of everything typed.'
              : '¿Qué quiere hacer el cliente este mes? Escribe CUALQUIER combinación de cantidades arriba — no hay un único patrón de uso "correcto". Cada fila muestra los créditos, el costo para nosotros y el precio de venta de esa pieza; los totales de abajo son el resultado combinado de todo lo escrito.'}
          </p>
          <table className="cost-table">
            <thead>
              <tr>
                <th>{isEnglish ? 'Service' : 'Servicio'}</th>
                <th>{isEnglish ? 'Quantity' : 'Cantidad'}</th>
                <th>{isEnglish ? 'Credits / unit' : 'Créditos / unidad'}</th>
                <th>{isEnglish ? 'Credits required' : 'Créditos requeridos'}</th>
                <th>{isEnglish ? 'Cost to FlatBit' : 'Costo para FlatBit'}</th>
                <th>{isEnglish ? 'Sale price' : 'Precio de venta'}</th>
              </tr>
            </thead>
            <tbody>
              {simRows.map((row) => (
                <tr key={row.labelEn}>
                  <td>{isEnglish ? row.labelEn : row.labelEs}</td>
                  <td>{row.quantity.toLocaleString()}</td>
                  <td>{row.creditsEach.toLocaleString()}</td>
                  <td><strong>{row.creditsUsed.toLocaleString()}</strong></td>
                  <td>{formatUsd(row.costToUs)}</td>
                  <td>{formatUsd(row.salePrice)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td colSpan={3}><strong>{isEnglish ? 'Combined total — exact need (no margin yet)' : 'Total combinado — necesidad exacta (sin margen aún)'}</strong></td>
                <td><strong style={{ color: '#1f8a4c' }}>{simTotalCreditsNeeded.toLocaleString()}</strong></td>
                <td><strong>{formatUsd(simTotalCreditsNeeded * costPerCreditUsd)}</strong></td>
                <td><strong>{formatUsd(simTotalCreditsNeeded * creditValueUsd)}</strong></td>
              </tr>
            </tfoot>
          </table>
        </section>

        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish
              ? `Credits to purchase — the exact need (${simTotalCreditsNeeded.toLocaleString()} credits) PLUS a ${(creditSafetyMarginRate * 100).toFixed(0)}% safety margin (editable above), so the client doesn't run dry mid-month.`
              : `Créditos a comprar — la necesidad exacta (${simTotalCreditsNeeded.toLocaleString()} créditos) MÁS un margen de seguridad del ${(creditSafetyMarginRate * 100).toFixed(0)}% (editable arriba), para que el cliente no se quede corto a mitad de mes.`}
          </p>
          <div className="stat-card-grid">
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Credits needed (exact)' : 'Créditos necesarios (exacto)'}</p>
              <h3>{simTotalCreditsNeeded.toLocaleString()}</h3>
            </div>
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? `+ Safety buffer (${(creditSafetyMarginRate * 100).toFixed(0)}%)` : `+ Colchón de seguridad (${(creditSafetyMarginRate * 100).toFixed(0)}%)`}</p>
              <h3>{creditsSafetyBuffer.toLocaleString()}</h3>
            </div>
            <div className="stat-card" style={{ border: '2px solid #1f5bb7' }}>
              <p className="customer-label">{isEnglish ? '= Credits to purchase' : '= Créditos a comprar'}</p>
              <h3 style={{ color: '#1f5bb7' }}>{creditsToPurchase.toLocaleString()}</h3>
            </div>
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Cost to FlatBit' : 'Costo para FlatBit'}</p>
              <h3>{formatUsd(purchaseCostToUs)}</h3>
            </div>
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Sale price (what the client pays)' : 'Precio de venta (lo que paga el cliente)'}</p>
              <h3 style={{ color: '#1f8a4c' }}>{formatUsd(purchaseSalePrice)}</h3>
            </div>
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? `Our profit (${(PROFIT_MARGIN_RATE * 100).toFixed(1)}% margin)` : `Nuestra ganancia (${(PROFIT_MARGIN_RATE * 100).toFixed(1)}% margen)`}</p>
              <h3 style={{ color: '#1f8a4c' }}>{formatUsd(purchaseProfit)}</h3>
            </div>
          </div>
        </section>

        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish
              ? `Reference — what the ${creditsToPurchase.toLocaleString()} purchased credits could cover IF ALL of them went to just one service, instead of the mix above (sanity check only).`
              : `Referencia — lo que cubrirían los ${creditsToPurchase.toLocaleString()} créditos comprados SI TODOS fueran a un solo servicio, en vez de la mezcla de arriba (solo para verificar).`}
          </p>
          <table className="cost-table">
            <thead>
              <tr>
                <th>{isEnglish ? 'Service' : 'Servicio'}</th>
                <th>{isEnglish ? 'Credits / unit' : 'Créditos / unidad'}</th>
                <th>{isEnglish ? 'Max if spent only here' : 'Máximo si se gasta solo aquí'}</th>
              </tr>
            </thead>
            <tbody>
              {purchaseEquivalenceRows.map((row) => (
                <tr key={row.labelEn}>
                  <td>{isEnglish ? row.labelEn : row.labelEs}</td>
                  <td>{row.creditsEach.toLocaleString()} {isEnglish ? 'credits' : 'créditos'}</td>
                  <td><strong style={{ color: '#1f8a4c' }}>{row.maxIfOnlyThis.toLocaleString()}</strong></td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>

        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish
              ? `Credit system — 1 credit = ${formatUsdPrecise(creditValueUsd)} USD (editable above). Every interaction's REAL $ cost is scaled up by a load factor of ${costLoadFactor.toFixed(2)}x (Azure infrastructure, Steps 7-8, + company overhead: sales, admin, marketing, salaries, Step 9) PLUS a ${(wasteBufferRate * 100).toFixed(1)}% waste/retry buffer (failed generations, regenerations — same idea Sora/Runway/HeyGen bake into their credit price), all BEFORE the ${(PROFIT_MARGIN_RATE * 100).toFixed(1)}% profit margin. Credits are always rounded UP (never give away a fraction).`
              : `Sistema de créditos — 1 crédito = ${formatUsdPrecise(creditValueUsd)} USD (editable arriba). El costo $ REAL de cada interacción se multiplica por un factor de carga de ${costLoadFactor.toFixed(2)}x (infraestructura de Azure, Pasos 7-8, + gastos de empresa: ventas, administración, marketing, salarios, Paso 9) MÁS un buffer de desperdicio/reintentos del ${(wasteBufferRate * 100).toFixed(1)}% (generaciones fallidas, regeneraciones — la misma idea que Sora/Runway/HeyGen integran en su precio de crédito), todo ANTES del margen de ganancia del ${(PROFIT_MARGIN_RATE * 100).toFixed(1)}%. Los créditos siempre se redondean HACIA ARRIBA (nunca se regala una fracción).`}
          </p>
          <table className="cost-table">
            <thead>
              <tr>
                <th>{isEnglish ? 'Billable interaction' : 'Interacción facturable'}</th>
                <th>{isEnglish ? 'Real $ cost' : 'Costo $ real'}</th>
                <th>{isEnglish ? `Loaded cost (×${costLoadFactor.toFixed(2)} infra/overhead, +${(wasteBufferRate * 100).toFixed(0)}% waste)` : `Costo cargado (×${costLoadFactor.toFixed(2)} infra/gastos, +${(wasteBufferRate * 100).toFixed(0)}% desperdicio)`}</th>
                <th>{isEnglish ? 'Sale price ($)' : 'Precio de venta ($)'}</th>
                <th>{isEnglish ? 'Credits' : 'Créditos'}</th>
              </tr>
            </thead>
            <tbody>
              {creditPriceRows.map((row) => (
                <tr key={row.labelEn}>
                  <td>{isEnglish ? row.labelEn : row.labelEs}</td>
                  <td>{formatUsdPrecise(row.rawCost)}</td>
                  <td>{formatUsdPrecise(row.loadedCost)}</td>
                  <td>{formatUsdPrecise(row.salePrice)}</td>
                  <td><strong style={{ color: '#1f8a4c' }}>{row.credits} {isEnglish ? (row.credits === 1 ? 'credit' : 'credits') : (row.credits === 1 ? 'crédito' : 'créditos')}</strong></td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>

        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish
              ? `Pre-paid bulk blocks — a ${(bulkBlockDiscountRate * 100).toFixed(1)}% volume discount (editable above) vs. buying the same quantity à la carte, one at a time. This is what makes a "block" a real deal instead of being mathematically identical to N individual purchases.`
              : `Bloques prepago — un ${(bulkBlockDiscountRate * 100).toFixed(1)}% de descuento por volumen (editable arriba) frente a comprar la misma cantidad a la carta, uno por uno. Esto es lo que hace que un "bloque" sea una oferta real, en vez de ser matemáticamente idéntico a N compras individuales.`}
          </p>
          <table className="cost-table">
            <thead>
              <tr>
                <th>{isEnglish ? 'Pre-paid block' : 'Bloque prepago'}</th>
                <th>{isEnglish ? 'À la carte total (credits)' : 'Total a la carta (créditos)'}</th>
                <th>{isEnglish ? 'Block price (credits)' : 'Precio del bloque (créditos)'}</th>
                <th>{isEnglish ? 'Savings' : 'Ahorro'}</th>
              </tr>
            </thead>
            <tbody>
              {bulkBlocks.map((block) => (
                <tr key={block.labelEn}>
                  <td>{isEnglish ? block.labelEn : block.labelEs}</td>
                  <td>{block.aLaCarteTotalCredits.toLocaleString()} {isEnglish ? 'credits' : 'créditos'}</td>
                  <td><strong style={{ color: '#1f8a4c' }}>{block.blockCredits.toLocaleString()} {isEnglish ? 'credits' : 'créditos'}</strong></td>
                  <td>{block.savedCredits.toLocaleString()} {isEnglish ? 'credits saved' : 'créditos ahorrados'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>

        <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
          <p className="customer-label" style={{ marginBottom: 14 }}>
            {isEnglish
              ? 'Packages (Step 11), expressed in credits/month — what a client actually sees on their invoice or dashboard. Based on the real psychological price charged (Step 11\'s rounded final price), floored to credits so we never hand out more than what was paid for.'
              : 'Paquetes (Paso 11), expresados en créditos/mes — lo que un cliente realmente ve en su factura o panel. Basado en el precio psicológico real cobrado (precio final redondeado del Paso 11), redondeado hacia abajo a créditos para nunca entregar más de lo que se pagó.'}
          </p>
          <table className="cost-table">
            <thead>
              <tr>
                <th>{isEnglish ? 'Plan' : 'Plan'}</th>
                <th>{isEnglish ? 'Final price / month' : 'Precio final / mes'}</th>
                <th>{isEnglish ? 'Credits included / month' : 'Créditos incluidos / mes'}</th>
              </tr>
            </thead>
            <tbody>
              {ratePlansInCredits.map((plan) => (
                <tr key={plan.id}>
                  <td>{isEnglish ? plan.nameEn : plan.nameEs}</td>
                  <td>{formatUsd(plan.displayPrice)}</td>
                  <td><strong style={{ color: '#1f8a4c' }}>{plan.creditsIncluded.toLocaleString()} {isEnglish ? 'credits' : 'créditos'}</strong></td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      </>
    ) : (
      <section className="cost-table-wrap tenant-section-gap" style={{ padding: 40, textAlign: 'center' }}>
        <FlaskConical size={32} style={{ margin: '0 auto 14px', opacity: 0.4 }} />
        <h3 style={{ marginBottom: 8 }}>{isEnglish ? 'Labs' : 'Labs'}</h3>
        <p className="customer-label">
          {isEnglish ? 'Coming soon — Labs usage cost simulation.' : 'Próximamente — simulación de costo de uso de Labs.'}
        </p>
      </section>
    )}

    <section className="stat-card stat-card-wide tenant-section-gap">
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, marginBottom: 10 }}>
        <p className="customer-label" style={{ margin: 0 }}>
          {isEnglish ? 'Running summary (in-memory, accumulates across steps)' : 'Resumen acumulado (en memoria, suma entre pasos)'}
        </p>
        <button
          type="button"
          onClick={recalculateAll}
          style={{
            background: '#2563eb',
            color: '#fff',
            border: 'none',
            borderRadius: 8,
            padding: '8px 18px',
            fontWeight: 600,
            cursor: 'pointer',
          }}
        >
          {isEnglish ? 'Calculate' : 'Calcular'}
        </button>
      </div>
      <table className="cost-table">
        <thead>
          <tr>
            <th>{isEnglish ? 'Step' : 'Paso'}</th>
            <th>{isEnglish ? 'Subtotal' : 'Subtotal'}</th>
            <th>{isEnglish ? 'Accumulated total' : 'Total acumulado'}</th>
          </tr>
        </thead>
        <tbody>
          {summaryRows.map((row) => (
            <tr key={row.id}>
              <td>{isEnglish ? row.labelEn : row.labelEs}{row.id === step ? (isEnglish ? ' (current)' : ' (actual)') : ''}</td>
              <td>{formatUsd(row.subtotal)}</td>
              <td>{formatUsd(row.cumulative)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <h3 style={{ marginTop: 10 }}>{isEnglish ? `Grand total so far: ${formatUsd(grandTotal)}` : `Gran total hasta ahora: ${formatUsd(grandTotal)}`}</h3>
    </section>
      </div>
    </div>
  </>;
}

function BusinessCasePage({ isEnglish }: { isEnglish: boolean }) {
  // A standalone, Excel-style monthly business case across 12 months,
  // navigated via a left-hand month submenu (same pattern as the
  // Simulations step sidebar). Reuses the SAME credit economics already
  // modeled in Simulations (ACPU — Average Credits Per Unit/customer,
  // credit sale price, Azure fixed/variable costs, company overhead as %
  // of cost base — sales/admin/marketing/salaries).
  //
  // Month 1 holds the shared assumptions (pricing, Azure/dev costs,
  // overhead %, and the default monthly growth/churn rates). From Month 2
  // onward, each month only edits its own ACPU, new clients added, and
  // churned clients — total clients then rolls forward automatically:
  //   total clients (this month) = total clients (prior month)
  //                                + new clients added (this month)
  //                                − churned clients (this month)
  const MONTH_COUNT = 12;
  // All 12 months are now active/editable.
  const ENABLED_MONTHS = 12;

  type BusinessCaseAssumptions = {
    creditValueUsd: string;
    serviceCostPerCreditUsd: string;
    azureFixedSqlUsd: string;
    azureFixedStorageUsd: string;
    azureFixedFunctionsUsd: string;
    azureFixedStaticAppsUsd: string;
    azureFixedKeyVaultUsd: string;
    azureFixedMonitoringUsd: string;
    azureFixedSupportPlanUsd: string;
    devMaintenanceCostUsd: string;
    azureVariableCostPerCreditUsd: string;
    // Sales stays variable (% of usage cost) — it behaves like a sales
    // commission, so it's realistic for it to scale with usage/volume.
    salesCostPercent: string;
    // Admin, marketing and salaries are FIXED USD/month amounts (not a %
    // of cost base) — a conservative choice: payroll/marketing budget
    // doesn't automatically grow just because clients consumed more
    // credits this month, so these no longer inflate total costs as
    // usage grows.
    adminCostUsd: string;
    marketingCostUsd: string;
    salariesCostUsd: string;
    // Growth assumptions — used by the "Apply growth to all months" tool
    // at the top of the page to auto-fill new clients added / ACPU for
    // every month (2-12) in one click. Churn is intentionally NOT
    // auto-calculated here — it stays whatever each month has set
    // manually, since the user wants full control over churn by hand.
    // Client growth is a FLAT number of new clients added each month (NOT
    // a %) — a % compounding on the running total explodes exponentially
    // (e.g. 100% would double the client count every month), which isn't
    // realistic, so this is a simple fixed headcount added per month.
    newClientsPerMonth: string;
    acpuGrowthRatePercent: string;
  };
  const DEFAULT_BUSINESS_CASE_ASSUMPTIONS: BusinessCaseAssumptions = {
    creditValueUsd: '0.137',
    serviceCostPerCreditUsd: '0.055',
    // Azure fixed costs, itemized — the $15 flat default we used before was
    // based on an almost-idle DEV snapshot (Simulations Step 7); a real
    // paying-customer production environment needs more: a real SQL tier
    // (not serverless-at-minimum), Key Vault (secrets for API keys/certs),
    // Application Insights / Monitor (you can't run a paid product blind),
    // Static Web Apps on the Standard tier (custom domain/SLA), and at
    // least a Developer-tier Azure support plan once customers depend on
    // uptime. All editable — these are realistic starting points, not
    // exact invoices.
    // SQL default = Azure SQL Standard S2 (DTU) tier, ~$90/mo with ~250GB
    // included — a reasonable, cost-aware production tier for an early-stage
    // paid product (vs. General Purpose 2 vCore, ~$220-240/mo, for when
    // read/write load or GB actually requires it).
    azureFixedSqlUsd: '90',
    azureFixedStorageUsd: '8',
    azureFixedFunctionsUsd: '15',
    azureFixedStaticAppsUsd: '18',
    azureFixedKeyVaultUsd: '3',
    azureFixedMonitoringUsd: '10',
    azureFixedSupportPlanUsd: '29',
    // Ongoing dev/maintenance cost — e.g. Claude/AI coding assistant spend
    // used to keep building and maintaining the product every month. This
    // is a fixed cost independent of how many credits clients consume.
    devMaintenanceCostUsd: '1000',
    azureVariableCostPerCreditUsd: '0.005',
    salesCostPercent: '20',
    // Fixed USD/month — edit to match your real payroll/marketing budget.
    adminCostUsd: '100',
    marketingCostUsd: '500',
    salariesCostUsd: '200',
    newClientsPerMonth: '2',
    acpuGrowthRatePercent: '0',
  };

  // Per-month client inputs: ACPU can change month to month (e.g. usage
  // ramps up), and each month has its own new-clients-added / churned
  // counts. Month 1's "new clients added" is effectively its starting
  // client count (prior total = 0).
  type MonthClientInputs = {
    arpuCreditsPerClient: string;
    newClientsAdded: string;
    churnClients: string;
  };
  const DEFAULT_MONTH_INPUTS: MonthClientInputs[] = Array.from({ length: MONTH_COUNT }, (_, i) => {
    if (i === 0) return { arpuCreditsPerClient: '2000', newClientsAdded: '10', churnClients: '0' };
    if (i === 1) return { arpuCreditsPerClient: '2000', newClientsAdded: '3', churnClients: '1' };
    // Month 3 onward — default growth pattern: +2 new clients, −1 churn
    // each month. Still fully editable per month.
    return { arpuCreditsPerClient: '2000', newClientsAdded: '2', churnClients: '1' };
  });

  const [assumptions, setAssumptions] = useState<BusinessCaseAssumptions>(DEFAULT_BUSINESS_CASE_ASSUMPTIONS);
  const [monthInputs, setMonthInputs] = useState<MonthClientInputs[]>(DEFAULT_MONTH_INPUTS);
  const [activeMonth, setActiveMonth] = useState(0);
  const [breakevenRevealed, setBreakevenRevealed] = useState(false);
  const updateAssumption = (key: keyof BusinessCaseAssumptions) => (value: string) =>
    setAssumptions((current) => ({ ...current, [key]: value }));
  const updateMonth = (monthIndex: number, key: keyof MonthClientInputs) => (value: string) =>
    setMonthInputs((current) => current.map((month, i) => (i === monthIndex ? { ...month, [key]: value } : month)));

  // "Apply growth to all months" — new clients added each month (2-12) is
  // a FLAT fixed number (NOT a %, since a % compounding on the running
  // total explodes exponentially — e.g. 100% would double the client
  // count every month, which isn't realistic). ACPU still grows by %
  // each month (a modest usage/price ramp makes sense as a %). Churn is
  // left completely untouched on every month — it's always set by hand,
  // never auto-calculated.
  const applyGrowthToAllMonths = () => {
    const newClientsEachMonth = Math.max(0, Math.round(toNumber(assumptions.newClientsPerMonth)));
    const acpuGrowthRate = toNumber(assumptions.acpuGrowthRatePercent) / 100;
    setMonthInputs((current) => {
      const next = current.map((month) => ({ ...month }));
      let runningAcpu = toNumber(next[0].arpuCreditsPerClient);
      for (let i = 1; i < MONTH_COUNT; i += 1) {
        next[i].newClientsAdded = String(newClientsEachMonth);
        runningAcpu = Math.max(0, Math.round(runningAcpu * (1 + acpuGrowthRate)));
        next[i].arpuCreditsPerClient = String(runningAcpu);
      }
      return next;
    });
  };

  // Roll clients forward month by month: total(n) = total(n-1) + added(n) − churned(n).
  const totalClientsByMonth: number[] = [];
  let runningClients = 0;
  for (let i = 0; i < MONTH_COUNT; i += 1) {
    const added = toNumber(monthInputs[i].newClientsAdded);
    const churned = toNumber(monthInputs[i].churnClients);
    runningClients = i === 0 ? added : runningClients + added - churned;
    totalClientsByMonth.push(Math.max(0, runningClients));
  }
  const priorMonthClients = activeMonth === 0 ? 0 : totalClientsByMonth[activeMonth - 1];
  const totalClients = totalClientsByMonth[activeMonth];
  const arpuCreditsPerClient = toNumber(monthInputs[activeMonth].arpuCreditsPerClient);
  const newClientsAddedThisMonth = toNumber(monthInputs[activeMonth].newClientsAdded);
  const churnClientsThisMonth = toNumber(monthInputs[activeMonth].churnClients);

  const creditValueUsd = toNumber(assumptions.creditValueUsd);
  const serviceCostPerCreditUsd = toNumber(assumptions.serviceCostPerCreditUsd);
  const azureFixedSqlUsd = toNumber(assumptions.azureFixedSqlUsd);
  const azureFixedStorageUsd = toNumber(assumptions.azureFixedStorageUsd);
  const azureFixedFunctionsUsd = toNumber(assumptions.azureFixedFunctionsUsd);
  const azureFixedStaticAppsUsd = toNumber(assumptions.azureFixedStaticAppsUsd);
  const azureFixedKeyVaultUsd = toNumber(assumptions.azureFixedKeyVaultUsd);
  const azureFixedMonitoringUsd = toNumber(assumptions.azureFixedMonitoringUsd);
  const azureFixedSupportPlanUsd = toNumber(assumptions.azureFixedSupportPlanUsd);
  const devMaintenanceCostUsd = toNumber(assumptions.devMaintenanceCostUsd);
  const azureFixedCostUsd = azureFixedSqlUsd + azureFixedStorageUsd + azureFixedFunctionsUsd
    + azureFixedStaticAppsUsd + azureFixedKeyVaultUsd + azureFixedMonitoringUsd + azureFixedSupportPlanUsd;
  const azureVariableCostPerCreditUsd = toNumber(assumptions.azureVariableCostPerCreditUsd);
  const salesCostPercent = toNumber(assumptions.salesCostPercent) / 100;
  // Admin/marketing/salaries are fixed USD/month — they don't scale with
  // usage/clients, unlike sales (which stays a % commission on usage cost).
  const adminCostUsd = toNumber(assumptions.adminCostUsd);
  const marketingCostUsd = toNumber(assumptions.marketingCostUsd);
  const salariesCostUsd = toNumber(assumptions.salariesCostUsd);

  // 2-4: customers → credits consumed → revenue
  const totalCreditsConsumed = totalClients * arpuCreditsPerClient;
  const totalRevenue = totalCreditsConsumed * creditValueUsd;
  // Average $ price a single client pays per month, derived straight from
  // ACPU (Average Credits Per Unit/customer) × the credit sale price —
  // not a separately-input number, so it always stays consistent with the
  // credits-based pricing already modeled in Simulations.
  const avgPricePerClientUsd = arpuCreditsPerClient * creditValueUsd;

  // 5-7: usage-based RAW service cost (course creation/taking, Buddy,
  // Agents, My Brain) + Azure fixed/variable — these are the real
  // "cost of goods", BEFORE company overhead, exactly mirroring Steps
  // 1-8 of the Simulations model.
  const serviceUsageCost = totalCreditsConsumed * serviceCostPerCreditUsd;
  const azureVariableCost = totalCreditsConsumed * azureVariableCostPerCreditUsd;
  const totalBeforeCompanyCosts = serviceUsageCost + azureFixedCostUsd + azureVariableCost + devMaintenanceCostUsd;

  // Company overhead — sales stays a % of usage cost (commission-like,
  // scales with volume). Admin/marketing/salaries are fixed USD/month
  // (conservative: payroll/marketing budget doesn't auto-grow just
  // because clients consumed more credits this month).
  const salesCost = serviceUsageCost * salesCostPercent;
  const adminCost = adminCostUsd;
  const marketingCost = marketingCostUsd;
  const salariesCost = salariesCostUsd;
  const totalCompanyCosts = salesCost + adminCost + marketingCost + salariesCost;

  const totalCosts = totalBeforeCompanyCosts + totalCompanyCosts;
  const profitLoss = totalRevenue - totalCosts;
  // Cash flow required THIS month, standalone — equals the month's own
  // profit/loss (before accumulating prior months' results).
  const cashFlowMonth = profitLoss;

  // Cumulative profit/(loss) — rolls forward month over month: each prior
  // month's own profit/(loss) (computed with ITS OWN client count/ACPU
  // for that month, rolled forward via totalClientsByMonth, but using the
  // SAME shared cost/overhead assumptions as the active month above) gets
  // added to the running total through the active month.
  const computeMonthProfitLoss = (monthIndex: number) => {
    const monthClients = totalClientsByMonth[monthIndex];
    const monthAcpu = toNumber(monthInputs[monthIndex].arpuCreditsPerClient);
    const monthCreditsConsumed = monthClients * monthAcpu;
    const monthRevenue = monthCreditsConsumed * creditValueUsd;
    const monthServiceUsageCost = monthCreditsConsumed * serviceCostPerCreditUsd;
    const monthAzureVariableCost = monthCreditsConsumed * azureVariableCostPerCreditUsd;
    const monthBeforeCompanyCosts = monthServiceUsageCost + azureFixedCostUsd + monthAzureVariableCost + devMaintenanceCostUsd;
    const monthSalesCost = monthServiceUsageCost * salesCostPercent;
    const monthCompanyCosts = monthSalesCost + adminCostUsd + marketingCostUsd + salariesCostUsd;
    const monthTotalCosts = monthBeforeCompanyCosts + monthCompanyCosts;
    return monthRevenue - monthTotalCosts;
  };
  let cumulativeProfitLoss = 0;
  for (let i = 0; i <= activeMonth; i += 1) {
    cumulativeProfitLoss += computeMonthProfitLoss(i);
  }
  const priorCumulativeProfitLoss = cumulativeProfitLoss - profitLoss;

  // Full per-month breakdown (same fields as the on-screen table above),
  // used by the PDF export so the PDF shows all 12 months, not just the
  // active one.
  const getMonthFullData = (monthIndex: number) => {
    const monthPriorClients = monthIndex === 0 ? 0 : totalClientsByMonth[monthIndex - 1];
    const monthClients = totalClientsByMonth[monthIndex];
    const monthAcpu = toNumber(monthInputs[monthIndex].arpuCreditsPerClient);
    const monthNewClients = toNumber(monthInputs[monthIndex].newClientsAdded);
    const monthChurn = toNumber(monthInputs[monthIndex].churnClients);
    const monthCreditsConsumed = monthClients * monthAcpu;
    const monthRevenue = monthCreditsConsumed * creditValueUsd;
    const monthAvgPricePerClient = monthAcpu * creditValueUsd;
    const monthServiceUsageCost = monthCreditsConsumed * serviceCostPerCreditUsd;
    const monthAzureVariableCost = monthCreditsConsumed * azureVariableCostPerCreditUsd;
    const monthBeforeCompanyCosts = monthServiceUsageCost + azureFixedCostUsd + monthAzureVariableCost + devMaintenanceCostUsd;
    const monthSalesCost = monthServiceUsageCost * salesCostPercent;
    const monthAdminCost = adminCostUsd;
    const monthMarketingCost = marketingCostUsd;
    const monthSalariesCost = salariesCostUsd;
    const monthCompanyCosts = monthSalesCost + monthAdminCost + monthMarketingCost + monthSalariesCost;
    const monthTotalCosts = monthBeforeCompanyCosts + monthCompanyCosts;
    const monthProfitLoss = monthRevenue - monthTotalCosts;
    let monthCumulativeProfitLoss = 0;
    for (let i = 0; i <= monthIndex; i += 1) {
      monthCumulativeProfitLoss += computeMonthProfitLoss(i);
    }
    return {
      month: monthIndex + 1,
      priorClients: monthPriorClients,
      newClients: monthNewClients,
      churn: monthChurn,
      totalClients: monthClients,
      acpu: monthAcpu,
      avgPricePerClient: monthAvgPricePerClient,
      revenue: monthRevenue,
      serviceUsageCost: monthServiceUsageCost,
      azureFixedCost: azureFixedCostUsd,
      azureVariableCost: monthAzureVariableCost,
      devMaintenanceCost: devMaintenanceCostUsd,
      salesCost: monthSalesCost,
      adminCost: monthAdminCost,
      marketingCost: monthMarketingCost,
      salariesCost: monthSalariesCost,
      totalCosts: monthTotalCosts,
      profitLoss: monthProfitLoss,
      cumulativeProfitLoss: monthCumulativeProfitLoss,
    };
  };

  const handleExportPdf = async () => {
    const { jsPDF } = await import('jspdf');
    const autoTable = (await import('jspdf-autotable')).default;
    const doc = new jsPDF({ orientation: 'landscape', unit: 'pt', format: 'a4' });

    doc.setFontSize(18);
    doc.text('Human OS — 12-Month Business Case', 40, 40);
    doc.setFontSize(10);
    doc.setTextColor(100);
    doc.text(`Generated ${new Date().toLocaleDateString('en-US')}`, 40, 58);

    const head = [[
      'Month', 'Clients (end)', '+New', '-Churn', 'ACPU', 'Avg $/client',
      'Revenue', 'Usage cost', 'Azure fixed', 'Azure var.', 'Dev/maint.',
      'Sales', 'Admin', 'Marketing', 'Salaries', 'Total costs', 'Profit/(loss)', 'Cumulative P/L',
    ]];
    const body = Array.from({ length: MONTH_COUNT }, (_, i) => getMonthFullData(i)).map((m) => [
      String(m.month),
      m.totalClients.toLocaleString(),
      `+${m.newClients.toLocaleString()}`,
      `-${m.churn.toLocaleString()}`,
      m.acpu.toLocaleString(),
      formatUsd(m.avgPricePerClient),
      formatUsd(m.revenue),
      formatUsd(m.serviceUsageCost),
      formatUsd(m.azureFixedCost),
      formatUsd(m.azureVariableCost),
      formatUsd(m.devMaintenanceCost),
      formatUsd(m.salesCost),
      formatUsd(m.adminCost),
      formatUsd(m.marketingCost),
      formatUsd(m.salariesCost),
      formatUsd(m.totalCosts),
      formatUsd(m.profitLoss),
      formatUsd(m.cumulativeProfitLoss),
    ]);

    autoTable(doc, {
      head,
      body,
      startY: 72,
      styles: { fontSize: 6.5, cellPadding: 3 },
      headStyles: { fillColor: [20, 70, 140] },
      didParseCell: (data) => {
        if (data.section === 'body' && (data.column.index === 16 || data.column.index === 17)) {
          const raw = String(data.cell.raw);
          if (raw.includes('$-')) data.cell.styles.textColor = [185, 28, 28];
          else data.cell.styles.textColor = [15, 118, 110];
        }
      },
    });

    doc.save('human-os-business-case-12-months.pdf');
  };

  // Breakeven clients: solve profitLoss(C) = 0 for C, with everything else
  // held fixed (ACPU, cost %, Azure, dev/maintenance). The whole cost model
  // is linear in "number of clients" once ACPU is fixed, so this has a
  // clean closed-form instead of needing a backend/trial-and-error:
  //   v = variable cost per client (service + Azure variable)
  //   s = sales cost per client (% of service cost only)
  //   F = all fixed costs (Azure fixed + dev/maintenance + admin + marketing + salaries,
  //       all USD/month flat amounts — admin/marketing/salaries no longer scale with usage)
  //   revenue(C) = C · ACPU · creditValueUsd
  //   totalCosts(C) = (v+s)·C + F
  //   breakeven: C · ACPU·creditValueUsd = (v+s)·C + F
  //           => C = F / [ ACPU·creditValueUsd − (v+s) ]
  const fixedCostsTotal = azureFixedCostUsd + devMaintenanceCostUsd + adminCostUsd + marketingCostUsd + salariesCostUsd;
  const variableCostPerClient = arpuCreditsPerClient * (serviceCostPerCreditUsd + azureVariableCostPerCreditUsd);
  const salesCostPerClient = arpuCreditsPerClient * serviceCostPerCreditUsd * salesCostPercent;
  const revenuePerClient = arpuCreditsPerClient * creditValueUsd;
  const breakevenDenominator = revenuePerClient - (variableCostPerClient + salesCostPerClient);
  const breakevenPossible = breakevenDenominator > 0;
  const breakevenClientsRaw = breakevenPossible
    ? fixedCostsTotal / breakevenDenominator
    : null;
  const breakevenClients = breakevenClientsRaw !== null ? Math.ceil(breakevenClientsRaw) : null;
  const breakevenRevenue = breakevenClients !== null ? breakevenClients * revenuePerClient : null;

  // Breakeven ACPU: same model, but solved the other way — holding the
  // number of clients fixed (totalClients, as entered), what ACPU would
  // each client need to consume/pay for to reach $0 profit? Same algebra,
  // just isolating ACPU (A) instead of clients (C):
  //   unitVarCost = per-credit variable cost incl. its share of sales% cost
  //   A = F / [ C · (creditValueUsd − unitVarCost) ]
  const unitVariableCostPerCredit = serviceCostPerCreditUsd + azureVariableCostPerCreditUsd
    + serviceCostPerCreditUsd * salesCostPercent;
  const breakevenAcpuDenominator = totalClients * (creditValueUsd - unitVariableCostPerCredit);
  const breakevenAcpuPossible = totalClients > 0 && breakevenAcpuDenominator > 0;
  const breakevenAcpuRaw = breakevenAcpuPossible
    ? fixedCostsTotal / breakevenAcpuDenominator
    : null;
  const breakevenAcpu = breakevenAcpuRaw !== null ? Math.ceil(breakevenAcpuRaw) : null;

  const businessCaseRows = [
    { labelEn: '1) Month', labelEs: '1) Mes', value: String(activeMonth + 1) },
    ...(activeMonth === 0
      ? []
      : [{ labelEn: '1a) Total clients (prior month)', labelEs: '1a) Total clientes (mes anterior)', value: priorMonthClients.toLocaleString() }]),
    { labelEn: '1b) New clients added (this month)', labelEs: '1b) Clientes nuevos agregados (este mes)', value: `+${newClientsAddedThisMonth.toLocaleString()}` },
    { labelEn: '1c) Churned clients (this month)', labelEs: '1c) Clientes perdidos / churn (este mes)', value: `-${churnClientsThisMonth.toLocaleString()}` },
    { labelEn: '2) Total clients (end of month)', labelEs: '2) Total clientes (fin de mes)', value: totalClients.toLocaleString() },
    { labelEn: '3) ACPU — avg. credits / client / month', labelEs: '3) ACPU — créditos promedio / cliente / mes', value: arpuCreditsPerClient.toLocaleString() },
    { labelEn: '3b) → Avg. $ price / client / month', labelEs: '3b) → Precio promedio $ / cliente / mes', value: formatUsd(avgPricePerClientUsd) },
    { labelEn: '4) Total revenue (month)', labelEs: '4) Total revenue (mes)', value: formatUsd(totalRevenue) },
    { labelEn: '5) Usage costs (create/take courses, Buddy, Agents, My Brain)', labelEs: '5) Costos por uso (crear/tomar cursos, Buddy, Agentes, My Brain)', value: formatUsd(serviceUsageCost) },
    { labelEn: '6) Azure fixed costs (SQL + Storage + Functions + Static Apps + Key Vault + Monitoring + Support)', labelEs: '6) Costos fijos de Azure (SQL + Storage + Functions + Static Apps + Key Vault + Monitoreo + Soporte)', value: formatUsd(azureFixedCostUsd) },
    { labelEn: '7) Azure variable costs', labelEs: '7) Costos variables de Azure', value: formatUsd(azureVariableCost) },
    { labelEn: '7a) Dev / maintenance cost (e.g. Claude AI usage)', labelEs: '7a) Costo de desarrollo / mantenimiento (ej. uso de Claude AI)', value: formatUsd(devMaintenanceCostUsd) },
    { labelEn: '7b) Sales cost (% of usage cost)', labelEs: '7b) Costo de ventas (% del costo de uso)', value: formatUsd(salesCost) },
    { labelEn: '7c) Administration (fixed USD/month)', labelEs: '7c) Administración (USD/mes fijo)', value: formatUsd(adminCost) },
    { labelEn: '7d) Marketing (fixed USD/month)', labelEs: '7d) Marketing (USD/mes fijo)', value: formatUsd(marketingCost) },
    { labelEn: '7e) Salaries (fixed USD/month)', labelEs: '7e) Salarios (USD/mes fijo)', value: formatUsd(salariesCost) },
    { labelEn: '8) Total costs (usage + Azure + dev/maintenance + sales + admin + marketing + salaries)', labelEs: '8) Total costos (uso + Azure + dev/mantenimiento + ventas + admin + marketing + salarios)', value: formatUsd(totalCosts) },
    { labelEn: '9) Profit / (loss) for the month', labelEs: '9) Profit / (pérdida) del mes', value: formatUsd(profitLoss), highlight: true, highlightPositive: profitLoss >= 0 },
    ...(activeMonth === 0
      ? []
      : [{ labelEn: '9a) Cumulative profit / (loss) (prior months)', labelEs: '9a) Profit / (pérdida) acumulado (meses anteriores)', value: formatUsd(priorCumulativeProfitLoss) }]),
    { labelEn: `10) Cumulative profit / (loss) — through month ${activeMonth + 1}`, labelEs: `10) Profit / (pérdida) acumulado — hasta el mes ${activeMonth + 1}`, value: formatUsd(cumulativeProfitLoss), highlight: true, highlightPositive: cumulativeProfitLoss >= 0 },
  ];

  return <>
    <div className="main-title-row">
      <div>
        <p className="eyebrow">{isEnglish ? 'Monthly financial model (Excel-style)' : 'Modelo financiero mensual (estilo Excel)'}</p>
        <h2>Business Case</h2>
      </div>
      <button type="button" className="business-case-apply-btn" onClick={handleExportPdf}>
        <Download size={16} style={{ marginRight: 6, verticalAlign: 'text-bottom' }} />
        {isEnglish ? 'Export 12-month PDF' : 'Exportar PDF de 12 meses'}
      </button>
    </div>

    <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
      <p className="customer-label" style={{ marginBottom: 14 }}>
        {isEnglish
          ? 'Apply growth to all 12 months — set a FIXED number of new clients added every month (not a %, so it never compounds/explodes) and a monthly ACPU growth %, then click Apply to auto-fill every month (2-12). Churn is NEVER touched by this — it stays exactly as entered by hand on each month.'
          : 'Aplicar crecimiento a los 12 meses — define un número FIJO de clientes nuevos agregados cada mes (no un %, así nunca se dispara exponencialmente) y un % de crecimiento mensual de ACPU, y presiona Aplicar para llenar automáticamente cada mes (2-12). El churn NUNCA se toca con esto — se queda tal cual lo ingreses a mano en cada mes.'}
      </p>
      <div className="stat-card-grid">
        <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
          <span className="customer-label">{isEnglish ? 'New clients added every month (fixed number) — e.g. 2' : 'Clientes nuevos agregados cada mes (número fijo) — ej. 2'}</span>
          <input type="number" min={0} step="1" className="modal-confirm-input" value={assumptions.newClientsPerMonth}
            onChange={(e) => updateAssumption('newClientsPerMonth')(e.target.value)} />
        </label>
        <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
          <span className="customer-label">{isEnglish ? 'Monthly ACPU growth rate (%) — e.g. 0' : 'Tasa de crecimiento mensual de ACPU (%) — ej. 0'}</span>
          <input type="number" min={0} max={1000} step="0.1" className="modal-confirm-input" value={assumptions.acpuGrowthRatePercent}
            onChange={(e) => updateAssumption('acpuGrowthRatePercent')(e.target.value)} />
        </label>
        <div className="stat-card" style={{ display: 'grid', gap: 8, alignContent: 'center' }}>
          <button type="button" className="business-case-apply-btn" onClick={applyGrowthToAllMonths}>
            {isEnglish ? 'Apply to all 12 months' : 'Aplicar a los 12 meses'}
          </button>
          <p style={{ margin: 0, fontSize: 11, color: '#9aa7b8' }}>
            {isEnglish ? 'Churn stays untouched — edit it by hand per month.' : 'El churn no se toca — edítalo a mano en cada mes.'}
          </p>
        </div>
      </div>
    </section>

    <div className="steps-layout">
      <aside className="steps-sidebar">
        <nav className="steps-nav">
          {Array.from({ length: MONTH_COUNT }, (_, i) => {
            const isActive = activeMonth === i;
            const isEnabled = i < ENABLED_MONTHS;
            return (
              <button
                key={i}
                type="button"
                disabled={!isEnabled}
                className={`steps-nav-item${isActive ? ' active' : ''}`}
                onClick={() => isEnabled && setActiveMonth(i)}
                title={!isEnabled ? (isEnglish ? 'Coming soon' : 'Próximamente') : undefined}
                style={!isEnabled ? { opacity: 0.45, cursor: 'not-allowed' } : undefined}
              >
                <span className="steps-nav-icon" style={{ color: isActive ? '#ffffff' : '#55708f' }}>
                  <Calendar size={16} />
                </span>
                <span>{isEnglish ? `Month ${i + 1}` : `Mes ${i + 1}`}</span>
              </button>
            );
          })}
        </nav>
      </aside>

      <div className="steps-content">

    {activeMonth === 0 && (
    <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
      <p className="customer-label" style={{ marginBottom: 14 }}>
        {isEnglish
          ? 'Shared assumptions (apply to all 12 months) — credit pricing, Azure/dev costs, company overhead %, and default monthly growth/churn rates. ACPU, new clients, and churn are set per month below.'
          : 'Supuestos compartidos (aplican a los 12 meses) — precio del crédito, costos de Azure/dev, % de gastos de empresa, y tasas mensuales de crecimiento/churn por defecto. El ACPU, los clientes nuevos y el churn se configuran por mes abajo.'}
      </p>
      <div className="stat-card-grid">
        <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
          <span className="customer-label">{isEnglish ? 'Credit sale price (USD / credit)' : 'Precio de venta del crédito (USD / crédito)'}</span>
          <input type="number" min={0} step="0.001" className="modal-confirm-input" value={assumptions.creditValueUsd}
            onChange={(e) => updateAssumption('creditValueUsd')(e.target.value)} />
        </label>
        <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
          <span className="customer-label">{isEnglish ? 'Usage cost (USD / credit) — courses, Buddy, Agents, My Brain' : 'Costo por uso (USD / crédito) — cursos, Buddy, Agentes, My Brain'}</span>
          <input type="number" min={0} step="0.001" className="modal-confirm-input" value={assumptions.serviceCostPerCreditUsd}
            onChange={(e) => updateAssumption('serviceCostPerCreditUsd')(e.target.value)} />
        </label>
        <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
          <span className="customer-label">{isEnglish ? 'Azure — SQL database / month (USD) — Standard S2 DTU (~250GB)' : 'Azure — base de datos SQL / mes (USD) — Standard S2 DTU (~250GB)'}</span>
          <input type="number" min={0} step="0.01" className="modal-confirm-input" value={assumptions.azureFixedSqlUsd}
            onChange={(e) => updateAssumption('azureFixedSqlUsd')(e.target.value)} />
        </label>
        <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
          <span className="customer-label">{isEnglish ? 'Azure — Storage / month (USD)' : 'Azure — Storage / mes (USD)'}</span>
          <input type="number" min={0} step="0.01" className="modal-confirm-input" value={assumptions.azureFixedStorageUsd}
            onChange={(e) => updateAssumption('azureFixedStorageUsd')(e.target.value)} />
        </label>
        <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
          <span className="customer-label">{isEnglish ? 'Azure — Functions hosting / month (USD)' : 'Azure — hospedaje de Functions / mes (USD)'}</span>
          <input type="number" min={0} step="0.01" className="modal-confirm-input" value={assumptions.azureFixedFunctionsUsd}
            onChange={(e) => updateAssumption('azureFixedFunctionsUsd')(e.target.value)} />
        </label>
        <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
          <span className="customer-label">{isEnglish ? 'Azure — Static Web Apps / month (USD)' : 'Azure — Static Web Apps / mes (USD)'}</span>
          <input type="number" min={0} step="0.01" className="modal-confirm-input" value={assumptions.azureFixedStaticAppsUsd}
            onChange={(e) => updateAssumption('azureFixedStaticAppsUsd')(e.target.value)} />
        </label>
        <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
          <span className="customer-label">{isEnglish ? 'Azure — Key Vault / month (USD)' : 'Azure — Key Vault / mes (USD)'}</span>
          <input type="number" min={0} step="0.01" className="modal-confirm-input" value={assumptions.azureFixedKeyVaultUsd}
            onChange={(e) => updateAssumption('azureFixedKeyVaultUsd')(e.target.value)} />
        </label>
        <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
          <span className="customer-label">{isEnglish ? 'Azure — Monitoring / App Insights / month (USD)' : 'Azure — Monitoreo / App Insights / mes (USD)'}</span>
          <input type="number" min={0} step="0.01" className="modal-confirm-input" value={assumptions.azureFixedMonitoringUsd}
            onChange={(e) => updateAssumption('azureFixedMonitoringUsd')(e.target.value)} />
        </label>
        <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
          <span className="customer-label">{isEnglish ? 'Azure — support plan / month (USD)' : 'Azure — plan de soporte / mes (USD)'}</span>
          <input type="number" min={0} step="0.01" className="modal-confirm-input" value={assumptions.azureFixedSupportPlanUsd}
            onChange={(e) => updateAssumption('azureFixedSupportPlanUsd')(e.target.value)} />
        </label>
        <div className="stat-card" style={{ display: 'grid', gap: 6, background: '#f7fafd' }}>
          <span className="customer-label">{isEnglish ? '→ Total Azure fixed costs / month' : '→ Total costos fijos de Azure / mes'}</span>
          <h3 style={{ margin: 0 }}>{formatUsd(azureFixedCostUsd)}</h3>
        </div>
        <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
          <span className="customer-label">{isEnglish ? 'Dev / maintenance cost / month (USD) — e.g. Claude AI for building & maintaining the product' : 'Costo de desarrollo / mantenimiento / mes (USD) — ej. Claude AI para construir y mantener el producto'}</span>
          <input type="number" min={0} step="1" className="modal-confirm-input" value={assumptions.devMaintenanceCostUsd}
            onChange={(e) => updateAssumption('devMaintenanceCostUsd')(e.target.value)} />
        </label>
        <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
          <span className="customer-label">{isEnglish ? 'Azure variable cost (USD / credit)' : 'Costo variable de Azure (USD / crédito)'}</span>
          <input type="number" min={0} step="0.001" className="modal-confirm-input" value={assumptions.azureVariableCostPerCreditUsd}
            onChange={(e) => updateAssumption('azureVariableCostPerCreditUsd')(e.target.value)} />
        </label>
        <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
          <span className="customer-label">{isEnglish ? 'Sales cost (% of usage cost)' : 'Costo de ventas (% del costo de uso)'}</span>
          <input type="number" min={0} max={100} step="0.1" className="modal-confirm-input" value={assumptions.salesCostPercent}
            onChange={(e) => updateAssumption('salesCostPercent')(e.target.value)} />
        </label>
        <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
          <span className="customer-label">{isEnglish ? 'Administration (fixed USD / month)' : 'Administración (USD / mes fijo)'}</span>
          <input type="number" min={0} step="1" className="modal-confirm-input" value={assumptions.adminCostUsd}
            onChange={(e) => updateAssumption('adminCostUsd')(e.target.value)} />
        </label>
        <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
          <span className="customer-label">{isEnglish ? 'Marketing (fixed USD / month)' : 'Marketing (USD / mes fijo)'}</span>
          <input type="number" min={0} step="1" className="modal-confirm-input" value={assumptions.marketingCostUsd}
            onChange={(e) => updateAssumption('marketingCostUsd')(e.target.value)} />
        </label>
        <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
          <span className="customer-label">{isEnglish ? 'Salaries (fixed USD / month)' : 'Salarios (USD / mes fijo)'}</span>
          <input type="number" min={0} step="1" className="modal-confirm-input" value={assumptions.salariesCostUsd}
            onChange={(e) => updateAssumption('salariesCostUsd')(e.target.value)} />
        </label>
      </div>
    </section>
    )}

    <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
      <p className="customer-label" style={{ marginBottom: 14 }}>
        {isEnglish
          ? `Month ${activeMonth + 1} inputs — ACPU (Average Credits Per Unit/customer), new clients added, and churned clients for this month.`
          : `Entradas del Mes ${activeMonth + 1} — ACPU (Créditos Promedio por Unidad/cliente), clientes nuevos agregados, y clientes perdidos (churn) este mes.`}
      </p>
      <div className="stat-card-grid">
        {activeMonth > 0 && (
          <div className="stat-card" style={{ display: 'grid', gap: 8, background: '#f7fafd' }}>
            <span className="customer-label">{isEnglish ? '← Total clients (prior month)' : '← Total clientes (mes anterior)'}</span>
            <h3 style={{ margin: 0 }}>{priorMonthClients.toLocaleString()}</h3>
          </div>
        )}
        <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
          <span className="customer-label">{isEnglish ? (activeMonth === 0 ? 'Starting clients (month 1)' : 'New clients added (this month)') : (activeMonth === 0 ? 'Clientes iniciales (mes 1)' : 'Clientes nuevos agregados (este mes)')}</span>
          <input type="number" min={0} className="modal-confirm-input" value={monthInputs[activeMonth].newClientsAdded}
            onChange={(e) => updateMonth(activeMonth, 'newClientsAdded')(e.target.value)} />
        </label>
        {activeMonth > 0 && (
          <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
            <span className="customer-label">{isEnglish ? 'Churned clients (this month)' : 'Clientes perdidos / churn (este mes)'}</span>
            <input type="number" min={0} className="modal-confirm-input" value={monthInputs[activeMonth].churnClients}
              onChange={(e) => updateMonth(activeMonth, 'churnClients')(e.target.value)} />
          </label>
        )}
        <div className="stat-card" style={{ display: 'grid', gap: 6, background: '#f7fafd' }}>
          <span className="customer-label">{isEnglish ? '→ Total clients (end of month)' : '→ Total clientes (fin de mes)'}</span>
          <h3 style={{ margin: 0 }}>{totalClients.toLocaleString()}</h3>
          {activeMonth > 0 && (
            <p style={{ margin: 0, fontSize: 11, color: '#9aa7b8' }}>
              {isEnglish ? '= prior month + new clients − churn' : '= mes anterior + clientes nuevos − churn'}
            </p>
          )}
        </div>
        <label className="stat-card" style={{ display: 'grid', gap: 8 }}>
          <span className="customer-label">{isEnglish ? 'ACPU — avg. credits / client / month' : 'ACPU — créditos promedio / cliente / mes'}</span>
          <input type="number" min={0} className="modal-confirm-input" value={monthInputs[activeMonth].arpuCreditsPerClient}
            onChange={(e) => updateMonth(activeMonth, 'arpuCreditsPerClient')(e.target.value)} />
        </label>
        <div className="stat-card" style={{ display: 'grid', gap: 8, background: '#f7fafd' }}>
          <span className="customer-label">{isEnglish ? '→ Avg. $ price / client / month' : '→ Precio promedio $ / cliente / mes'}</span>
          <h3 style={{ margin: 0, color: '#1f8a4c' }}>{formatUsd(avgPricePerClientUsd)}</h3>
          <p style={{ margin: 0, fontSize: 11, color: '#9aa7b8' }}>
            {isEnglish ? '= ACPU × credit sale price' : '= ACPU × precio de venta del crédito'}
          </p>
        </div>
      </div>
    </section>

    <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
      <p className="customer-label" style={{ marginBottom: 14 }}>
        {isEnglish ? `Monthly business case — Month ${activeMonth + 1}` : `Caso de negocio mensual — Mes ${activeMonth + 1}`}
      </p>
      <table className="cost-table">
        <thead>
          <tr>
            <th>{isEnglish ? 'Line item' : 'Concepto'}</th>
            <th>{isEnglish ? `Month ${activeMonth + 1}` : `Mes ${activeMonth + 1}`}</th>
          </tr>
        </thead>
        <tbody>
          {businessCaseRows.map((row) => (
            <tr key={row.labelEn}>
              <td>{isEnglish ? row.labelEn : row.labelEs}</td>
              <td>
                {row.highlight
                  ? <strong style={{ color: row.highlightPositive ? '#1f8a4c' : '#c0392b' }}>{row.value}</strong>
                  : row.value}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>

    <section className="stat-card-grid tenant-section-gap">
      <div className="stat-card">
        <p className="customer-label">{isEnglish ? 'Total revenue / month' : 'Revenue total / mes'}</p>
        <h3>{formatUsd(totalRevenue)}</h3>
      </div>
      <div className="stat-card">
        <p className="customer-label">{isEnglish ? 'Total costs / month' : 'Costos totales / mes'}</p>
        <h3>{formatUsd(totalCosts)}</h3>
      </div>
      <div className="stat-card" style={{ border: `2px solid ${profitLoss >= 0 ? '#1f8a4c' : '#c0392b'}` }}>
        <p className="customer-label">{isEnglish ? 'Profit / (loss)' : 'Ganancia / (pérdida)'}</p>
        <h3 style={{ color: profitLoss >= 0 ? '#1f8a4c' : '#c0392b' }}>{formatUsd(profitLoss)}</h3>
      </div>
      <div className="stat-card">
        <p className="customer-label">{isEnglish ? 'Cash flow required this month' : 'Flujo de caja requerido este mes'}</p>
        <h3 style={{ color: cashFlowMonth >= 0 ? '#1f8a4c' : '#c0392b' }}>{formatUsd(cashFlowMonth)}</h3>
        <p style={{ margin: '4px 0 0', fontSize: 11, color: '#9aa7b8' }}>
          {isEnglish
            ? (cashFlowMonth >= 0 ? 'Positive — no extra cash needed beyond operations.' : 'Negative — this much extra cash must be funded to cover the month.')
            : (cashFlowMonth >= 0 ? 'Positivo — no se necesita caja extra más allá de la operación.' : 'Negativo — hay que financiar esta cantidad extra para cubrir el mes.')}
        </p>
      </div>
      <div className="stat-card" style={{ border: `2px solid ${cumulativeProfitLoss >= 0 ? '#1f8a4c' : '#c0392b'}`, background: '#f7fafd' }}>
        <p className="customer-label">{isEnglish ? `Cumulative profit / (loss) — through month ${activeMonth + 1}` : `Profit / (pérdida) acumulado — hasta el mes ${activeMonth + 1}`}</p>
        <h3 style={{ color: cumulativeProfitLoss >= 0 ? '#1f8a4c' : '#c0392b' }}>{formatUsd(cumulativeProfitLoss)}</h3>
        <p style={{ margin: '4px 0 0', fontSize: 11, color: '#9aa7b8' }}>
          {activeMonth === 0
            ? (isEnglish ? '= profit/(loss) of month 1' : '= profit/(pérdida) del mes 1')
            : (isEnglish
                ? `= ${formatUsd(priorCumulativeProfitLoss)} (through month ${activeMonth}) + ${formatUsd(profitLoss)} (this month)`
                : `= ${formatUsd(priorCumulativeProfitLoss)} (hasta el mes ${activeMonth}) + ${formatUsd(profitLoss)} (este mes)`)}
        </p>
      </div>
    </section>

    <section className="cost-table-wrap tenant-section-gap" style={{ padding: 20 }}>
      <p className="customer-label" style={{ marginBottom: 14 }}>
        {isEnglish ? 'Breakeven calculator — clients needed this month to reach $0 profit' : 'Calculadora de breakeven — clientes necesarios este mes para llegar a $0 de ganancia'}
      </p>
      <p style={{ fontSize: 12, color: '#64748b', margin: '0 0 14px' }}>
        {isEnglish
          ? 'Solves the full cost model above (usage cost, Azure fixed + variable, dev/maintenance, sales %, fixed admin/marketing/salaries USD) for the number of clients at which revenue exactly covers total costs, holding ACPU and all assumptions fixed.'
          : 'Resuelve el modelo de costos completo de arriba (costo de uso, Azure fijo + variable, dev/mantenimiento, % de ventas, admin/marketing/salarios fijos en USD) para el número de clientes en que el revenue cubre exactamente los costos totales, manteniendo fijos el ACPU y el resto de supuestos.'}
      </p>
      <button
        type="button"
        className="primary-btn"
        onClick={() => setBreakevenRevealed(true)}
      >
        {isEnglish ? 'Calculate breakeven clients' : 'Calcular clientes para breakeven'}
      </button>
      {breakevenRevealed && (
        <div className="stat-card-grid" style={{ marginTop: 16 }}>
          <div className="stat-card" style={{ border: '2px solid #2563eb' }}>
            <p className="customer-label">{isEnglish ? 'Clients needed to break even' : 'Clientes necesarios para breakeven'}</p>
            <h3 style={{ color: '#2563eb' }}>
              {breakevenPossible && breakevenClients !== null ? breakevenClients.toLocaleString() : (isEnglish ? 'Not possible' : 'No es posible')}
            </h3>
            <p style={{ margin: '4px 0 0', fontSize: 11, color: '#9aa7b8' }}>
              {breakevenPossible
                ? (isEnglish ? `At current ACPU (${arpuCreditsPerClient.toLocaleString()} credits/client)` : `Con el ACPU actual (${arpuCreditsPerClient.toLocaleString()} créditos/cliente)`)
                : (isEnglish
                    ? 'Margin per client is negative at this price/cost/% mix — adding clients only adds losses. Raise the credit price, lower cost %, or lower cost/credit.'
                    : 'El margen por cliente es negativo con este mix de precio/costo/%. Agregar clientes solo agrega pérdidas. Sube el precio del crédito, baja los % de costo, o baja el costo/crédito.')}
            </p>
          </div>
          {breakevenPossible && breakevenRevenue !== null && (
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'Revenue at breakeven' : 'Revenue en breakeven'}</p>
              <h3>{formatUsd(breakevenRevenue)}</h3>
            </div>
          )}
          {breakevenPossible && breakevenClients !== null && (
            <div className="stat-card">
              <p className="customer-label">{isEnglish ? 'vs. current clients input' : 'vs. clientes actuales ingresados'}</p>
              <h3 style={{ color: totalClients >= breakevenClients ? '#1f8a4c' : '#c0392b' }}>
                {totalClients.toLocaleString()} {isEnglish ? 'clients' : 'clientes'}
              </h3>
              <p style={{ margin: '4px 0 0', fontSize: 11, color: '#9aa7b8' }}>
                {totalClients >= breakevenClients
                  ? (isEnglish ? '✓ Above breakeven' : '✓ Por encima del breakeven')
                  : (isEnglish ? `Need ${(breakevenClients - totalClients).toLocaleString()} more clients` : `Faltan ${(breakevenClients - totalClients).toLocaleString()} clientes más`)}
              </p>
            </div>
          )}
          <div className="stat-card" style={{ border: '2px solid #7c3aed' }}>
            <p className="customer-label">{isEnglish ? 'ACPU needed to break even (at current clients)' : 'ACPU necesario para breakeven (con los clientes actuales)'}</p>
            <h3 style={{ color: '#7c3aed' }}>
              {breakevenAcpuPossible && breakevenAcpu !== null ? `${breakevenAcpu.toLocaleString()} ${isEnglish ? 'credits/client' : 'créditos/cliente'}` : (isEnglish ? 'Not possible' : 'No es posible')}
            </h3>
            <p style={{ margin: '4px 0 0', fontSize: 11, color: '#9aa7b8' }}>
              {breakevenAcpuPossible
                ? (isEnglish
                    ? `With ${totalClients.toLocaleString()} clients, each would need to consume/pay for this many credits/month`
                    : `Con ${totalClients.toLocaleString()} clientes, cada uno necesitaría consumir/pagar esta cantidad de créditos/mes`)
                : (isEnglish
                    ? 'No ACPU value makes this profitable at the current client count and cost/% mix — margin per credit is negative.'
                    : 'Ningún valor de ACPU hace esto rentable con la cantidad de clientes y mix de costo/% actuales — el margen por crédito es negativo.')}
            </p>
            {breakevenAcpuPossible && breakevenAcpu !== null && (
              <p style={{ margin: '4px 0 0', fontSize: 11, color: arpuCreditsPerClient >= breakevenAcpu ? '#1f8a4c' : '#c0392b' }}>
                {arpuCreditsPerClient >= breakevenAcpu
                  ? (isEnglish ? `✓ Current ACPU (${arpuCreditsPerClient.toLocaleString()}) is above breakeven` : `✓ El ACPU actual (${arpuCreditsPerClient.toLocaleString()}) está por encima del breakeven`)
                  : (isEnglish ? `Current ACPU (${arpuCreditsPerClient.toLocaleString()}) is ${(breakevenAcpu - arpuCreditsPerClient).toLocaleString()} credits short` : `Al ACPU actual (${arpuCreditsPerClient.toLocaleString()}) le faltan ${(breakevenAcpu - arpuCreditsPerClient).toLocaleString()} créditos`)}
              </p>
            )}
          </div>
        </div>
      )}
    </section>

      </div>
    </div>
  </>;
}

function BusinessCustomersPage({ isEnglish }: { isEnglish: boolean }) {
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [tenantToDelete, setTenantToDelete] = useState<Tenant | null>(null);
  const [confirmText, setConfirmText] = useState('');
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [lastDeletionSummary, setLastDeletionSummary] = useState<TenantDeletionSummary | null>(null);

  const loadTenants = () => {
    setIsLoading(true);
    fetch('/api/tenants')
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load tenants.');
        return response.json() as Promise<Record<string, unknown>[]>;
      })
      // API returns PascalCase (TenantId, Name, IsActive) — normalize to the camelCase Tenant shape.
      .then((rows) => setTenants(rows.map((row) => ({
        tenantId: String(row.TenantId ?? row.tenantId ?? ''),
        name: String(row.Name ?? row.name ?? ''),
        slug: String(row.Slug ?? row.slug ?? ''),
        orgType: String(row.OrgType ?? row.orgType ?? ''),
        isActive: Boolean(row.IsActive ?? row.isActive ?? false),
      }))))
      .catch((loadError: Error) => setError(loadError.message))
      .finally(() => setIsLoading(false));
  };

  useEffect(loadTenants, []);

  const closeDeleteDialog = () => {
    setTenantToDelete(null);
    setConfirmText('');
    setDeleteError(null);
  };

  const handleConfirmDelete = () => {
    if (!tenantToDelete || confirmText.trim().toLowerCase() !== tenantToDelete.slug.toLowerCase()) return;
    setIsDeleting(true);
    setDeleteError(null);
    fetch(`/api/tenants/${tenantToDelete.tenantId}?confirm=${encodeURIComponent(confirmText.trim())}`, { method: 'DELETE' })
      .then(async (response) => {
        const body = await response.json().catch(() => null);
        if (!response.ok) {
          throw new Error(body?.message ?? body?.error ?? 'Unable to delete tenant.');
        }
        return body as Record<string, unknown>;
      })
      .then((row) => {
        setLastDeletionSummary({
          tenantId: String(row.TenantId ?? row.tenantId ?? ''),
          tenantName: String(row.TenantName ?? row.tenantName ?? ''),
          deletedRowsByTable: (row.DeletedRowsByTable ?? row.deletedRowsByTable ?? {}) as Record<string, number>,
          deletedBlobCount: Number(row.DeletedBlobCount ?? row.deletedBlobCount ?? 0),
          totalRowsDeleted: Number(row.TotalRowsDeleted ?? row.totalRowsDeleted ?? 0),
        });
        closeDeleteDialog();
        loadTenants();
      })
      .catch((deleteErr: Error) => setDeleteError(deleteErr.message))
      .finally(() => setIsDeleting(false));
  };

  return <>
    <div className="main-title-row"><div><p className="eyebrow">{isEnglish ? 'Customer directory' : 'Directorio de clientes'}</p><h2>{isEnglish ? 'Business Customers' : 'Clientes empresariales'}</h2></div><button className="login-button" type="button"><Building2 size={16} /> {isEnglish ? 'Add customer' : 'Agregar cliente'}</button></div>
    {lastDeletionSummary ? <div className="deletion-receipt">
      <strong>{isEnglish ? `Deleted tenant "${lastDeletionSummary.tenantName}"` : `Tenant "${lastDeletionSummary.tenantName}" eliminado`}</strong>
      <p>{isEnglish ? `${lastDeletionSummary.totalRowsDeleted} rows and ${lastDeletionSummary.deletedBlobCount} files removed.` : `${lastDeletionSummary.totalRowsDeleted} filas y ${lastDeletionSummary.deletedBlobCount} archivos eliminados.`}</p>
      <ul className="deletion-receipt-list">{Object.entries(lastDeletionSummary.deletedRowsByTable).filter(([, count]) => count > 0).map(([table, count]) => <li key={table}><span>{table}</span><span>{count}</span></li>)}</ul>
      <button type="button" className="btn-ghost" onClick={() => setLastDeletionSummary(null)}>{isEnglish ? 'Dismiss' : 'Cerrar'}</button>
    </div> : null}
    {isLoading ? <div className="data-message">{isEnglish ? 'Loading customers from SQL...' : 'Cargando clientes desde SQL...'}</div> : error ? <div className="data-message error">{isEnglish ? 'Could not load customers from the database.' : 'No se pudieron cargar los clientes desde la base de datos.'}</div> : tenants.length === 0 ? <div className="data-message">{isEnglish ? 'No business customers found.' : 'No se encontraron clientes empresariales.'}</div> : <div className="customer-grid">{tenants.map((tenant) => <article className="customer-card" key={tenant.tenantId}><div className="customer-card-icon"><Building2 size={21} /></div><div><h3>{tenant.name}</h3><p className="customer-label">{isEnglish ? 'Tenant ID' : 'ID del tenant'}</p><code>{tenant.tenantId}</code></div><span className="customer-status">{tenant.isActive ? (isEnglish ? 'Active' : 'Activo') : (isEnglish ? 'Inactive' : 'Inactivo')}</span><div className="customer-card-actions"><Link to={`/tenants/${tenant.tenantId}`} className="btn-ghost">{isEnglish ? 'Open' : 'Abrir'}</Link><button type="button" className="btn-danger-outline" onClick={() => setTenantToDelete(tenant)}>{isEnglish ? 'Delete' : 'Eliminar'}</button></div></article>)}</div>}
    {tenantToDelete ? <div className="modal-overlay" onClick={closeDeleteDialog}>
      <div className="modal-card modal-danger" onClick={(e) => e.stopPropagation()}>
        <div className="modal-danger-icon">⚠️</div>
        <h2>{isEnglish ? 'Irreversible deletion' : 'Borrado irreversible'}</h2>
        <p className="modal-body-text">{isEnglish
          ? `This will PERMANENTLY delete tenant "${tenantToDelete.name}": all its people, departments, job roles, skills, enrollments, learning progress, gamification data, invitations, and Film/Video Productions (including files). Global/shared courses are NEVER deleted.`
          : `Esto eliminará PERMANENTEMENTE al tenant "${tenantToDelete.name}": todas sus personas, departamentos, puestos, habilidades, inscripciones, progreso de aprendizaje, datos de gamificación, invitaciones y Film/Video Productions (incluyendo archivos). Los cursos globales/compartidos NUNCA se eliminan.`}</p>
        <label className="modal-confirm-label">
          {isEnglish ? "Type the tenant's slug to confirm:" : 'Escribe el slug del tenant para confirmar:'} <code>{tenantToDelete.slug}</code>
        </label>
        <input
          type="text"
          className="modal-confirm-input"
          value={confirmText}
          onChange={(e) => setConfirmText(e.target.value)}
          placeholder={tenantToDelete.slug}
          autoFocus
        />
        {deleteError ? <p className="data-message error modal-error">{deleteError}</p> : null}
        <div className="modal-actions">
          <button type="button" className="btn-ghost" onClick={closeDeleteDialog}>{isEnglish ? 'Cancel' : 'Cancelar'}</button>
          <button
            type="button"
            className="btn-danger"
            disabled={isDeleting || confirmText.trim().toLowerCase() !== tenantToDelete.slug.toLowerCase()}
            onClick={handleConfirmDelete}
          >
            {isDeleting ? (isEnglish ? 'Deleting...' : 'Eliminando...') : (isEnglish ? 'Permanently delete' : 'Eliminar permanentemente')}
          </button>
        </div>
      </div>
    </div> : null}
  </>;
}

type TenantPerson = {
  personId: string;
  displayName: string | null;
  email: string | null;
  jobRoleTitle: string | null;
  departmentName: string | null;
  isActive: boolean;
};

type TenantDashboardStats = {
  peopleCount: number;
  courses: { totalOwnedByTenant: number; totalConsumedByTenant: number; totalGlobal: number; byLanguage: Record<string, number> };
  labs: { ownedByTenant: number; global: number };
  media: { imagesCount: number; videosCount: number };
};

type TenantCourse = {
  courseId: string;
  name: string;
  courseType: string;
  isGlobal: boolean;
  languageCode: string | null;
  createdDate: string;
  executiveSummary?: string | null;
  sectionCount?: number;
  coverIllustrationId?: string | null;
  category?: string | null;
  targetSkill?: string | null;
};

type TenantCourseMediaBreakdown = {
  courseId: string;
  name: string;
  courseType: string;
  imagesCount: number;
  videosCount: number;
};

type TenantCourseTotalsStep = {
  stepName: string;
  models: string;
  inputTokens: number;
  outputTokens: number;
  totalElapsedMilliseconds: number;
  imagesGeneratedCount: number;
  videosGeneratedCount: number;
  estimatedCostUsd: number;
};

type TenantCourseTotals = {
  courseCount: number;
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  totalElapsedMilliseconds: number;
  imagesGeneratedCount: number;
  videosGeneratedCount: number;
  estimatedCostUsd: number;
  engramsCount: number;
  chaptersCount: number;
  factsCount: number;
  steps: TenantCourseTotalsStep[];
};

type TenantCourseConsumptionUsage = {
  channel: string;
  modelName: string | null;
  inputTokens: number;
  outputTokens: number;
  durationSeconds: number;
};

type TenantCourseConsumptionTotals = {
  courseCount: number;
  engramsVisitedCount: number;
  sessionsCount: number;
  voiceSessionCount: number;
  totalTimeSeconds: number;
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  textToSpeechCharacterCount: number;
  textToSpeechEstimatedCostUsd: number;
  estimatedCostUsd: number;
  recall: { attemptsCount: number; masteredCount: number; failedCount: number };
  assessment: { roundsCount: number; passedRounds: number; failedRounds: number; questionsCount: number; incorrectAnswers: number };
  usageByChannel: TenantCourseConsumptionUsage[];
};

type TenantMonthlyCost = {
  yearMonth: string;
  estimatedCostUsd: number;
};

type TenantLabCost = {
  labId: string;
  name: string;
  isGlobal: boolean;
  estimatedCostUsd: number;
};

type TenantBuddyUsage = {
  sessionCount: number;
  totalDurationSeconds: number;
  estimatedCostUsd: number;
};

type TenantQuota = {
  key: string;
  category: string;
  labelEn: string;
  labelEs: string;
  descriptionEn: string;
  descriptionEs: string;
  unitEn: string;
  unitEs: string;
  platformDefault: number;
  effectiveLimit: number;
  isOverridden: boolean;
  isEnforced: boolean;
  estimatedMaxMonthlyCostUsd: number | null;
  costBasisEn: string | null;
  costBasisEs: string | null;
};

type TenantEngramStatRow = {
  structuredLearningDocumentId: string;
  documentName: string;
  structuredLearningDocumentSectionId: string;
  chapterTitle: string;
  subchapterTitle: string;
  totalSessionsVisited: number;
  totalTimeSeconds: number;
  recall: { attemptsCount: number; masteredCount: number; failedCount: number };
  assessment: { roundsCount: number; passedRounds: number; failedRounds: number };
  usage: { channel: string; modelName: string | null; inputTokens: number; outputTokens: number; durationSeconds: number }[];
  totalInputTokens: number;
  totalOutputTokens: number;
};

type TenantLabVoiceCost = {
  labId: string;
  name: string;
  isGlobal: boolean;
  sessionCount: number;
  totalDurationSeconds: number;
  estimatedCostUsd: number;
};

type TenantLabEvaluationCost = {
  labId: string;
  name: string;
  isGlobal: boolean;
  evaluationCount: number;
  inputTokens: number;
  outputTokens: number;
  cachedInputTokens: number;
  estimatedCostUsd: number;
};

const TENANT_DETAIL_TABS = [
  { key: 'users', es: 'Usuarios', en: 'Users', icon: Users },
  { key: 'courses', es: 'Cursos', en: 'Courses', icon: BookOpen },
  { key: 'labs', es: 'Labs', en: 'Labs', icon: FlaskConical },
  { key: 'labCosts', es: 'Costos de Labs', en: 'Labs costs', icon: DollarSign },
  { key: 'media', es: 'Imágenes y videos', en: 'Images & videos', icon: ImageIcon },
  { key: 'buddy', es: 'Uso de Buddy', en: 'Buddy usage', icon: Activity },
  { key: 'engrams', es: 'Engramas', en: 'Engrams', icon: Layers3 },
  { key: 'quotas', es: 'Quotas y límites', en: 'Quotas & limits', icon: Settings },
] as const;

const QUOTA_CATEGORY_ORDER = ['Buddy', 'Courses', 'Voice'] as const;

const QUOTA_CATEGORY_ICON: Record<string, typeof Activity> = {
  Buddy: Activity,
  Courses: BookOpenCheck,
  Voice: Headphones,
};

/** Human-readable labels for StructuredLearningEngramRuntimeUsage.StepType
 * (2026-10-06) — "Production" is shown to the admin as "In your role"
 * (a.k.a. "Apply this lesson your way"), matching the employee-facing tab
 * name, since "Production" alone means nothing to a non-engineer admin. */
const STEP_TYPE_LABELS: Record<string, { en: string; es: string }> = {
  Hypothesis: { en: 'Hypothesis', es: 'Hipótesis' },
  Teaching: { en: 'Teaching', es: 'Enseñanza' },
  DeepDive: { en: 'Deep Dive', es: 'Deep Dive' },
  Production: { en: 'In your role', es: 'En tu rol' },
  Recall: { en: 'Recall', es: 'Recall' },
  Assessment: { en: 'Assessment', es: 'Assessment' },
  AiAgentChat: { en: 'Free chat', es: 'Chat libre' },
  '(unassigned)': { en: 'Unassigned', es: 'Sin asignar' },
};

function TenantDetailPage({ isEnglish }: { isEnglish: boolean }) {
  const { tenantId } = useParams<{ tenantId: string }>();
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<(typeof TENANT_DETAIL_TABS)[number]['key']>('users');
  const [tenantInfo, setTenantInfo] = useState<{ tenantId: string; name: string } | null>(null);
  const [tenantInfoError, setTenantInfoError] = useState<string | null>(null);
  const [people, setPeople] = useState<TenantPerson[] | null>(null);
  const [peopleError, setPeopleError] = useState<string | null>(null);
  const [stats, setStats] = useState<TenantDashboardStats | null>(null);
  const [statsError, setStatsError] = useState<string | null>(null);
  const [courses, setCourses] = useState<TenantCourse[] | null>(null);
  const [catalogCourses, setCatalogCourses] = useState<TenantCourse[] | null>(null);
  const [coursesError, setCoursesError] = useState<string | null>(null);
  const [catalogCoursesError, setCatalogCoursesError] = useState<string | null>(null);
  const [mediaBreakdown, setMediaBreakdown] = useState<TenantCourseMediaBreakdown[] | null>(null);
  const [mediaBreakdownError, setMediaBreakdownError] = useState<string | null>(null);
  const [coursesSubTab, setCoursesSubTab] = useState<'totals' | 'consumed' | 'detail'>('totals');
  const [courseTotals, setCourseTotals] = useState<TenantCourseTotals | null>(null);
  const [courseTotalsError, setCourseTotalsError] = useState<string | null>(null);
  const [courseConsumptionTotals, setCourseConsumptionTotals] = useState<TenantCourseConsumptionTotals | null>(null);
  const [courseConsumptionTotalsError, setCourseConsumptionTotalsError] = useState<string | null>(null);
  const [monthlyCosts, setMonthlyCosts] = useState<TenantMonthlyCost[] | null>(null);
  const [monthlyCostsError, setMonthlyCostsError] = useState<string | null>(null);
  const [labCosts, setLabCosts] = useState<TenantLabCost[] | null>(null);
  const [labCostsError, setLabCostsError] = useState<string | null>(null);
  const [buddyUsage, setBuddyUsage] = useState<TenantBuddyUsage | null>(null);
  const [buddyUsageError, setBuddyUsageError] = useState<string | null>(null);
  const [engramStats, setEngramStats] = useState<TenantEngramStatRow[] | null>(null);
  const [engramStatsError, setEngramStatsError] = useState<string | null>(null);
  const [selectedEngramRow, setSelectedEngramRow] = useState<TenantEngramStatRow | null>(null);
  const [labVoiceCost, setLabVoiceCost] = useState<TenantLabVoiceCost[] | null>(null);
  const [labVoiceCostError, setLabVoiceCostError] = useState<string | null>(null);
  const [labEvaluationCost, setLabEvaluationCost] = useState<TenantLabEvaluationCost[] | null>(null);
  const [labEvaluationCostError, setLabEvaluationCostError] = useState<string | null>(null);
  const [quotas, setQuotas] = useState<TenantQuota[] | null>(null);
  const [quotasError, setQuotasError] = useState<string | null>(null);
  const [quotaDrafts, setQuotaDrafts] = useState<Record<string, string>>({});
  const [savingQuotaKey, setSavingQuotaKey] = useState<string | null>(null);
  const [editingQuotaKey, setEditingQuotaKey] = useState<string | null>(null);

  function loadQuotas() {
    if (!tenantId) return;
    fetch(`/api/tenants/${tenantId}/quotas`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load quotas.');
        return response.json() as Promise<Record<string, unknown>[]>;
      })
      .then((rows) => {
        const mapped = rows.map((row): TenantQuota => ({
          key: String(row.Key ?? row.key ?? ''),
          category: String(row.Category ?? row.category ?? ''),
          labelEn: String(row.LabelEn ?? row.labelEn ?? ''),
          labelEs: String(row.LabelEs ?? row.labelEs ?? ''),
          descriptionEn: String(row.DescriptionEn ?? row.descriptionEn ?? ''),
          descriptionEs: String(row.DescriptionEs ?? row.descriptionEs ?? ''),
          unitEn: String(row.UnitEn ?? row.unitEn ?? ''),
          unitEs: String(row.UnitEs ?? row.unitEs ?? ''),
          platformDefault: Number(row.PlatformDefault ?? row.platformDefault ?? 0),
          effectiveLimit: Number(row.EffectiveLimit ?? row.effectiveLimit ?? 0),
          isOverridden: Boolean(row.IsOverridden ?? row.isOverridden ?? false),
          isEnforced: Boolean(row.IsEnforced ?? row.isEnforced ?? false),
          estimatedMaxMonthlyCostUsd: (row.EstimatedMaxMonthlyCostUsd ?? row.estimatedMaxMonthlyCostUsd ?? null) as number | null,
          costBasisEn: (row.CostBasisEn ?? row.costBasisEn ?? null) as string | null,
          costBasisEs: (row.CostBasisEs ?? row.costBasisEs ?? null) as string | null,
        }));
        setQuotas(mapped);
        setQuotaDrafts(Object.fromEntries(mapped.map((q) => [q.key, String(q.effectiveLimit)])));
      })
      .catch((err: Error) => setQuotasError(err.message));
  }

  async function saveQuota(quotaKey: string) {
    if (!tenantId) return;
    const draft = quotaDrafts[quotaKey];
    const limitValue = draft === '' || draft === undefined ? null : Number(draft);
    if (limitValue !== null && Number.isNaN(limitValue)) return;
    setSavingQuotaKey(quotaKey);
    try {
      const response = await fetch(`/api/tenants/${tenantId}/quotas/${encodeURIComponent(quotaKey)}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ limitValue }),
      });
      if (!response.ok) throw new Error('Unable to save quota.');
      loadQuotas();
      setEditingQuotaKey(null);
    } catch (err) {
      setQuotasError((err as Error).message);
    } finally {
      setSavingQuotaKey(null);
    }
  }

  function resetQuotaToDefault(quotaKey: string) {
    setQuotaDrafts((current) => {
      const quota = quotas?.find((q) => q.key === quotaKey);
      return { ...current, [quotaKey]: quota ? String(quota.platformDefault) : current[quotaKey] };
    });
  }

  useEffect(() => {
    loadQuotas();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tenantId]);

  useEffect(() => {
    if (!tenantId) return;
    setTenantInfo(null);
    setTenantInfoError(null);
    fetch(`/api/tenants/${tenantId}`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load tenant.');
        return response.json() as Promise<Record<string, unknown>>;
      })
      .then((row) => setTenantInfo({
        tenantId: String(row.TenantId ?? row.tenantId ?? tenantId),
        name: String(row.Name ?? row.name ?? ''),
      }))
      .catch((err: Error) => setTenantInfoError(err.message));

    setCourses(null);
    setCatalogCourses(null);
    setCoursesError(null);
    setCatalogCoursesError(null);
    fetch(`/api/tenants/${tenantId}/people`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load people.');
        return response.json() as Promise<Record<string, unknown>[]>;
      })
      .then((rows) => setPeople(rows.map((row) => ({
        personId: String(row.PersonId ?? row.personId ?? ''),
        displayName: (row.DisplayName ?? row.displayName ?? null) as string | null,
        email: (row.Email ?? row.email ?? null) as string | null,
        jobRoleTitle: (row.JobRoleTitle ?? row.jobRoleTitle ?? null) as string | null,
        departmentName: (row.DepartmentName ?? row.departmentName ?? null) as string | null,
        isActive: Boolean(row.IsActive ?? row.isActive ?? false),
      }))))
      .catch((err: Error) => setPeopleError(err.message));

    fetch(`/api/tenants/${tenantId}/dashboard-stats`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load tenant stats.');
        return response.json() as Promise<TenantDashboardStats>;
      })
      .then(setStats)
      .catch((err: Error) => setStatsError(err.message));

    fetch(`/api/tenants/${tenantId}/courses`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load tenant courses.');
        return response.json() as Promise<Record<string, unknown>[]>;
      })
      .then((rows) => setCourses(rows.map((row) => ({
        courseId: String(row.CourseId ?? row.courseId ?? ''),
        name: String(row.Name ?? row.name ?? ''),
        courseType: String(row.CourseType ?? row.courseType ?? ''),
        isGlobal: Boolean(row.IsGlobal ?? row.isGlobal ?? false),
        languageCode: (row.LanguageCode ?? row.languageCode ?? null) as string | null,
        createdDate: String(row.CreatedDate ?? row.createdDate ?? ''),
      }))))
      .catch((err: Error) => setCoursesError(err.message));

    fetch(`/api/catalog/structured-learning?tenantId=${encodeURIComponent(tenantId)}`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load course catalog.');
        return response.json() as Promise<Record<string, unknown>[]>;
      })
      .then((rows) => setCatalogCourses(rows.map((row) => ({
        courseId: String(row.StructuredLearningDocumentId ?? row.structuredLearningDocumentId ?? ''),
        name: String(row.DocumentName ?? row.documentName ?? ''),
        courseType: 'StructuredLearning',
        isGlobal: true,
        languageCode: (row.Language ?? row.language ?? null) as string | null,
        createdDate: String(row.CreatedDate ?? row.createdDate ?? ''),
        executiveSummary: (row.ExecutiveSummary ?? row.executiveSummary ?? null) as string | null,
        sectionCount: Number(row.SectionCount ?? row.sectionCount ?? 0),
        coverIllustrationId: (row.CoverIllustrationId ?? row.coverIllustrationId ?? null) as string | null,
        category: (row.Category ?? row.category ?? null) as string | null,
        targetSkill: (row.TargetSkill ?? row.targetSkill ?? null) as string | null,
      }))))
      .catch((err: Error) => setCatalogCoursesError(err.message));

    fetch(`/api/tenants/${tenantId}/media-breakdown`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load media breakdown.');
        return response.json() as Promise<Record<string, unknown>[]>;
      })
      .then((rows) => setMediaBreakdown(rows.map((row) => ({
        courseId: String(row.CourseId ?? row.courseId ?? ''),
        name: String(row.Name ?? row.name ?? ''),
        courseType: String(row.CourseType ?? row.courseType ?? ''),
        imagesCount: Number(row.ImagesCount ?? row.imagesCount ?? 0),
        videosCount: Number(row.VideosCount ?? row.videosCount ?? 0),
      }))))
      .catch((err: Error) => setMediaBreakdownError(err.message));

    fetch(`/api/tenants/${tenantId}/course-totals`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load course totals.');
        return response.json() as Promise<Record<string, unknown>>;
      })
      .then((row) => {
        const rawSteps = (row.Steps ?? row.steps ?? []) as Record<string, unknown>[];
        setCourseTotals({
          courseCount: Number(row.CourseCount ?? row.courseCount ?? 0),
          inputTokens: Number(row.InputTokens ?? row.inputTokens ?? 0),
          outputTokens: Number(row.OutputTokens ?? row.outputTokens ?? 0),
          totalTokens: Number(row.TotalTokens ?? row.totalTokens ?? 0),
          totalElapsedMilliseconds: Number(row.TotalElapsedMilliseconds ?? row.totalElapsedMilliseconds ?? 0),
          imagesGeneratedCount: Number(row.ImagesGeneratedCount ?? row.imagesGeneratedCount ?? 0),
          videosGeneratedCount: Number(row.VideosGeneratedCount ?? row.videosGeneratedCount ?? 0),
          estimatedCostUsd: Number(row.EstimatedCostUsd ?? row.estimatedCostUsd ?? 0),
          engramsCount: Number(row.EngramsCount ?? row.engramsCount ?? 0),
          chaptersCount: Number(row.ChaptersCount ?? row.chaptersCount ?? 0),
          factsCount: Number(row.FactsCount ?? row.factsCount ?? 0),
          steps: rawSteps.map((s) => ({
            stepName: String(s.StepName ?? s.stepName ?? ''),
            models: String(s.Models ?? s.models ?? ''),
            inputTokens: Number(s.InputTokens ?? s.inputTokens ?? 0),
            outputTokens: Number(s.OutputTokens ?? s.outputTokens ?? 0),
            totalElapsedMilliseconds: Number(s.TotalElapsedMilliseconds ?? s.totalElapsedMilliseconds ?? 0),
            imagesGeneratedCount: Number(s.ImagesGeneratedCount ?? s.imagesGeneratedCount ?? 0),
            videosGeneratedCount: Number(s.VideosGeneratedCount ?? s.videosGeneratedCount ?? 0),
            estimatedCostUsd: Number(s.EstimatedCostUsd ?? s.estimatedCostUsd ?? 0),
          })),
        });
      })
      .catch((err: Error) => setCourseTotalsError(err.message));

    fetch(`/api/tenants/${tenantId}/course-consumption-totals`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load course consumption totals.');
        return response.json() as Promise<Record<string, unknown>>;
      })
      .then((row) => {
        const rawUsage = (row.UsageByChannel ?? row.usageByChannel ?? []) as Record<string, unknown>[];
        const rawRecall = (row.Recall ?? row.recall ?? {}) as Record<string, unknown>;
        const rawAssessment = (row.Assessment ?? row.assessment ?? {}) as Record<string, unknown>;
        setCourseConsumptionTotals({
          courseCount: Number(row.CourseCount ?? row.courseCount ?? 0),
          engramsVisitedCount: Number(row.EngramsVisitedCount ?? row.engramsVisitedCount ?? 0),
          sessionsCount: Number(row.SessionsCount ?? row.sessionsCount ?? 0),
          voiceSessionCount: Number(row.VoiceSessionCount ?? row.voiceSessionCount ?? 0),
          totalTimeSeconds: Number(row.TotalTimeSeconds ?? row.totalTimeSeconds ?? 0),
          inputTokens: Number(row.InputTokens ?? row.inputTokens ?? 0),
          outputTokens: Number(row.OutputTokens ?? row.outputTokens ?? 0),
          totalTokens: Number(row.TotalTokens ?? row.totalTokens ?? 0),
          textToSpeechCharacterCount: Number(row.TextToSpeechCharacterCount ?? row.textToSpeechCharacterCount ?? 0),
          textToSpeechEstimatedCostUsd: Number(row.TextToSpeechEstimatedCostUsd ?? row.textToSpeechEstimatedCostUsd ?? 0),
          estimatedCostUsd: Number(row.EstimatedCostUsd ?? row.estimatedCostUsd ?? 0),
          recall: {
            attemptsCount: Number(rawRecall.AttemptsCount ?? rawRecall.attemptsCount ?? 0),
            masteredCount: Number(rawRecall.MasteredCount ?? rawRecall.masteredCount ?? 0),
            failedCount: Number(rawRecall.FailedCount ?? rawRecall.failedCount ?? 0),
          },
          assessment: {
            roundsCount: Number(rawAssessment.RoundsCount ?? rawAssessment.roundsCount ?? 0),
            passedRounds: Number(rawAssessment.PassedRounds ?? rawAssessment.passedRounds ?? 0),
            failedRounds: Number(rawAssessment.FailedRounds ?? rawAssessment.failedRounds ?? 0),
            questionsCount: Number(rawAssessment.QuestionsCount ?? rawAssessment.questionsCount ?? 0),
            incorrectAnswers: Number(rawAssessment.IncorrectAnswers ?? rawAssessment.incorrectAnswers ?? 0),
          },
          usageByChannel: rawUsage.map((u) => ({
            channel: String(u.Channel ?? u.channel ?? ''),
            modelName: (u.ModelName ?? u.modelName ?? null) as string | null,
            inputTokens: Number(u.InputTokens ?? u.inputTokens ?? 0),
            outputTokens: Number(u.OutputTokens ?? u.outputTokens ?? 0),
            durationSeconds: Number(u.DurationSeconds ?? u.durationSeconds ?? 0),
          })),
        });
      })
      .catch((err: Error) => setCourseConsumptionTotalsError(err.message));

    fetch(`/api/tenants/${tenantId}/monthly-costs`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load monthly costs.');
        return response.json() as Promise<Record<string, unknown>[]>;
      })
      .then((rows) => setMonthlyCosts(rows.map((row) => ({
        yearMonth: String(row.YearMonth ?? row.yearMonth ?? ''),
        estimatedCostUsd: Number(row.EstimatedCostUsd ?? row.estimatedCostUsd ?? 0),
      }))))
      .catch((err: Error) => setMonthlyCostsError(err.message));

    fetch(`/api/tenants/${tenantId}/lab-costs`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load lab costs.');
        return response.json() as Promise<Record<string, unknown>[]>;
      })
      .then((rows) => setLabCosts(rows.map((row) => ({
        labId: String(row.LabId ?? row.labId ?? ''),
        name: String(row.Name ?? row.name ?? ''),
        isGlobal: Boolean(row.IsGlobal ?? row.isGlobal ?? false),
        estimatedCostUsd: Number(row.EstimatedCostUsd ?? row.estimatedCostUsd ?? 0),
      }))))
      .catch((err: Error) => setLabCostsError(err.message));

    fetch(`/api/tenants/${tenantId}/buddy-usage`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load Buddy usage.');
        return response.json() as Promise<Record<string, unknown>>;
      })
      .then((row) => setBuddyUsage({
        sessionCount: Number(row.SessionCount ?? row.sessionCount ?? 0),
        totalDurationSeconds: Number(row.TotalDurationSeconds ?? row.totalDurationSeconds ?? 0),
        estimatedCostUsd: Number(row.EstimatedCostUsd ?? row.estimatedCostUsd ?? 0),
      }))
      .catch((err: Error) => setBuddyUsageError(err.message));

    fetch(`/api/tenants/${tenantId}/structured-learning-engram-stats`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load engram stats.');
        return response.json() as Promise<Record<string, unknown>[]>;
      })
      .then((rows) => setEngramStats(rows.map((row) => ({
        structuredLearningDocumentId: String(row.StructuredLearningDocumentId ?? row.structuredLearningDocumentId ?? ''),
        documentName: String(row.DocumentName ?? row.documentName ?? ''),
        structuredLearningDocumentSectionId: String(row.StructuredLearningDocumentSectionId ?? row.structuredLearningDocumentSectionId ?? ''),
        chapterTitle: String(row.ChapterTitle ?? row.chapterTitle ?? ''),
        subchapterTitle: String(row.SubchapterTitle ?? row.subchapterTitle ?? ''),
        totalSessionsVisited: Number(row.TotalSessionsVisited ?? row.totalSessionsVisited ?? 0),
        totalTimeSeconds: Number(row.TotalTimeSeconds ?? row.totalTimeSeconds ?? 0),
        recall: {
          attemptsCount: Number((row.Recall as Record<string, unknown> ?? row.recall as Record<string, unknown>)?.AttemptsCount ?? (row.Recall as Record<string, unknown> ?? row.recall as Record<string, unknown>)?.attemptsCount ?? 0),
          masteredCount: Number((row.Recall as Record<string, unknown> ?? row.recall as Record<string, unknown>)?.MasteredCount ?? (row.Recall as Record<string, unknown> ?? row.recall as Record<string, unknown>)?.masteredCount ?? 0),
          failedCount: Number((row.Recall as Record<string, unknown> ?? row.recall as Record<string, unknown>)?.FailedCount ?? (row.Recall as Record<string, unknown> ?? row.recall as Record<string, unknown>)?.failedCount ?? 0),
        },
        assessment: {
          roundsCount: Number((row.Assessment as Record<string, unknown> ?? row.assessment as Record<string, unknown>)?.RoundsCount ?? (row.Assessment as Record<string, unknown> ?? row.assessment as Record<string, unknown>)?.roundsCount ?? 0),
          passedRounds: Number((row.Assessment as Record<string, unknown> ?? row.assessment as Record<string, unknown>)?.PassedRounds ?? (row.Assessment as Record<string, unknown> ?? row.assessment as Record<string, unknown>)?.passedRounds ?? 0),
          failedRounds: Number((row.Assessment as Record<string, unknown> ?? row.assessment as Record<string, unknown>)?.FailedRounds ?? (row.Assessment as Record<string, unknown> ?? row.assessment as Record<string, unknown>)?.failedRounds ?? 0),
        },
        totalInputTokens: Number(row.TotalInputTokens ?? row.totalInputTokens ?? 0),
        totalOutputTokens: Number(row.TotalOutputTokens ?? row.totalOutputTokens ?? 0),
        usage: ((row.Usage ?? row.usage ?? []) as Record<string, unknown>[]).map((u) => ({
          channel: String(u.Channel ?? u.channel ?? ''),
          modelName: (u.ModelName ?? u.modelName ?? null) as string | null,
          inputTokens: Number(u.InputTokens ?? u.inputTokens ?? 0),
          outputTokens: Number(u.OutputTokens ?? u.outputTokens ?? 0),
          durationSeconds: Number(u.DurationSeconds ?? u.durationSeconds ?? 0),
        })),
      }))))
      .catch((err: Error) => setEngramStatsError(err.message));

    fetch(`/api/tenants/${tenantId}/lab-voice-costs`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load Lab voice agent cost.');
        return response.json() as Promise<Record<string, unknown>[]>;
      })
      .then((rows) => setLabVoiceCost(rows.map((row) => ({
        labId: String(row.LabId ?? row.labId ?? ''),
        name: String(row.Name ?? row.name ?? ''),
        isGlobal: Boolean(row.IsGlobal ?? row.isGlobal ?? false),
        sessionCount: Number(row.SessionCount ?? row.sessionCount ?? 0),
        totalDurationSeconds: Number(row.TotalDurationSeconds ?? row.totalDurationSeconds ?? 0),
        estimatedCostUsd: Number(row.EstimatedCostUsd ?? row.estimatedCostUsd ?? 0),
      }))))
      .catch((err: Error) => setLabVoiceCostError(err.message));

    fetch(`/api/tenants/${tenantId}/lab-evaluation-costs`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load Lab evaluation cost.');
        return response.json() as Promise<Record<string, unknown>[]>;
      })
      .then((rows) => setLabEvaluationCost(rows.map((row) => ({
        labId: String(row.LabId ?? row.labId ?? ''),
        name: String(row.Name ?? row.name ?? ''),
        isGlobal: Boolean(row.IsGlobal ?? row.isGlobal ?? false),
        evaluationCount: Number(row.EvaluationCount ?? row.evaluationCount ?? 0),
        inputTokens: Number(row.InputTokens ?? row.inputTokens ?? 0),
        outputTokens: Number(row.OutputTokens ?? row.outputTokens ?? 0),
        cachedInputTokens: Number(row.CachedInputTokens ?? row.cachedInputTokens ?? 0),
        estimatedCostUsd: Number(row.EstimatedCostUsd ?? row.estimatedCostUsd ?? 0),
      }))))
      .catch((err: Error) => setLabEvaluationCostError(err.message));
  }, [tenantId]);

  const displayedCourses = [
    ...(courses ?? []),
    ...(catalogCourses ?? []).filter((catalogCourse) => !(courses ?? []).some((course) => course.courseId === catalogCourse.courseId)),
  ];
  return <>
    <div className="main-title-row">
      <div>
        <p className="eyebrow">{isEnglish ? 'Tenant detail' : 'Detalle del cliente'}</p>
        <h2>{isEnglish ? 'Tenant dashboard' : 'Panel del tenant'}</h2>
        {tenantInfoError ? (
          <p className="customer-label">{isEnglish ? 'Unable to load tenant name.' : 'No se pudo cargar el nombre del tenant.'}</p>
        ) : (
          <p className="customer-label">
            {tenantInfo?.name || (isEnglish ? 'Loading tenant...' : 'Cargando tenant...')}
            {' · '}
            <code>{tenantId}</code>
          </p>
        )}
      </div>
      <button type="button" className="btn-ghost" onClick={() => navigate('/business-customers')}>
        {isEnglish ? '← Back to customers' : '← Volver a clientes'}
      </button>
    </div>

    <div className="tenant-tabs">
      {TENANT_DETAIL_TABS.map(({ key, es, en, icon: Icon }) => (
        <button
          key={key}
          type="button"
          className={`tenant-tab${activeTab === key ? ' active' : ''}`}
          onClick={() => setActiveTab(key)}
        >
          <Icon size={16} /> {isEnglish ? en : es}
        </button>
      ))}
    </div>

    {activeTab === 'users' ? (
      peopleError ? <div className="data-message error">{peopleError}</div>
      : people === null ? <div className="data-message">{isEnglish ? 'Loading people...' : 'Cargando personas...'}</div>
      : people.length === 0 ? <div className="data-message">{isEnglish ? 'No people found for this tenant.' : 'No se encontraron personas para este tenant.'}</div>
      : <div className="customer-grid">{people.map((person) => (
          <Link to={`/tenants/${tenantId}/people/${person.personId}`} className="customer-card customer-card-link" key={person.personId}>
            <div className="customer-card-icon"><Users size={21} /></div>
            <div>
              <h3>{person.displayName ?? (isEnglish ? 'Unnamed person' : 'Sin nombre')}</h3>
              <p className="customer-label">{isEnglish ? 'Email' : 'Correo'}</p>
              <code>{person.email ?? '—'}</code>
              <p className="customer-label">{isEnglish ? 'Role / Department' : 'Puesto / Departamento'}</p>
              <code>{person.jobRoleTitle ?? '—'} · {person.departmentName ?? '—'}</code>
            </div>
            <span className="customer-status">{person.isActive ? (isEnglish ? 'Active' : 'Activo') : (isEnglish ? 'Inactive' : 'Inactivo')}</span>
          </Link>
        ))}</div>
    ) : null}

    {activeTab === 'courses' ? (
      <>
        <div className="tenant-subtabs">
          <button type="button" className={`tenant-subtab${coursesSubTab === 'totals' ? ' active' : ''}`} onClick={() => setCoursesSubTab('totals')}>
            {isEnglish ? 'Courses created (totals)' : 'Cursos generados (totales)'}
          </button>
          <button type="button" className={`tenant-subtab${coursesSubTab === 'consumed' ? ' active' : ''}`} onClick={() => setCoursesSubTab('consumed')}>
            {isEnglish ? 'Courses taken (totals)' : 'Cursos tomados (totales)'}
          </button>
          <button type="button" className={`tenant-subtab${coursesSubTab === 'detail' ? ' active' : ''}`} onClick={() => setCoursesSubTab('detail')}>
            {isEnglish ? 'Details' : 'Cursos detalle'}
          </button>
        </div>

        {coursesSubTab === 'totals' ? (
          courseTotalsError ? <div className="data-message error">{courseTotalsError}</div>
          : courseTotals === null ? <div className="data-message">{isEnglish ? 'Loading totals...' : 'Cargando totales...'}</div>
          : <TenantCourseTotalsView
              totals={courseTotals}
              consumedCourseCount={stats?.courses.totalConsumedByTenant ?? null}
              consumedCourseError={statsError}
              monthlyCosts={monthlyCosts}
              monthlyCostsError={monthlyCostsError}
              isEnglish={isEnglish}
            />
        ) : coursesSubTab === 'consumed' ? (
          courseConsumptionTotalsError ? <div className="data-message error">{courseConsumptionTotalsError}</div>
          : courseConsumptionTotals === null ? <div className="data-message">{isEnglish ? 'Loading consumption totals...' : 'Cargando totales de consumo...'}</div>
          : <TenantCourseConsumptionTotalsView totals={courseConsumptionTotals} isEnglish={isEnglish} />
        ) : (
          statsError ? <div className="data-message error">{statsError}</div>
          : stats === null ? <div className="data-message">{isEnglish ? 'Loading stats...' : 'Cargando estadísticas...'}</div>
          : <>
            <div className="stat-card-grid tenant-section-gap">
              <div className="stat-card"><p className="customer-label">{isEnglish ? 'Created by this tenant' : 'Creados por este tenant'}</p><h3>{stats.courses.totalOwnedByTenant}</h3></div>
              <div className="stat-card"><p className="customer-label">{isEnglish ? 'Global (shared) courses' : 'Cursos globales (compartidos)'}</p><h3>{stats.courses.totalGlobal}</h3></div>
              {Object.entries(stats.courses.byLanguage).map(([lang, count]) => (
                <div className="stat-card" key={lang}><p className="customer-label">{isEnglish ? `Language: ${lang}` : `Idioma: ${lang}`}</p><h3>{count}</h3></div>
              ))}
              <div className="stat-card stat-card-wide">
                <p className="customer-label">{isEnglish ? 'Source' : 'Fuente'}</p>
                <a href="http://localhost:3010/catalog" target="_blank" rel="noreferrer">HumanOS Studio — /catalog ↗</a>
              </div>
            </div>
            {coursesError || catalogCoursesError ? <div className="data-message error">{coursesError ?? catalogCoursesError}</div>
            : courses === null ? <div className="data-message">{isEnglish ? 'Loading courses...' : 'Cargando cursos...'}</div>
            : displayedCourses.length === 0 ? <div className="data-message">{isEnglish ? 'No courses are available in the catalog yet.' : 'Aún no hay cursos disponibles en el catálogo.'}</div>
            : <div className="catalog-course-grid">{displayedCourses.map((course) => (
                <Link to={`/tenants/${tenantId}/courses/${course.courseType}/${course.courseId}`} className="catalog-course-card" key={course.courseId}>
                  <div className="catalog-course-cover">
                    {course.coverIllustrationId ? <img src={`/api/illustrations/structured-learning/${course.coverIllustrationId}/image`} alt="" /> : <BookOpen size={28} />}
                    <span className="catalog-course-badge">{course.category || (isEnglish ? 'Course' : 'Curso')}</span>
                    <span className="catalog-course-global">{isEnglish ? 'Catalog' : 'Catálogo'}</span>
                  </div>
                  <div className="catalog-course-content">
                    <h3>{course.name}</h3>
                    {course.targetSkill ? <p className="catalog-course-skill">✦ {course.targetSkill}</p> : null}
                    <p className="catalog-course-description">{course.executiveSummary || (isEnglish ? 'Interactive structured learning course.' : 'Curso interactivo de aprendizaje estructurado.')}</p>
                    <p className="catalog-course-meta">{course.sectionCount ?? 0} {isEnglish ? 'engrams' : 'engramas'} · {course.languageCode || 'es'}</p>
                  </div>
                  <div className="catalog-course-action">{isEnglish ? 'View statistics →' : 'Ver estadísticas →'}</div>
                </Link>
              ))}</div>}
          </>
        )}
      </>
    ) : null}

    {activeTab === 'labs' ? (
      statsError ? <div className="data-message error">{statsError}</div>
      : stats === null ? <div className="data-message">{isEnglish ? 'Loading stats...' : 'Cargando estadísticas...'}</div>
      : <>
        <div className="stat-card-grid">
          <div className="stat-card"><p className="customer-label">{isEnglish ? 'Created by this tenant' : 'Creados por este tenant'}</p><h3>{stats.labs.ownedByTenant}</h3></div>
          <div className="stat-card"><p className="customer-label">{isEnglish ? 'Global (shared) labs' : 'Labs globales (compartidos)'}</p><h3>{stats.labs.global}</h3></div>
        </div>
        <div className="tenant-section-gap">
          <p className="customer-label">{isEnglish ? 'Cost by lab' : 'Costo por lab'}</p>
          {labCostsError ? <div className="data-message error">{labCostsError}</div>
          : labCosts === null ? <div className="data-message">{isEnglish ? 'Loading lab costs...' : 'Cargando costos de labs...'}</div>
          : labCosts.length === 0 ? <div className="data-message">{isEnglish ? 'No labs with generation cost yet.' : 'Aún no hay labs con costo de generación.'}</div>
          : <div className="cost-table-wrap">
              <table className="cost-table">
                <thead>
                  <tr>
                    <th>{isEnglish ? 'Lab' : 'Lab'}</th>
                    <th>{isEnglish ? 'Scope' : 'Alcance'}</th>
                    <th>{isEnglish ? 'Est. cost' : 'Costo est.'}</th>
                  </tr>
                </thead>
                <tbody>
                  {labCosts.map((lab) => (
                    <tr key={lab.labId}>
                      <td>{lab.name}</td>
                      <td>{lab.isGlobal ? (isEnglish ? 'Global' : 'Global') : (isEnglish ? 'Own' : 'Propio')}</td>
                      <td>{formatUsd(lab.estimatedCostUsd)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>}
        </div>
      </>
    ) : null}

    {activeTab === 'media' ? (
      statsError ? <div className="data-message error">{statsError}</div>
      : stats === null ? <div className="data-message">{isEnglish ? 'Loading stats...' : 'Cargando estadísticas...'}</div>
      : <>
        <div className="stat-card-grid">
          <div className="stat-card"><p className="customer-label">{isEnglish ? 'Images generated' : 'Imágenes generadas'}</p><h3>{stats.media.imagesCount}</h3></div>
          <div className="stat-card"><p className="customer-label">{isEnglish ? 'Videos generated' : 'Videos generados'}</p><h3>{stats.media.videosCount}</h3></div>
        </div>
        <div className="tenant-section-gap">
          <p className="customer-label">{isEnglish ? 'Top 20 courses by images & videos generated' : 'Top 20 cursos por imágenes y videos generados'}</p>
          {mediaBreakdownError ? <div className="data-message error">{mediaBreakdownError}</div>
          : mediaBreakdown === null ? <div className="data-message">{isEnglish ? 'Loading histogram...' : 'Cargando histograma...'}</div>
          : mediaBreakdown.length === 0 ? <div className="data-message">{isEnglish ? 'No media generated yet.' : 'Aún no se ha generado contenido multimedia.'}</div>
          : <>
            <MediaHistogramStats courses={mediaBreakdown} isEnglish={isEnglish} />
            <MediaHistogram courses={mediaBreakdown} isEnglish={isEnglish} />
          </>}
        </div>
      </>
    ) : null}

    {activeTab === 'buddy' ? (
      <div className="tenant-section-gap">
        {buddyUsageError ? <div className="data-message error">{buddyUsageError}</div>
        : buddyUsage === null ? <div className="data-message">{isEnglish ? 'Loading Buddy usage...' : 'Cargando uso de Buddy...'}</div>
        : <div className="cost-hero">
            <div className="cost-hero-stat"><Users size={18} /><span>{buddyUsage.sessionCount} {isEnglish ? 'calls' : 'llamadas'}</span></div>
            <div className="cost-hero-stat"><Clock size={18} /><span>{formatSeconds(buddyUsage.totalDurationSeconds * 1000)}</span></div>
            <div className="cost-hero-stat cost-hero-total"><DollarSign size={18} /><span>{formatUsd(buddyUsage.estimatedCostUsd)} ({isEnglish ? 'est.' : 'estimado'})</span></div>
          </div>}
      </div>
    ) : null}

    {activeTab === 'engrams' ? (
      <div className="tenant-section-gap">
        {engramStatsError ? <div className="data-message error">{engramStatsError}</div>
        : engramStats === null ? <div className="data-message">{isEnglish ? 'Loading engram stats...' : 'Cargando estadísticas de engramas...'}</div>
        : engramStats.length === 0 ? <div className="data-message">{isEnglish ? 'No engram activity recorded yet for this tenant.' : 'Aún no hay actividad de engramas registrada para este tenant.'}</div>
        : <div className="customer-grid">
            {engramStats.map((row) => (
              <article
                className="customer-card customer-card-link"
                key={row.structuredLearningDocumentSectionId}
                role="button"
                tabIndex={0}
                onClick={() => setSelectedEngramRow(row)}
                onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') setSelectedEngramRow(row); }}
              >
                <div className="customer-card-icon"><Layers3 size={21} /></div>
                <div>
                  <h3>{row.chapterTitle}{row.subchapterTitle && row.subchapterTitle !== row.chapterTitle ? ` · ${row.subchapterTitle}` : ''}</h3>
                  <p className="customer-label">{isEnglish ? 'Course' : 'Curso'}</p>
                  <code>{row.documentName}</code>
                </div>
                <span className="customer-status">{row.totalSessionsVisited} {isEnglish ? 'sessions' : 'sesiones'}</span>
              </article>
            ))}
          </div>}
      </div>
    ) : null}

    {activeTab === 'quotas' ? (
      <div className="tenant-section-gap">
        <p className="customer-label">
          {isEnglish
            ? 'Service limits for this tenant — same pattern as Azure/Microsoft 365 quotas. An empty value falls back to the platform default.'
            : 'Límites de servicio para este tenant — mismo patrón que las cuotas de Azure/Microsoft 365. Un valor vacío usa el valor por defecto de la plataforma.'}
        </p>

        {quotas && stats ? (
          <div className="cost-hero">
            <div className="cost-hero-stat">
              <Users size={18} />
              <span>{stats.peopleCount} {isEnglish ? 'employees' : 'empleados'}</span>
            </div>
            <div className="cost-hero-stat cost-hero-total">
              <DollarSign size={18} />
              <span>
                {formatUsdPrecise(
                  quotas.reduce((sum, q) => sum + (q.estimatedMaxMonthlyCostUsd ?? 0), 0) * stats.peopleCount
                )} {isEnglish ? '/ month (worst case, all employees maxed out)' : '/ mes (peor caso, todos los empleados al máximo)'}
              </span>
            </div>
          </div>
        ) : null}

        {quotasError ? <div className="data-message error">{quotasError}</div>
        : quotas === null ? <div className="data-message">{isEnglish ? 'Loading quotas...' : 'Cargando quotas...'}</div>
        : QUOTA_CATEGORY_ORDER.filter((category) => quotas.some((q) => q.category === category)).map((category) => {
            const CategoryIcon = QUOTA_CATEGORY_ICON[category] ?? Settings;
            const categoryQuotas = quotas.filter((q) => q.category === category);
            const categoryCostPerEmployee = categoryQuotas.reduce((sum, q) => sum + (q.estimatedMaxMonthlyCostUsd ?? 0), 0);
            const hasCategoryCost = categoryQuotas.some((q) => q.estimatedMaxMonthlyCostUsd !== null);
            const isPerEmployeeCategory = category === 'Buddy';
            return (
              <div className="quota-category-group" key={category}>
                <div className="quota-category-header">
                  <p className="quota-category-title"><CategoryIcon size={16} /> {category}</p>
                  {hasCategoryCost ? (
                    <p className="quota-category-cost">
                      {isEnglish ? 'Max total: ' : 'Máx. total: '}
                      <strong>{formatUsdPrecise(categoryCostPerEmployee)}</strong>
                      {isPerEmployeeCategory ? (isEnglish ? ' / employee / month' : ' / empleado / mes') : null}
                      {isPerEmployeeCategory && stats ? (
                        <span className="quota-card-cost-total"> ({formatUsdPrecise(categoryCostPerEmployee * stats.peopleCount)} {isEnglish ? 'tenant-wide' : 'todo el tenant'})</span>
                      ) : null}
                    </p>
                  ) : null}
                </div>
                <div className="quota-card-grid">
                  {categoryQuotas.map((quota) => {
                    const isEditing = editingQuotaKey === quota.key;
                    const isSaving = savingQuotaKey === quota.key;
                    return (
                      <article className="quota-card" key={quota.key}>
                        <div className="quota-card-header">
                          <div className="quota-card-icon"><CategoryIcon size={20} /></div>
                          <h3 className="quota-card-title">{isEnglish ? quota.labelEn : quota.labelEs}</h3>
                        </div>

                        <p className="quota-card-description">{isEnglish ? quota.descriptionEn : quota.descriptionEs}</p>

                        <div className="quota-card-limit-row">
                          {isEditing ? (
                            <input
                              type="number"
                              min={0}
                              autoFocus
                              className="quota-card-input"
                              value={quotaDrafts[quota.key] ?? ''}
                              onChange={(e) => setQuotaDrafts((current) => ({ ...current, [quota.key]: e.target.value }))}
                            />
                          ) : (
                            <span className="quota-card-limit-value">{quota.effectiveLimit.toLocaleString()}</span>
                          )}
                          <span className="quota-card-limit-unit">{isEnglish ? quota.unitEn : quota.unitEs}</span>
                        </div>

                        {quota.estimatedMaxMonthlyCostUsd !== null ? (
                          <p className="quota-card-cost">
                            {quota.category === 'Buddy'
                              ? (isEnglish ? 'Max cost if fully used: ' : 'Costo máx. si se agota: ')
                              : (isEnglish ? 'Est. max cost at this limit: ' : 'Costo máx. est. en este límite: ')}
                            <strong>{formatUsdPrecise(quota.estimatedMaxMonthlyCostUsd)}</strong>
                            {quota.category === 'Buddy'
                              ? (isEnglish ? ' / employee / month' : ' / empleado / mes')
                              : ` (${quota.effectiveLimit.toLocaleString()} ${isEnglish ? quota.unitEn : quota.unitEs})`}
                            {quota.category === 'Buddy' && stats ? (
                              <span className="quota-card-cost-total">
                                {' '}({formatUsdPrecise(quota.estimatedMaxMonthlyCostUsd * stats.peopleCount)} {isEnglish ? 'tenant-wide' : 'todo el tenant'})
                              </span>
                            ) : null}
                          </p>
                        ) : null}
                        {quota.costBasisEn ? (
                          <p className="quota-card-cost-basis">{isEnglish ? quota.costBasisEn : quota.costBasisEs}</p>
                        ) : null}

                        <div className="quota-card-badges">
                          <span className={`quota-badge ${quota.isEnforced ? 'quota-badge-enforced' : 'quota-badge-not-enforced'}`}>
                            {quota.isEnforced ? (isEnglish ? 'Enforced' : 'Aplicado') : (isEnglish ? 'Not yet enforced' : 'Aún sin aplicar')}
                          </span>
                          <span className={`quota-badge ${quota.isOverridden ? 'quota-badge-custom' : 'quota-badge-default'}`}>
                            {quota.isOverridden
                              ? (isEnglish ? 'Custom' : 'Personalizado')
                              : `${isEnglish ? 'Platform default' : 'Default de plataforma'}: ${quota.platformDefault}`}
                          </span>
                        </div>

                        <div className="quota-card-actions">
                          {isEditing ? (
                            <>
                              <button type="button" className="btn-ghost" disabled={isSaving} onClick={() => saveQuota(quota.key)}>
                                {isSaving ? (isEnglish ? 'Saving...' : 'Guardando...') : (isEnglish ? 'Save' : 'Guardar')}
                              </button>
                              <button
                                type="button"
                                className="btn-ghost"
                                disabled={isSaving}
                                onClick={() => { setEditingQuotaKey(null); setQuotaDrafts((current) => ({ ...current, [quota.key]: String(quota.effectiveLimit) })); }}
                              >
                                {isEnglish ? 'Cancel' : 'Cancelar'}
                              </button>
                            </>
                          ) : (
                            <>
                              <button type="button" className="btn-ghost" onClick={() => setEditingQuotaKey(quota.key)}>
                                {isEnglish ? 'Edit limit' : 'Editar límite'}
                              </button>
                              {quota.isOverridden ? (
                                <button
                                  type="button"
                                  className="btn-ghost"
                                  disabled={isSaving}
                                  onClick={() => { resetQuotaToDefault(quota.key); saveQuota(quota.key); }}
                                >
                                  {isEnglish ? 'Reset to default' : 'Restablecer default'}
                                </button>
                              ) : null}
                            </>
                          )}
                        </div>
                      </article>
                    );
                  })}
                </div>
              </div>
            );
          })}
      </div>
    ) : null}

    {selectedEngramRow ? <div className="modal-overlay" onClick={() => setSelectedEngramRow(null)}>
      <div className="modal-card" onClick={(e) => e.stopPropagation()}>
        <h2>{selectedEngramRow.chapterTitle}</h2>
        {selectedEngramRow.subchapterTitle && selectedEngramRow.subchapterTitle !== selectedEngramRow.chapterTitle ? <p className="modal-body-text">{selectedEngramRow.subchapterTitle}</p> : null}
        <p className="customer-label">{isEnglish ? 'Course' : 'Curso'}</p>
        <code>{selectedEngramRow.documentName}</code>

        <div className="stat-card-grid tenant-section-gap">
          <div className="stat-card"><p className="customer-label">{isEnglish ? 'Sessions' : 'Sesiones'}</p><h3>{selectedEngramRow.totalSessionsVisited}</h3></div>
          <div className="stat-card"><p className="customer-label">{isEnglish ? 'Total time' : 'Tiempo total'}</p><h3>{formatSeconds(selectedEngramRow.totalTimeSeconds * 1000)}</h3></div>
          <div className="stat-card"><p className="customer-label">{isEnglish ? 'Recall mastered/failed' : 'Recall dominado/fallido'}</p><h3>{selectedEngramRow.recall.masteredCount} / {selectedEngramRow.recall.failedCount}</h3></div>
          <div className="stat-card"><p className="customer-label">{isEnglish ? 'Assessment passed/failed' : 'Assessment aprobado/fallido'}</p><h3>{selectedEngramRow.assessment.passedRounds} / {selectedEngramRow.assessment.failedRounds}</h3></div>
        </div>

        <div className="tenant-section-gap">
          <p className="customer-label">{isEnglish ? 'Token usage by channel' : 'Uso de tokens por canal'}</p>
          {selectedEngramRow.usage.length === 0 ? <div className="data-message">{isEnglish ? 'No usage recorded yet.' : 'Aún no hay uso registrado.'}</div>
          : <div className="cost-table-wrap">
              <table className="cost-table">
                <thead>
                  <tr>
                    <th>{isEnglish ? 'Channel' : 'Canal'}</th>
                    <th>{isEnglish ? 'Model' : 'Modelo'}</th>
                    <th>{isEnglish ? 'Tokens in' : 'Tokens entrada'}</th>
                    <th>{isEnglish ? 'Tokens out' : 'Tokens salida'}</th>
                  </tr>
                </thead>
                <tbody>
                  {selectedEngramRow.usage.map((u, i) => (
                    <tr key={`${u.channel}-${u.modelName}-${i}`}>
                      <td>{u.channel}</td>
                      <td>{u.modelName ?? '—'}</td>
                      <td>{u.inputTokens.toLocaleString()}</td>
                      <td>{u.outputTokens.toLocaleString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>}
        </div>

        <div className="modal-actions">
          <button type="button" className="btn-ghost" onClick={() => setSelectedEngramRow(null)}>{isEnglish ? 'Close' : 'Cerrar'}</button>
        </div>
      </div>
    </div> : null}

    {activeTab === 'labCosts' ? (
      <>
        <div className="tenant-section-gap">
          <p className="customer-label">{isEnglish ? 'Realtime voice agent (practice conversations), by lab' : 'Agente de voz en tiempo real (conversaciones de práctica), por lab'}</p>
          {labVoiceCostError ? <div className="data-message error">{labVoiceCostError}</div>
          : labVoiceCost === null ? <div className="data-message">{isEnglish ? 'Loading voice agent cost...' : 'Cargando costo del agente de voz...'}</div>
          : labVoiceCost.length === 0 ? <div className="data-message">{isEnglish ? 'No labs found for this tenant.' : 'No se encontraron labs para este tenant.'}</div>
          : <div className="cost-table-wrap">
              <table className="cost-table">
                <thead>
                  <tr>
                    <th>{isEnglish ? 'Lab' : 'Lab'}</th>
                    <th>{isEnglish ? 'Scope' : 'Alcance'}</th>
                    <th>{isEnglish ? 'Attempts' : 'Intentos'}</th>
                    <th>{isEnglish ? 'Time' : 'Tiempo'}</th>
                    <th>{isEnglish ? 'Est. cost' : 'Costo est.'}</th>
                  </tr>
                </thead>
                <tbody>
                  {labVoiceCost.map((lab) => (
                    <tr key={lab.labId}>
                      <td>{lab.name}</td>
                      <td>{lab.isGlobal ? (isEnglish ? 'Global' : 'Global') : (isEnglish ? 'Own' : 'Propio')}</td>
                      <td>{lab.sessionCount}</td>
                      <td>{formatSeconds(lab.totalDurationSeconds * 1000)}</td>
                      <td>{formatUsd(lab.estimatedCostUsd)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>}
        </div>

        <div className="tenant-section-gap">
          <p className="customer-label">{isEnglish ? 'Attempt evaluation (prompt in/out), by lab' : 'Evaluación de intentos (prompt in/out), por lab'}</p>
          {labEvaluationCostError ? <div className="data-message error">{labEvaluationCostError}</div>
          : labEvaluationCost === null ? <div className="data-message">{isEnglish ? 'Loading evaluation cost...' : 'Cargando costo de evaluación...'}</div>
          : labEvaluationCost.length === 0 ? <div className="data-message">{isEnglish ? 'No labs found for this tenant.' : 'No se encontraron labs para este tenant.'}</div>
          : <div className="cost-table-wrap">
              <table className="cost-table">
                <thead>
                  <tr>
                    <th>{isEnglish ? 'Lab' : 'Lab'}</th>
                    <th>{isEnglish ? 'Scope' : 'Alcance'}</th>
                    <th>{isEnglish ? 'Evaluations' : 'Evaluaciones'}</th>
                    <th>{isEnglish ? 'Tokens in' : 'Tokens entrada'}</th>
                    <th>{isEnglish ? 'Tokens out' : 'Tokens salida'}</th>
                    <th>{isEnglish ? 'Est. cost' : 'Costo est.'}</th>
                  </tr>
                </thead>
                <tbody>
                  {labEvaluationCost.map((lab) => (
                    <tr key={lab.labId}>
                      <td>{lab.name}</td>
                      <td>{lab.isGlobal ? (isEnglish ? 'Global' : 'Global') : (isEnglish ? 'Own' : 'Propio')}</td>
                      <td>{lab.evaluationCount}</td>
                      <td>{formatNumber(lab.inputTokens)}</td>
                      <td>{formatNumber(lab.outputTokens)}</td>
                      <td>{formatUsd(lab.estimatedCostUsd)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>}
        </div>
      </>
    ) : null}
  </>;
}

type PersonProfile = {
  personId: string;
  displayName: string | null;
  email: string | null;
  jobRoleTitle: string | null;
  departmentName: string | null;
};

type PersonEnrolledCourse = {
  structuredLearningDocumentId: string;
  documentName: string;
  sectionCount: number;
  completedSectionCount: number;
  coverIllustrationId: string | null;
  enrolledDate: string;
  language: string;
};

type PersonCourseDashboardRow = {
  structuredLearningDocumentId: string;
  documentName: string;
  sectionCount: number;
  completedSectionCount: number;
  isCompleted: boolean;
  totalTimeSeconds: number;
  totalInputTokens: number;
  totalOutputTokens: number;
  estimatedCostUsd: number;
};

type PersonLearningDashboard = {
  coursesCompleted: number;
  coursesInProgress: number;
  totalEngramsCompleted: number;
  totalEngramsAvailable: number;
  totalInputTokens: number;
  totalOutputTokens: number;
  totalEstimatedCostUsd: number;
  courseEstimatedCostUsd: number;
  totalTimeSeconds: number;
  recall: { attemptsCount: number; masteredCount: number; failedCount: number; averageScore: number | null };
  assessment: { roundsCount: number; passedRounds: number; failedRounds: number; questionsCount: number; incorrectAnswers: number };
  courses: PersonCourseDashboardRow[];
  buddy: { sessionCount: number; totalDurationSeconds: number; estimatedCostUsd: number };
};

type PersonBrainEngramStat = {
  sectionId: string;
  title: string;
  chapterTitle: string;
  voiceSessions: number;
  voiceDurationSeconds: number;
  voiceInputTokens: number;
  voiceOutputTokens: number;
  voiceEstimatedCostUsd: number;
  productionGenerations: number;
  productionInputTokens: number;
  productionOutputTokens: number;
  productionEstimatedCostUsd: number;
  textToSpeechCharacterCount: number;
  textToSpeechEstimatedCostUsd: number;
};

type PersonBrainCourseStat = {
  courseId: string;
  courseName: string;
  engrams: PersonBrainEngramStat[];
};

/** Maps one row from GET /people/{personId}/structured-learning-engram-stats
 * (StructuredLearningEngramStatsService.GetPersonEngramStatsAsync \u2014 a single
 * batched query, not a per-section fetch) into this page's existing
 * PersonBrainEngramStat shape, tagged with which course it belongs to so the
 * caller can group rows by course without a second round-trip. */
function mapPersonEngramStatRow(row: Record<string, unknown>): { documentId: string; documentName: string; engram: PersonBrainEngramStat } {
  const sectionId = String(row.StructuredLearningDocumentSectionId ?? row.structuredLearningDocumentSectionId ?? '');
  const documentId = String(row.StructuredLearningDocumentId ?? row.structuredLearningDocumentId ?? '');
  const documentName = String(row.DocumentName ?? row.documentName ?? '');
  const chapterTitle = String(row.ChapterTitle ?? row.chapterTitle ?? '');
  const subchapterTitle = String(row.SubchapterTitle ?? row.subchapterTitle ?? '');

  const usage = (row.Usage ?? row.usage ?? []) as Record<string, unknown>[];
  const voiceRows = usage.filter((u) => String(u.Channel ?? u.channel ?? '').toLowerCase() === 'voice');
  const voiceInputTokens = voiceRows.reduce((total, u) => total + Number(u.InputTokens ?? u.inputTokens ?? 0), 0);
  const voiceOutputTokens = voiceRows.reduce((total, u) => total + Number(u.OutputTokens ?? u.outputTokens ?? 0), 0);
  const voiceDurationSeconds = voiceRows.reduce((total, u) => total + Number(u.DurationSeconds ?? u.durationSeconds ?? 0), 0);

  const usageCostBreakdown = (row.UsageCostBreakdown ?? row.usageCostBreakdown ?? []) as Record<string, unknown>[];
  const productionRows = usageCostBreakdown.filter((u) =>
    String(u.StepType ?? u.stepType ?? '').toLowerCase() === 'production'
    && String(u.Channel ?? u.channel ?? '').toLowerCase() === 'text');

  return {
    documentId,
    documentName,
    engram: {
      sectionId,
      title: subchapterTitle || chapterTitle || 'Engram',
      chapterTitle,
      voiceSessions: Number(row.VoiceSessionCount ?? row.voiceSessionCount ?? 0),
      voiceDurationSeconds,
      voiceInputTokens,
      voiceOutputTokens,
      voiceEstimatedCostUsd: Math.round((voiceDurationSeconds / 60 * 0.045) * 10000) / 10000,
      productionGenerations: productionRows.reduce((total, u) => total + Number(u.CallCount ?? u.callCount ?? 0), 0),
      productionInputTokens: productionRows.reduce((total, u) => total + Number(u.InputTokens ?? u.inputTokens ?? 0), 0),
      productionOutputTokens: productionRows.reduce((total, u) => total + Number(u.OutputTokens ?? u.outputTokens ?? 0), 0),
      productionEstimatedCostUsd: productionRows.reduce((total, u) => total + Number(u.TotalCostUsd ?? u.totalCostUsd ?? 0), 0),
      textToSpeechCharacterCount: Number(row.TextToSpeechCharacterCount ?? row.textToSpeechCharacterCount ?? 0),
      textToSpeechEstimatedCostUsd: Number(row.TextToSpeechEstimatedCostUsd ?? row.textToSpeechEstimatedCostUsd ?? 0),
    },
  };
}

/** Groups every engram-stat row returned for a person into per-course
 * buckets \u2014 same PersonBrainCourseStat shape the Brain tab/course detail
 * page already render, just assembled from one batched fetch. */
function groupPersonBrainCourseStats(rows: Record<string, unknown>[]): PersonBrainCourseStat[] {
  const byCourse = new Map<string, PersonBrainCourseStat>();
  for (const row of rows) {
    const { documentId, documentName, engram } = mapPersonEngramStatRow(row);
    if (!documentId) continue;
    const existing = byCourse.get(documentId);
    if (existing) {
      existing.engrams.push(engram);
    } else {
      byCourse.set(documentId, { courseId: documentId, courseName: documentName, engrams: [engram] });
    }
  }
  return Array.from(byCourse.values());
}

/** Pure-CSS donut chart via conic-gradient \u2014 no chart library dependency,
 * same philosophy as <see cref="MiniBar"/>. Renders an empty gray ring when
 * every segment is zero so the layout never collapses. */
function DonutChart({
  segments, size = 150, thickness = 24, centerLabel, centerSubLabel,
}: {
  segments: { label: string; value: number; color: string }[];
  size?: number;
  thickness?: number;
  centerLabel?: string;
  centerSubLabel?: string;
}) {
  const total = segments.reduce((sum, s) => sum + s.value, 0);
  let cumulative = 0;
  const stops = total <= 0
    ? ['#e2e8f0 0% 100%']
    : segments.filter((s) => s.value > 0).map((s) => {
        const start = cumulative;
        cumulative += (s.value / total) * 100;
        return `${s.color} ${start}% ${cumulative}%`;
      });

  return (
    <div className="donut-chart-wrap">
      <div
        className="donut-chart"
        style={{ width: size, height: size, background: `conic-gradient(${stops.join(', ')})`, '--donut-thickness': `${thickness}px` } as CSSProperties}
      >
        <div className="donut-chart-hole">
          {centerLabel ? <strong>{centerLabel}</strong> : null}
          {centerSubLabel ? <span>{centerSubLabel}</span> : null}
        </div>
      </div>
      <div className="donut-legend">
        {segments.map((s) => (
          <div className="donut-legend-item" key={s.label}>
            <span className="donut-legend-swatch" style={{ background: s.color }} />
            <span>{s.label}</span>
            <strong>{s.value.toLocaleString()}</strong>
          </div>
        ))}
      </div>
    </div>
  );
}

const DASHBOARD_PALETTE = ['#315fc0', '#1f9d7c', '#e0a52c', '#c0486f', '#5a42a8', '#2f8fd1', '#d1662f', '#6fae3a'];

/** Spectacular cross-course learning dashboard for one person (2026-10-03)
 * \u2014 KPI hero cards, donut charts (courses status, Recall, Assessment),
 * and per-course bar charts (tokens, time, cost) via <see cref="MiniBar"/>
 * and <see cref="DonutChart"/>. Backed by GetPersonLearningDashboard. */
function PersonDashboardTab({
  dashboard, dashboardError, brainCourses, isEnglish,
}: {
  dashboard: PersonLearningDashboard | null;
  dashboardError: string | null;
  brainCourses: PersonBrainCourseStat[] | null;
  isEnglish: boolean;
}) {
  if (dashboardError) return <div className="data-message error">{dashboardError}</div>;
  if (dashboard === null) return <div className="data-message">{isEnglish ? 'Loading dashboard...' : 'Cargando dashboard...'}</div>;

  const totalTokens = dashboard.totalInputTokens + dashboard.totalOutputTokens;
  const totalCourses = dashboard.coursesCompleted + dashboard.coursesInProgress;
  const topByTokens = [...dashboard.courses].sort((a, b) => (b.totalInputTokens + b.totalOutputTokens) - (a.totalInputTokens + a.totalOutputTokens));
  const maxCourseTokens = Math.max(1, ...topByTokens.map((c) => c.totalInputTokens + c.totalOutputTokens));
  const maxCourseTime = Math.max(1, ...dashboard.courses.map((c) => c.totalTimeSeconds));
  const maxCourseCost = Math.max(1, ...dashboard.courses.map((c) => c.estimatedCostUsd));
  const brainEngrams = brainCourses?.flatMap((course) => course.engrams) ?? [];
  const brainEngramsUsed = brainEngrams.filter((engram) => engram.voiceSessions > 0).length;
  const brainInputTokens = brainEngrams.reduce((total, engram) => total + engram.voiceInputTokens, 0);
  const brainOutputTokens = brainEngrams.reduce((total, engram) => total + engram.voiceOutputTokens, 0);
  const brainCost = brainEngrams.reduce((total, engram) => total + engram.voiceEstimatedCostUsd, 0);
  const productionInputTokens = brainEngrams.reduce((total, engram) => total + engram.productionInputTokens, 0);
  const productionOutputTokens = brainEngrams.reduce((total, engram) => total + engram.productionOutputTokens, 0);
  const productionEstimatedCostUsd = brainEngrams.reduce((total, engram) => total + engram.productionEstimatedCostUsd, 0);
  const textToSpeechCharacterCount = brainEngrams.reduce((total, engram) => total + engram.textToSpeechCharacterCount, 0);
  const textToSpeechEstimatedCostUsd = brainEngrams.reduce((total, engram) => total + engram.textToSpeechEstimatedCostUsd, 0);
  const brainAverageCost = brainEngramsUsed === 0 ? 0 : brainCost / brainEngramsUsed;

  const kpis = [
    { label: isEnglish ? 'Courses completed' : 'Cursos terminados', value: dashboard.coursesCompleted, icon: GraduationCap, gradient: 'linear-gradient(135deg, #168b72, #1f9d7c)' },
    { label: isEnglish ? 'Courses in progress' : 'Cursos en curso', value: dashboard.coursesInProgress, icon: BookOpen, gradient: 'linear-gradient(135deg, #245ac0, #5a42a8)' },
    { label: isEnglish ? 'Engrams completed' : 'Engramas terminados', value: `${dashboard.totalEngramsCompleted}/${dashboard.totalEngramsAvailable}`, icon: BookOpenCheck, gradient: 'linear-gradient(135deg, #b5651d, #e0a52c)' },
    { label: isEnglish ? 'Total time (courses + Buddy)' : 'Tiempo total (cursos + Buddy)', value: formatSeconds(dashboard.totalTimeSeconds * 1000), icon: Clock, gradient: 'linear-gradient(135deg, #1e59b8, #315fc0)' },
    { label: isEnglish ? 'Total tokens (in/out)' : 'Tokens totales (in/out)', value: `${formatNumber(dashboard.totalInputTokens)} / ${formatNumber(dashboard.totalOutputTokens)}`, icon: Coins, gradient: 'linear-gradient(135deg, #8a3fa8, #c0486f)' },
    { label: isEnglish ? 'Buddy usage (voice)' : 'Uso de Buddy (voz)', value: `${formatSeconds(dashboard.buddy.totalDurationSeconds * 1000)} · ${dashboard.buddy.sessionCount} ${isEnglish ? 'sessions' : 'sesiones'}`, icon: Headphones, gradient: 'linear-gradient(135deg, #6b3fa0, #9d5fc0)' },
  ];

  return <>
    <div className="dashboard-spend-hero">
      <div className="dashboard-spend-hero-icon"><DollarSign size={40} /></div>
      <div className="dashboard-spend-hero-content">
        <p>{isEnglish ? 'Total spent by this user' : 'Total gastado por este usuario'}</p>
        <h2>{formatUsd(dashboard.totalEstimatedCostUsd)}</h2>
        <div className="dashboard-spend-hero-breakdown">
          <span><BookOpen size={14} /> {isEnglish ? 'Courses' : 'Cursos'}: {formatUsd(dashboard.courseEstimatedCostUsd)}</span>
          <span><Headphones size={14} /> Buddy: {formatUsd(dashboard.buddy.estimatedCostUsd)}</span>
        </div>

      </div>
    </div>

    <section className="dashboard-brain-card">
      <div className="dashboard-brain-heading">
        <div className="dashboard-brain-icon"><Headphones size={21} /></div>
        <div>
          <p className="eyebrow">{isEnglish ? 'Brain / voice usage' : 'Cerebro / uso de voz'}</p>
          <h3>{isEnglish ? 'Brain totals' : 'Totales de Brain'}</h3>
        </div>
      </div>
      {brainCourses === null ? (
        <span className="dashboard-brain-loading">{isEnglish ? 'Loading...' : 'Cargando...'}</span>
      ) : (
        <div className="dashboard-brain-metrics">
          <div><span>{isEnglish ? 'Tokens in' : 'Tokens entrada'}</span><strong>{formatNumber(brainInputTokens)}</strong></div>
          <div><span>{isEnglish ? 'Tokens out' : 'Tokens salida'}</span><strong>{formatNumber(brainOutputTokens)}</strong></div>
          <div><span>{isEnglish ? 'Voice cost' : 'Costo de voz'}</span><strong>{formatUsd(brainCost)}</strong></div>
          <div><span>{isEnglish ? 'Engrams used' : 'Engramas usados'}</span><strong>{formatNumber(brainEngramsUsed)}</strong></div>
          <div><span>{isEnglish ? 'Apply tokens in / out' : 'Tokens Apply entrada / salida'}</span><strong>{formatNumber(productionInputTokens)} / {formatNumber(productionOutputTokens)}</strong></div>
          <div><span>{isEnglish ? 'Apply cost' : 'Costo Apply'}</span><strong>{formatUsd(productionEstimatedCostUsd)}</strong></div>
          <div><span>{isEnglish ? 'Azure Speech characters' : 'Caracteres Azure Speech'}</span><strong>{formatNumber(textToSpeechCharacterCount)}</strong></div>
          <div><span>{isEnglish ? 'Azure Speech cost' : 'Costo Azure Speech'}</span><strong>{formatUsd(textToSpeechEstimatedCostUsd)}</strong></div>
          <div><span>{isEnglish ? 'Avg. cost / engram' : 'Promedio / engrama'}</span><strong>{formatUsd(brainAverageCost)}</strong></div>
        </div>
      )}
    </section>

    <div className="dashboard-hero-grid">
      {kpis.map((kpi) => (
        <div className="dashboard-kpi-card" style={{ background: kpi.gradient }} key={kpi.label}>
          <kpi.icon size={22} />
          <p>{kpi.label}</p>
          <h3>{kpi.value}</h3>
        </div>
      ))}
    </div>

    <div className="dashboard-charts-grid">
      <div className="dashboard-chart-card">
        <h4><PieChart size={16} /> {isEnglish ? 'Course status' : 'Estado de cursos'}</h4>
        <DonutChart
          size={150}
          thickness={26}
          centerLabel={String(totalCourses)}
          centerSubLabel={isEnglish ? 'courses' : 'cursos'}
          segments={[
            { label: isEnglish ? 'Completed' : 'Terminados', value: dashboard.coursesCompleted, color: DASHBOARD_PALETTE[1] },
            { label: isEnglish ? 'In progress' : 'En curso', value: dashboard.coursesInProgress, color: DASHBOARD_PALETTE[0] },
          ]}
        />
      </div>

      <div className="dashboard-chart-card">
        <h4><Target size={16} /> Recall</h4>
        <DonutChart
          size={150}
          thickness={26}
          centerLabel={String(dashboard.recall.attemptsCount)}
          centerSubLabel={isEnglish ? 'attempts' : 'intentos'}
          segments={[
            { label: isEnglish ? 'Mastered' : 'Logrado', value: dashboard.recall.masteredCount, color: DASHBOARD_PALETTE[1] },
            { label: isEnglish ? 'Failed' : 'Fallido', value: dashboard.recall.failedCount, color: DASHBOARD_PALETTE[3] },
          ]}
        />
      </div>

      <div className="dashboard-chart-card">
        <h4><TrendingUp size={16} /> Assessment</h4>
        <DonutChart
          size={150}
          thickness={26}
          centerLabel={String(dashboard.assessment.roundsCount)}
          centerSubLabel={isEnglish ? 'rounds' : 'rondas'}
          segments={[
            { label: isEnglish ? 'Passed' : 'Aprobado', value: dashboard.assessment.passedRounds, color: DASHBOARD_PALETTE[1] },
            { label: isEnglish ? 'Failed' : 'Reprobado', value: dashboard.assessment.failedRounds, color: DASHBOARD_PALETTE[3] },
          ]}
        />
      </div>
    </div>

    {dashboard.courses.length > 0 ? <div className="dashboard-chart-stack">
      <div className="dashboard-chart-card">
        <h4><BarChart3 size={16} /> {isEnglish ? 'Tokens per course (in + out)' : 'Tokens por curso (entrada + salida)'}</h4>
        <div className="mini-chart-card">
          {topByTokens.map((course, i) => <MiniBar
            key={course.structuredLearningDocumentId}
            label={course.documentName || (isEnglish ? 'Untitled course' : 'Curso sin título')}
            value={course.totalInputTokens + course.totalOutputTokens}
            max={maxCourseTokens}
            color={`linear-gradient(90deg, ${DASHBOARD_PALETTE[i % DASHBOARD_PALETTE.length]}, ${DASHBOARD_PALETTE[(i + 1) % DASHBOARD_PALETTE.length]})`}
            valueLabel={formatNumber(course.totalInputTokens + course.totalOutputTokens)}
          />)}
        </div>
      </div>

      <div className="dashboard-chart-card">
        <h4><Clock size={16} /> {isEnglish ? 'Time per course' : 'Tiempo por curso'}</h4>
        <div className="mini-chart-card">
          {dashboard.courses.map((course, i) => <MiniBar
            key={course.structuredLearningDocumentId}
            label={course.documentName || (isEnglish ? 'Untitled course' : 'Curso sin título')}
            value={course.totalTimeSeconds}
            max={maxCourseTime}
            color={`linear-gradient(90deg, ${DASHBOARD_PALETTE[(i + 2) % DASHBOARD_PALETTE.length]}, ${DASHBOARD_PALETTE[(i + 3) % DASHBOARD_PALETTE.length]})`}
            valueLabel={formatSeconds(course.totalTimeSeconds * 1000)}
          />)}
        </div>
      </div>

      <div className="dashboard-chart-card">
        <h4><DollarSign size={16} /> {isEnglish ? 'Estimated cost per course' : 'Costo estimado por curso'}</h4>
        <div className="mini-chart-card">
          {dashboard.courses.map((course, i) => <MiniBar
            key={course.structuredLearningDocumentId}
            label={course.documentName || (isEnglish ? 'Untitled course' : 'Curso sin título')}
            value={course.estimatedCostUsd}
            max={maxCourseCost}
            color={`linear-gradient(90deg, ${DASHBOARD_PALETTE[(i + 4) % DASHBOARD_PALETTE.length]}, ${DASHBOARD_PALETTE[(i + 5) % DASHBOARD_PALETTE.length]})`}
            valueLabel={formatUsd(course.estimatedCostUsd)}
          />)}
        </div>
      </div>

      <div className="dashboard-chart-card">
        <h4><Layers3 size={16} /> {isEnglish ? 'Engrams completed per course' : 'Engramas terminados por curso'}</h4>
        <div className="mini-chart-card">
          {dashboard.courses.map((course, i) => <MiniBar
            key={course.structuredLearningDocumentId}
            label={course.documentName || (isEnglish ? 'Untitled course' : 'Curso sin título')}
            value={course.completedSectionCount}
            max={Math.max(1, course.sectionCount)}
            color={`linear-gradient(90deg, ${DASHBOARD_PALETTE[(i + 1) % DASHBOARD_PALETTE.length]}, ${DASHBOARD_PALETTE[(i + 6) % DASHBOARD_PALETTE.length]})`}
            valueLabel={`${course.completedSectionCount}/${course.sectionCount}`}
          />)}
        </div>
      </div>
    </div> : null}

    {totalTokens === 0 && dashboard.courses.length === 0 ? (
      <div className="data-message">{isEnglish ? 'No learning activity recorded yet for this user.' : 'Aún no hay actividad de aprendizaje registrada para este usuario.'}</div>
    ) : null}
  </>;
}

type PersonEngramReviewNode = {
  capabilityGraphNodeId: string;
  nodeName: string;
  sessionCount: number;
  totalDurationSeconds: number;
  estimatedCostUsd: number;
};

type PersonEngramReviewUsage = {
  totalSessions: number;
  totalDurationSeconds: number;
  estimatedCostUsd: number;
  nodes: PersonEngramReviewNode[];
};

/** Real "Reactivar Engrama" (Brain spaced-repetition review) spend for one
 * person (2026-10-04) — separate domain from <see cref="PersonBrainTab"/>
 * (which is in-course Learn/Recall/Assessment/Apply usage). Backed by
 * GetPersonEngramReviewUsage, itself backed by the always-fires
 * EngramReviewSessionUsage ledger (real duration, no proxy). */
function PersonRepasosTab({ personId, isEnglish }: { personId: string; isEnglish: boolean }) {
  const [usage, setUsage] = useState<PersonEngramReviewUsage | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!personId) return;
    setUsage(null);
    setError(null);
    fetch(`/api/people/${personId}/engram-review-usage`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load Brain review usage.');
        return response.json() as Promise<Record<string, unknown>>;
      })
      .then((row) => {
        const rawNodes = (row.Nodes ?? row.nodes ?? []) as Record<string, unknown>[];
        setUsage({
          totalSessions: Number(row.TotalSessions ?? row.totalSessions ?? 0),
          totalDurationSeconds: Number(row.TotalDurationSeconds ?? row.totalDurationSeconds ?? 0),
          estimatedCostUsd: Number(row.EstimatedCostUsd ?? row.estimatedCostUsd ?? 0),
          nodes: rawNodes.map((n) => ({
            capabilityGraphNodeId: String(n.CapabilityGraphNodeId ?? n.capabilityGraphNodeId ?? ''),
            nodeName: String(n.NodeName ?? n.nodeName ?? ''),
            sessionCount: Number(n.SessionCount ?? n.sessionCount ?? 0),
            totalDurationSeconds: Number(n.TotalDurationSeconds ?? n.totalDurationSeconds ?? 0),
            estimatedCostUsd: Number(n.EstimatedCostUsd ?? n.estimatedCostUsd ?? 0),
          })),
        });
      })
      .catch((err: Error) => setError(err.message));
  }, [personId]);

  if (error) return <div className="data-message error">{error}</div>;
  if (usage === null) return <div className="data-message">{isEnglish ? 'Loading Brain review usage...' : 'Cargando uso de repasos de Cerebro...'}</div>;
  if (usage.totalSessions === 0) {
    return <div className="data-message">{isEnglish ? 'No Brain reviews ("Reactivar Engrama") recorded yet for this user.' : 'Aún no hay repasos de Cerebro ("Reactivar Engrama") registrados para este usuario.'}</div>;
  }

  return (
    <div className="brain-admin-page">
      <div className="brain-admin-hero">
        <div>
          <p className="eyebrow">{isEnglish ? 'Brain reviews' : 'Repasos de Cerebro'}</p>
          <h3>{isEnglish ? '"Reactivate engram" spaced-repetition usage' : 'Uso de repasos ("Reactivar Engrama")'}</h3>
          <p>{isEnglish ? 'Real voice time spent reviewing already-learned engrams, billed at the Realtime per-minute rate.' : 'Tiempo real de voz invertido repasando engramas ya aprendidos, facturado a la tarifa por minuto de Realtime.'}</p>
        </div>
        <div className="brain-admin-hero-mark"><Headphones size={28} /></div>
      </div>

      <div className="brain-admin-kpis">
        <article><span>{isEnglish ? 'Review sessions' : 'Sesiones de repaso'}</span><strong>{formatNumber(usage.totalSessions)}</strong></article>
        <article><span>{isEnglish ? 'Review time' : 'Tiempo de repaso'}</span><strong>{formatSeconds(usage.totalDurationSeconds * 1000)}</strong></article>
        <article><span>{isEnglish ? 'Estimated cost' : 'Costo estimado'}</span><strong>{formatUsd(usage.estimatedCostUsd)}</strong></article>
      </div>

      <p className="customer-label">{isEnglish ? 'Reviewed engrams' : 'Engramas repasados'}</p>
      <div className="catalog-course-grid">
        {usage.nodes.map((node) => (
          <article className="brain-admin-course-link" key={node.capabilityGraphNodeId}>
            <div className="brain-admin-course-header">
              <div>
                <p className="customer-label">{isEnglish ? 'Engram' : 'Engrama'}</p>
                <h3>{node.nodeName}</h3>
              </div>
              <div className="brain-admin-course-total">
                <span>{node.sessionCount} {isEnglish ? 'sessions' : 'sesiones'}</span>
                <strong>{formatUsd(node.estimatedCostUsd)}</strong>
              </div>
            </div>
            <div className="brain-admin-course-summary">
              <span><Clock size={15} /> {formatSeconds(node.totalDurationSeconds * 1000)}</span>
            </div>
          </article>
        ))}
      </div>
    </div>
  );
}

function PersonBrainTab({
  tenantId,
  personId,
  brainCourses,
  brainError,
  isEnglish,
}: {
  tenantId: string;
  personId: string;
  brainCourses: PersonBrainCourseStat[] | null;
  brainError: string | null;
  isEnglish: boolean;
}) {
  if (brainError) return <div className="data-message error">{brainError}</div>;
  if (brainCourses === null) {
    return <div className="data-message">{isEnglish ? 'Loading Brain usage...' : 'Cargando uso de Cerebro...'}</div>;
  }
  if (brainCourses.length === 0) {
    return <div className="data-message">{isEnglish ? 'No enrolled courses have Brain usage yet.' : 'Aún no hay uso de Cerebro en cursos inscritos.'}</div>;
  }

  const allEngrams = brainCourses.flatMap((course) => course.engrams);
  const voiceSessions = allEngrams.reduce((total, engram) => total + engram.voiceSessions, 0);
  const voiceDurationSeconds = allEngrams.reduce((total, engram) => total + engram.voiceDurationSeconds, 0);
  const voiceInputTokens = allEngrams.reduce((total, engram) => total + engram.voiceInputTokens, 0);
  const voiceOutputTokens = allEngrams.reduce((total, engram) => total + engram.voiceOutputTokens, 0);
  const voiceEstimatedCostUsd = allEngrams.reduce((total, engram) => total + engram.voiceEstimatedCostUsd, 0);
  const productionGenerations = allEngrams.reduce((total, engram) => total + engram.productionGenerations, 0);
  const productionEstimatedCostUsd = allEngrams.reduce((total, engram) => total + engram.productionEstimatedCostUsd, 0);
  const textToSpeechCharacterCount = allEngrams.reduce((total, engram) => total + engram.textToSpeechCharacterCount, 0);
  const textToSpeechEstimatedCostUsd = allEngrams.reduce((total, engram) => total + engram.textToSpeechEstimatedCostUsd, 0);

  return (
    <div className="brain-admin-page">
      <div className="brain-admin-hero">
        <div>
          <p className="eyebrow">{isEnglish ? 'Brain / voice usage' : 'Cerebro / uso de voz'}</p>
          <h3>{isEnglish ? 'Engram usage by course' : 'Uso de engramas por curso'}</h3>
          <p>{isEnglish ? 'Realtime voice and workplace application usage are accumulated separately under each course and engram.' : 'El uso de voz Realtime y las aplicaciones de puesto se acumulan por separado en cada curso y engrama.'}</p>
        </div>
        <div className="brain-admin-hero-mark"><Headphones size={28} /></div>
      </div>

      <div className="brain-admin-kpis">
        <article><span>{isEnglish ? 'Voice sessions' : 'Sesiones de voz'}</span><strong>{formatNumber(voiceSessions)}</strong></article>
        <article><span>{isEnglish ? 'Voice time' : 'Tiempo de voz'}</span><strong>{formatSeconds(voiceDurationSeconds * 1000)}</strong></article>
        <article><span>{isEnglish ? 'Tokens in / out' : 'Tokens entrada / salida'}</span><strong>{formatNumber(voiceInputTokens)} / {formatNumber(voiceOutputTokens)}</strong></article>
        <article><span>{isEnglish ? 'Estimated voice cost' : 'Costo estimado de voz'}</span><strong>{formatUsd(voiceEstimatedCostUsd)}</strong></article>
        <article><span>{isEnglish ? 'Apply lesson generations' : 'Generaciones Apply lesson'}</span><strong>{formatNumber(productionGenerations)}</strong></article>
        <article><span>{isEnglish ? 'Apply lesson cost' : 'Costo Apply lesson'}</span><strong>{formatUsd(productionEstimatedCostUsd)}</strong></article>
        <article><span>{isEnglish ? 'Azure Speech characters' : 'Caracteres Azure Speech'}</span><strong>{formatNumber(textToSpeechCharacterCount)}</strong></article>
        <article><span>{isEnglish ? 'Azure Speech cost' : 'Costo Azure Speech'}</span><strong>{formatUsd(textToSpeechEstimatedCostUsd)}</strong></article>
      </div>

      <p className="customer-label">{isEnglish ? 'Courses — open one to see its engrams' : 'Cursos — abre uno para ver sus engramas'}</p>
      <div className="catalog-course-grid">
        {brainCourses.map((course) => {
          const courseVoiceSessions = course.engrams.reduce((total, engram) => total + engram.voiceSessions, 0);
          const courseVoiceDuration = course.engrams.reduce((total, engram) => total + engram.voiceDurationSeconds, 0);
          const courseVoiceCost = course.engrams.reduce((total, engram) => total + engram.voiceEstimatedCostUsd, 0);
          const courseProductionGenerations = course.engrams.reduce((total, engram) => total + engram.productionGenerations, 0);
          const courseProductionCost = course.engrams.reduce((total, engram) => total + engram.productionEstimatedCostUsd, 0);
          const courseSpeechCost = course.engrams.reduce((total, engram) => total + engram.textToSpeechEstimatedCostUsd, 0);
          return (
            <Link
              to={`/tenants/${tenantId}/people/${personId}/brain/${course.courseId}`}
              className="brain-admin-course-link"
              key={course.courseId}
            >
              <div className="brain-admin-course-header">
                <div>
                  <p className="customer-label">{isEnglish ? 'Course' : 'Curso'}</p>
                  <h3>{course.courseName}</h3>
                </div>
                <div className="brain-admin-course-total">
                  <span>{course.engrams.length} {isEnglish ? 'engrams' : 'engramas'}</span>
                  <strong>{formatUsd(courseVoiceCost + courseProductionCost + courseSpeechCost)}</strong>
                </div>
              </div>
              <div className="brain-admin-course-summary">
                <span><Headphones size={15} /> {courseVoiceSessions} {isEnglish ? 'voice sessions' : 'sesiones de voz'}</span>
                <span><Clock size={15} /> {formatSeconds(courseVoiceDuration * 1000)}</span>
                <span><Sparkles size={15} /> {courseProductionGenerations} {isEnglish ? 'Apply generations' : 'generaciones Apply'}</span>
              </div>
              <div className="catalog-course-action">{isEnglish ? 'View engrams →' : 'Ver engramas →'}</div>
            </Link>
          );
        })}
      </div>
    </div>
  );
}

/** Per-course drill-down of the Brain tab (2026-10-03) \u2014 opened by clicking
 * a course card in <see cref="PersonBrainTab"/>. Own route/page (tree
 * navigation instead of one giant flat list) so only the clicked course's
 * engrams render at a time; still a single batched fetch (not per-section). */
function PersonBrainCourseDetailPage({ isEnglish }: { isEnglish: boolean }) {
  const { tenantId, personId, courseId } = useParams<{ tenantId: string; personId: string; courseId: string }>();
  const navigate = useNavigate();
  const [courseStat, setCourseStat] = useState<PersonBrainCourseStat | null | undefined>(undefined);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!personId || !courseId) return;
    setCourseStat(undefined);
    setError(null);
    fetch(`/api/people/${personId}/structured-learning-engram-stats`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load Brain usage.');
        return response.json() as Promise<Record<string, unknown>[]>;
      })
      .then((rows) => {
        const courses = groupPersonBrainCourseStats(rows);
        setCourseStat(courses.find((c) => c.courseId === courseId) ?? null);
      })
      .catch((err: Error) => setError(err.message));
  }, [personId, courseId]);

  if (error) return <div className="data-message error">{error}</div>;
  if (courseStat === undefined) {
    return <div className="data-message">{isEnglish ? 'Loading engrams...' : 'Cargando engramas...'}</div>;
  }
  if (courseStat === null) {
    return <div className="data-message">{isEnglish ? 'No Brain usage recorded for this course yet.' : 'Aún no hay uso de Cerebro registrado para este curso.'}</div>;
  }

  const courseVoiceSessions = courseStat.engrams.reduce((total, engram) => total + engram.voiceSessions, 0);
  const courseVoiceDuration = courseStat.engrams.reduce((total, engram) => total + engram.voiceDurationSeconds, 0);
  const courseVoiceInputTokens = courseStat.engrams.reduce((total, engram) => total + engram.voiceInputTokens, 0);
  const courseVoiceOutputTokens = courseStat.engrams.reduce((total, engram) => total + engram.voiceOutputTokens, 0);
  const courseVoiceCost = courseStat.engrams.reduce((total, engram) => total + engram.voiceEstimatedCostUsd, 0);
  const courseProductionGenerations = courseStat.engrams.reduce((total, engram) => total + engram.productionGenerations, 0);
  const courseProductionCost = courseStat.engrams.reduce((total, engram) => total + engram.productionEstimatedCostUsd, 0);
  const courseSpeechCharacters = courseStat.engrams.reduce((total, engram) => total + engram.textToSpeechCharacterCount, 0);
  const courseSpeechCost = courseStat.engrams.reduce((total, engram) => total + engram.textToSpeechEstimatedCostUsd, 0);

  return (
    <div className="brain-admin-page">
      <div className="main-title-row">
        <div>
          <p className="eyebrow">{isEnglish ? 'Brain / voice usage · course detail' : 'Cerebro / uso de voz · detalle de curso'}</p>
          <h2>{courseStat.courseName}</h2>
        </div>
        <button type="button" className="btn-ghost" onClick={() => navigate(`/tenants/${tenantId}/people/${personId}`)}>
          {isEnglish ? '← Back to Brain' : '← Volver a Cerebro'}
        </button>
      </div>

      <div className="brain-admin-kpis tenant-section-gap">
        <article><span>{isEnglish ? 'Voice sessions' : 'Sesiones de voz'}</span><strong>{formatNumber(courseVoiceSessions)}</strong></article>
        <article><span>{isEnglish ? 'Voice time' : 'Tiempo de voz'}</span><strong>{formatSeconds(courseVoiceDuration * 1000)}</strong></article>
        <article><span>{isEnglish ? 'Voice tokens in / out' : 'Tokens voz entrada / salida'}</span><strong>{formatNumber(courseVoiceInputTokens)} / {formatNumber(courseVoiceOutputTokens)}</strong></article>
        <article><span>{isEnglish ? 'Estimated voice cost' : 'Costo estimado de voz'}</span><strong>{formatUsd(courseVoiceCost)}</strong></article>
        <article><span>{isEnglish ? 'Apply lesson generations' : 'Generaciones Apply lesson'}</span><strong>{formatNumber(courseProductionGenerations)}</strong></article>
        <article><span>{isEnglish ? 'Apply lesson cost' : 'Costo Apply lesson'}</span><strong>{formatUsd(courseProductionCost)}</strong></article>
        <article><span>{isEnglish ? 'Azure Speech characters' : 'Caracteres Azure Speech'}</span><strong>{formatNumber(courseSpeechCharacters)}</strong></article>
        <article><span>{isEnglish ? 'Azure Speech cost' : 'Costo Azure Speech'}</span><strong>{formatUsd(courseSpeechCost)}</strong></article>
      </div>

      <section className="brain-admin-course">
        <div className="brain-admin-course-header">
          <div>
            <p className="customer-label">{isEnglish ? 'Engrams' : 'Engramas'}</p>
            <h3>{courseStat.engrams.length} {isEnglish ? 'engrams with Brain usage' : 'engramas con uso de Cerebro'}</h3>
          </div>
          <div className="brain-admin-course-total">
            <span>{isEnglish ? 'Course total' : 'Total del curso'}</span>
            <strong>{formatUsd(courseVoiceCost + courseProductionCost + courseSpeechCost)}</strong>
          </div>
        </div>
        <div className="brain-admin-engram-list">
          {courseStat.engrams.map((engram) => (
            <article className="brain-admin-engram" key={engram.sectionId}>
              <div className="brain-admin-engram-title">
                <div className="brain-admin-engram-index">{engram.chapterTitle || '•'}</div>
                <div>
                  <h4>{engram.title}</h4>
                  <span>{engram.voiceSessions} {isEnglish ? 'voice sessions' : 'sesiones de voz'}</span>
                </div>
              </div>
              <div className="brain-admin-engram-stats">
                <div><span>{isEnglish ? 'Duration' : 'Duración'}</span><strong>{formatSeconds(engram.voiceDurationSeconds * 1000)}</strong></div>
                <div><span>{isEnglish ? 'Voice tokens in / out' : 'Tokens voz entrada / salida'}</span><strong>{formatNumber(engram.voiceInputTokens)} / {formatNumber(engram.voiceOutputTokens)}</strong></div>
                <div><span>{isEnglish ? 'Voice cost' : 'Costo de voz'}</span><strong>{formatUsd(engram.voiceEstimatedCostUsd)}</strong></div>
                <div><span>{isEnglish ? 'Apply generations' : 'Generaciones Apply'}</span><strong>{formatNumber(engram.productionGenerations)}</strong></div>
                <div><span>{isEnglish ? 'Apply tokens in / out' : 'Tokens Apply entrada / salida'}</span><strong>{formatNumber(engram.productionInputTokens)} / {formatNumber(engram.productionOutputTokens)}</strong></div>
                <div><span>{isEnglish ? 'Apply cost' : 'Costo Apply'}</span><strong>{formatUsd(engram.productionEstimatedCostUsd)}</strong></div>
                <div><span>{isEnglish ? 'Azure Speech characters' : 'Caracteres Azure Speech'}</span><strong>{formatNumber(engram.textToSpeechCharacterCount)}</strong></div>
                <div><span>{isEnglish ? 'Azure Speech cost' : 'Costo Azure Speech'}</span><strong>{formatUsd(engram.textToSpeechEstimatedCostUsd)}</strong></div>
                <div><span>{isEnglish ? 'Engram total' : 'Total del engrama'}</span><strong>{formatUsd(engram.voiceEstimatedCostUsd + engram.productionEstimatedCostUsd + engram.textToSpeechEstimatedCostUsd)}</strong></div>
              </div>
            </article>
          ))}
        </div>
      </section>
    </div>
  );
}

function PersonDetailPage({ isEnglish }: { isEnglish: boolean }) {
  const { tenantId, personId } = useParams<{ tenantId: string; personId: string }>();
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<'dashboard' | 'brain' | 'inProgress' | 'completed'>('dashboard');
  const [person, setPerson] = useState<PersonProfile | null>(null);
  const [personError, setPersonError] = useState<string | null>(null);
  const [courses, setCourses] = useState<PersonEnrolledCourse[] | null>(null);
  const [coursesError, setCoursesError] = useState<string | null>(null);
  const [dashboard, setDashboard] = useState<PersonLearningDashboard | null>(null);
  const [dashboardError, setDashboardError] = useState<string | null>(null);
  const [brainCourses, setBrainCourses] = useState<PersonBrainCourseStat[] | null>(null);
  const [brainError, setBrainError] = useState<string | null>(null);

  useEffect(() => {
    if (!tenantId || !personId) return;

    fetch(`/api/tenants/${tenantId}/people`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load person.');
        return response.json() as Promise<Record<string, unknown>[]>;
      })
      .then((rows) => {
        const row = rows.find((r) => String(r.PersonId ?? r.personId ?? '') === personId);
        if (!row) throw new Error(isEnglish ? 'Person not found.' : 'Persona no encontrada.');
        setPerson({
          personId: String(row.PersonId ?? row.personId ?? personId),
          displayName: (row.DisplayName ?? row.displayName ?? null) as string | null,
          email: (row.Email ?? row.email ?? null) as string | null,
          jobRoleTitle: (row.JobRoleTitle ?? row.jobRoleTitle ?? null) as string | null,
          departmentName: (row.DepartmentName ?? row.departmentName ?? null) as string | null,
        });
      })
      .catch((err: Error) => setPersonError(err.message));

    fetch(`/api/people/${personId}/enrolled-structured-learning`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load enrolled courses.');
        return response.json() as Promise<Record<string, unknown>[]>;
      })
      .then((rows) => setCourses(rows.map((row) => ({
        structuredLearningDocumentId: String(row.StructuredLearningDocumentId ?? row.structuredLearningDocumentId ?? ''),
        documentName: String(row.DocumentName ?? row.documentName ?? ''),
        sectionCount: Number(row.SectionCount ?? row.sectionCount ?? 0),
        completedSectionCount: Number(row.CompletedSectionCount ?? row.completedSectionCount ?? 0),
        coverIllustrationId: (row.CoverIllustrationId ?? row.coverIllustrationId ?? null) as string | null,
        enrolledDate: String(row.EnrolledDate ?? row.enrolledDate ?? ''),
        language: String(row.Language ?? row.language ?? 'es'),
      }))))
      .catch((err: Error) => setCoursesError(err.message));

    fetch(`/api/people/${personId}/learning-dashboard`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load learning dashboard.');
        return response.json() as Promise<Record<string, unknown>>;
      })
      .then((row) => {
        const recall = (row.Recall ?? row.recall ?? {}) as Record<string, unknown>;
        const assessment = (row.Assessment ?? row.assessment ?? {}) as Record<string, unknown>;
        const buddy = (row.Buddy ?? row.buddy ?? {}) as Record<string, unknown>;
        const rawCourses = (row.Courses ?? row.courses ?? []) as Record<string, unknown>[];
        setDashboard({
          coursesCompleted: Number(row.CoursesCompleted ?? row.coursesCompleted ?? 0),
          coursesInProgress: Number(row.CoursesInProgress ?? row.coursesInProgress ?? 0),
          totalEngramsCompleted: Number(row.TotalEngramsCompleted ?? row.totalEngramsCompleted ?? 0),
          totalEngramsAvailable: Number(row.TotalEngramsAvailable ?? row.totalEngramsAvailable ?? 0),
          totalInputTokens: Number(row.TotalInputTokens ?? row.totalInputTokens ?? 0),
          totalOutputTokens: Number(row.TotalOutputTokens ?? row.totalOutputTokens ?? 0),
          totalEstimatedCostUsd: Number(row.TotalEstimatedCostUsd ?? row.totalEstimatedCostUsd ?? 0),
          courseEstimatedCostUsd: Number(row.CourseEstimatedCostUsd ?? row.courseEstimatedCostUsd ?? 0),
          totalTimeSeconds: Number(row.TotalTimeSeconds ?? row.totalTimeSeconds ?? 0),
          recall: {
            attemptsCount: Number(recall.AttemptsCount ?? recall.attemptsCount ?? 0),
            masteredCount: Number(recall.MasteredCount ?? recall.masteredCount ?? 0),
            failedCount: Number(recall.FailedCount ?? recall.failedCount ?? 0),
            averageScore: (recall.AverageScore ?? recall.averageScore ?? null) as number | null,
          },
          assessment: {
            roundsCount: Number(assessment.RoundsCount ?? assessment.roundsCount ?? 0),
            passedRounds: Number(assessment.PassedRounds ?? assessment.passedRounds ?? 0),
            failedRounds: Number(assessment.FailedRounds ?? assessment.failedRounds ?? 0),
            questionsCount: Number(assessment.QuestionsCount ?? assessment.questionsCount ?? 0),
            incorrectAnswers: Number(assessment.IncorrectAnswers ?? assessment.incorrectAnswers ?? 0),
          },
          buddy: {
            sessionCount: Number(buddy.SessionCount ?? buddy.sessionCount ?? 0),
            totalDurationSeconds: Number(buddy.TotalDurationSeconds ?? buddy.totalDurationSeconds ?? 0),
            estimatedCostUsd: Number(buddy.EstimatedCostUsd ?? buddy.estimatedCostUsd ?? 0),
          },
          courses: rawCourses.map((c) => ({
            structuredLearningDocumentId: String(c.StructuredLearningDocumentId ?? c.structuredLearningDocumentId ?? ''),
            documentName: String(c.DocumentName ?? c.documentName ?? ''),
            sectionCount: Number(c.SectionCount ?? c.sectionCount ?? 0),
            completedSectionCount: Number(c.CompletedSectionCount ?? c.completedSectionCount ?? 0),
            isCompleted: Boolean(c.IsCompleted ?? c.isCompleted ?? false),
            totalTimeSeconds: Number(c.TotalTimeSeconds ?? c.totalTimeSeconds ?? 0),
            totalInputTokens: Number(c.TotalInputTokens ?? c.totalInputTokens ?? 0),
            totalOutputTokens: Number(c.TotalOutputTokens ?? c.totalOutputTokens ?? 0),
            estimatedCostUsd: Number(c.EstimatedCostUsd ?? c.estimatedCostUsd ?? 0),
          })),
        });
      })
      .catch((err: Error) => setDashboardError(err.message));
  }, [personId]);

  useEffect(() => {
    if (!personId) return;
    setBrainCourses(null);
    setBrainError(null);
    fetch(`/api/people/${personId}/structured-learning-engram-stats`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load Brain usage.');
        return response.json() as Promise<Record<string, unknown>[]>;
      })
      .then((rows) => setBrainCourses(groupPersonBrainCourseStats(rows)))
      .catch((err: Error) => setBrainError(err.message));
  }, [personId]);

  const inProgressCourses = (courses ?? []).filter((c) => !(c.sectionCount > 0 && c.completedSectionCount >= c.sectionCount));
  const completedCourses = (courses ?? []).filter((c) => c.sectionCount > 0 && c.completedSectionCount >= c.sectionCount);
  const inProgressBrainCourses = brainCourses === null
    ? null
    : brainCourses.filter((c) => inProgressCourses.some((ic) => ic.structuredLearningDocumentId === c.courseId));

  return (
    <>
      <div className="main-title-row">
        <div>
          <p className="eyebrow">{isEnglish ? 'User detail' : 'Detalle de usuario'}</p>
          <h2>{person?.displayName ?? (personError ? '—' : isEnglish ? 'Loading...' : 'Cargando...')}</h2>
        </div>
        <button type="button" className="btn-ghost" onClick={() => navigate(`/tenants/${tenantId}`)}>
          {isEnglish ? '← Back to users' : '← Volver a usuarios'}
        </button>
      </div>

      {personError ? <div className="data-message error">{personError}</div> : person ? (
        <div className="customer-grid tenant-section-gap">
          <article className="customer-card">
            <div className="customer-card-icon"><Users size={21} /></div>
            <div>
              <h3>{person.displayName ?? (isEnglish ? 'Unnamed person' : 'Sin nombre')}</h3>
              <p className="customer-label">{isEnglish ? 'Email' : 'Correo'}</p>
              <code>{person.email ?? '—'}</code>
              <p className="customer-label">{isEnglish ? 'Role / Department' : 'Puesto / Departamento'}</p>
              <code>{person.jobRoleTitle ?? '—'} · {person.departmentName ?? '—'}</code>
            </div>
          </article>
        </div>
      ) : null}

      <div className="tenant-tabs tenant-section-gap">
        <button
          type="button"
          className={`tenant-tab${activeTab === 'dashboard' ? ' active' : ''}`}
          onClick={() => setActiveTab('dashboard')}
        >
          <BarChart3 size={16} /> Dashboard
        </button>
        <button
          type="button"
          className={`tenant-tab${activeTab === 'brain' ? ' active' : ''}`}
          onClick={() => setActiveTab('brain')}
        >
          <Headphones size={16} /> {isEnglish ? 'Brain / Cerebro' : 'Cerebro / Brain'}
        </button>
        <button
          type="button"
          className={`tenant-tab${activeTab === 'inProgress' ? ' active' : ''}`}
          onClick={() => setActiveTab('inProgress')}
        >
          <BookOpen size={16} /> {isEnglish ? 'Courses' : 'Cursos'} ({inProgressCourses.length})
        </button>
        <button
          type="button"
          className={`tenant-tab${activeTab === 'completed' ? ' active' : ''}`}
          onClick={() => setActiveTab('completed')}
        >
          <BookOpenCheck size={16} /> {isEnglish ? 'Completed courses' : 'Cursos terminados'} ({completedCourses.length})
        </button>
      </div>

      {activeTab === 'dashboard' ? (
        <PersonDashboardTab dashboard={dashboard} dashboardError={dashboardError} brainCourses={brainCourses} isEnglish={isEnglish} />
      ) : activeTab === 'brain' ? (
        <PersonRepasosTab personId={personId ?? ''} isEnglish={isEnglish} />
      ) : activeTab === 'inProgress' ? (
        <PersonBrainTab
          tenantId={tenantId ?? ''}
          personId={personId ?? ''}
          brainCourses={inProgressBrainCourses}
          brainError={brainError}
          isEnglish={isEnglish}
        />
      ) : coursesError ? <div className="data-message error">{coursesError}</div>
      : courses === null ? <div className="data-message">{isEnglish ? 'Loading courses...' : 'Cargando cursos...'}</div>
      : completedCourses.length === 0 ? (
        <div className="data-message">{isEnglish ? 'No completed courses yet.' : 'Aún no hay cursos terminados.'}</div>
      ) : (
        <div className="catalog-course-grid">
          {completedCourses.map((course) => {
            const costRow = dashboard?.courses.find((c) => c.structuredLearningDocumentId === course.structuredLearningDocumentId);
            return (
              <Link
                to={`/tenants/${tenantId}/people/${personId}/courses/${course.structuredLearningDocumentId}`}
                className="catalog-course-card"
                key={course.structuredLearningDocumentId}
              >
                <div className="catalog-course-cover">
                  {course.coverIllustrationId ? <img src={`/api/illustrations/structured-learning/${course.coverIllustrationId}/image`} alt="" /> : <BookOpen size={28} />}
                  <span className="catalog-course-badge">{course.language}</span>
                  <span className="catalog-course-global">{isEnglish ? 'Completed' : 'Terminado'}</span>
                </div>
                <div className="catalog-course-content">
                  <h3>{course.documentName}</h3>
                  <p className="catalog-course-meta">
                    {isEnglish ? 'Enrolled' : 'Inscrito'}: {course.enrolledDate ? new Date(course.enrolledDate).toLocaleDateString(isEnglish ? 'en-US' : 'es-MX') : '—'}
                  </p>
                  <p className="catalog-course-meta">
                    <DollarSign size={14} /> {isEnglish ? 'Total cost' : 'Costo total'}: <strong>{costRow ? formatUsd(costRow.estimatedCostUsd) : '—'}</strong>
                  </p>
                </div>
                <div className="catalog-course-action">{isEnglish ? 'View statistics →' : 'Ver estadísticas →'}</div>
              </Link>
            );
          })}
        </div>
      )}
    </>
  );
}

/** Per-user, per-course statistical dashboard (2026-10-03) — same design as
 * the global <see cref="CourseDetailPage"/> "Course dashboard"/"Overview"
 * tabs, but every engram's stats (time, tokens, Recall, Assessment) are
 * scoped to just this one person's own sessions via ?personId= on
 * GetStructuredLearningEngramStats. Course content totals (engram/chapter/
 * fact counts) stay course-wide since the content itself is shared. */
function PersonCourseDetailPage({ isEnglish }: { isEnglish: boolean }) {
  const { tenantId, personId, courseId } = useParams<{ tenantId: string; personId: string; courseId: string }>();
  const navigate = useNavigate();
  const [pageTab, setPageTab] = useState<'overview' | 'dashboard'>('overview');
  const [personName, setPersonName] = useState<string | null>(null);
  const [documentName, setDocumentName] = useState<string>('');
  const [summary, setSummary] = useState<CourseSummary>(null);
  const [engrams, setEngrams] = useState<GlobalEngramStat[]>([]);
  const [engramsError, setEngramsError] = useState<string | null>(null);

  useEffect(() => {
    if (!tenantId || !personId) return;
    fetch(`/api/tenants/${tenantId}/people`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load person.');
        return response.json() as Promise<Record<string, unknown>[]>;
      })
      .then((rows) => {
        const row = rows.find((r) => String(r.PersonId ?? r.personId ?? '') === personId);
        setPersonName(row ? (String(row.DisplayName ?? row.displayName ?? '') || null) : null);
      })
      .catch(() => setPersonName(null));
  }, [tenantId, personId]);

  useEffect(() => {
    if (!courseId || !personId) return;

    fetch(`/api/studio/structured-learning/${courseId}/summary`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load course summary.');
        return response.json() as Promise<Record<string, unknown>>;
      })
      .then((row) => setSummary({
        engramsCount: Number(row.EngramsCount ?? row.engramsCount ?? 0),
        chaptersCount: Number(row.ChaptersCount ?? row.chaptersCount ?? 0),
        factsCount: Number(row.FactsCount ?? row.factsCount ?? 0),
      }))
      .catch(() => setSummary(null));

    fetch(`/api/studio/structured-learning/${courseId}`)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load course engrams.');
        return response.json() as Promise<Record<string, unknown>>;
      })
      .then(async (document) => {
        setDocumentName(String(document.DocumentName ?? document.documentName ?? ''));
        const sections = (document.Sections ?? document.sections ?? []) as Record<string, unknown>[];
        const rows = await Promise.all(sections.map(async (section) => {
          const sectionId = String(section.StructuredLearningDocumentSectionId ?? section.structuredLearningDocumentSectionId);
          const statsResponse = await fetch(`/api/structured-learning/sections/${sectionId}/stats?personId=${personId}`);
          const stats = statsResponse.ok ? await statsResponse.json() as Record<string, unknown> : {};
          const recall = (stats.Recall ?? stats.recall ?? {}) as Record<string, unknown>;
          const assessment = (stats.Assessment ?? stats.assessment ?? {}) as Record<string, unknown>;
          const rawStepSummaries = (stats.StepSummaries ?? stats.stepSummaries ?? []) as Record<string, unknown>[];
          return {
            sectionId,
            chapterTitle: String(section.ChapterTitle ?? section.chapterTitle ?? ''),
            subchapterTitle: String(section.SubchapterTitle ?? section.subchapterTitle ?? ''),
            totalSessionsVisited: Number(stats.TotalSessionsVisited ?? stats.totalSessionsVisited ?? 0),
            totalTimeSeconds: Number(stats.TotalTimeSeconds ?? stats.totalTimeSeconds ?? 0),
            averageTimeSecondsPerVisit: Number(stats.AverageTimeSecondsPerVisit ?? stats.averageTimeSecondsPerVisit ?? 0),
            recallAttempts: Number(recall.AttemptsCount ?? recall.attemptsCount ?? 0),
            recallMastered: Number(recall.MasteredCount ?? recall.masteredCount ?? 0),
            assessmentRounds: Number(assessment.RoundsCount ?? assessment.roundsCount ?? 0),
            assessmentPassed: Number(assessment.PassedRounds ?? assessment.passedRounds ?? 0),
            inputTokens: Number(stats.TotalInputTokens ?? stats.totalInputTokens ?? 0),
            outputTokens: Number(stats.TotalOutputTokens ?? stats.totalOutputTokens ?? 0),
            estimatedCostUsd: Number(stats.EstimatedCostUsd ?? stats.estimatedCostUsd ?? 0),
            stepSummaries: rawStepSummaries.map((s) => ({
              stepType: String(s.StepType ?? s.stepType ?? ''),
              sessionsCount: Number(s.SessionsCount ?? s.sessionsCount ?? 0),
              totalDurationSeconds: Number(s.TotalDurationSeconds ?? s.totalDurationSeconds ?? 0),
              averageDurationSeconds: Number(s.AverageDurationSeconds ?? s.averageDurationSeconds ?? 0),
              inputTokens: Number(s.InputTokens ?? s.inputTokens ?? 0),
              outputTokens: Number(s.OutputTokens ?? s.outputTokens ?? 0),
              modelNames: String(s.ModelNames ?? s.modelNames ?? ''),
              estimatedCostUsd: Number(s.EstimatedCostUsd ?? s.estimatedCostUsd ?? 0),
            })),
          };
        }));
        setEngrams(rows);
      })
      .catch((err: Error) => setEngramsError(err.message));
  }, [courseId, personId]);

  const totalInputTokens = engrams.reduce((total, e) => total + e.inputTokens, 0);
  const totalOutputTokens = engrams.reduce((total, e) => total + e.outputTokens, 0);
  const totalTimeMs = engrams.reduce((total, e) => total + e.totalTimeSeconds, 0) * 1000;
  const totalCostUsd = engrams.reduce((total, e) => total + e.estimatedCostUsd, 0);
  const totalRecallMastered = engrams.reduce((total, e) => total + e.recallMastered, 0);
  const totalRecallAttempts = engrams.reduce((total, e) => total + e.recallAttempts, 0);
  const totalAssessmentPassed = engrams.reduce((total, e) => total + e.assessmentPassed, 0);
  const totalAssessmentRounds = engrams.reduce((total, e) => total + e.assessmentRounds, 0);
  const totalVisits = engrams.reduce((total, e) => total + e.totalSessionsVisited, 0);

  return <>
    <div className="main-title-row">
      <div>
        <p className="eyebrow">{isEnglish ? 'Course detail · per user' : 'Detalle del curso · por usuario'}</p>
        <h2>{documentName || (isEnglish ? 'Loading...' : 'Cargando...')}</h2>
        <p className="customer-label">{personName ? (isEnglish ? `For ${personName}` : `Para ${personName}`) : ''}</p>
      </div>
      <button type="button" className="btn-ghost" onClick={() => navigate(`/tenants/${tenantId}/people/${personId}`)}>
        {isEnglish ? '← Back to user' : '← Volver al usuario'}
      </button>
    </div>

    <div className="tenant-tabs">
      <button type="button" className={`tenant-tab${pageTab === 'overview' ? ' active' : ''}`} onClick={() => setPageTab('overview')}>
        <BookOpenCheck size={16} /> {isEnglish ? 'Overview' : 'Vista general'}
      </button>
      <button type="button" className={`tenant-tab${pageTab === 'dashboard' ? ' active' : ''}`} onClick={() => setPageTab('dashboard')}>
        <BarChart3 size={16} /> {isEnglish ? 'User dashboard' : 'Dashboard del usuario'}
      </button>
    </div>

    {pageTab === 'dashboard' ? (
      <CourseRuntimeDashboard engrams={engrams} engramsError={engramsError} isEnglish={isEnglish} />
    ) : <>
      <div className="cost-hero">
        <div className="cost-hero-stat"><Users size={18} /><span>{totalVisits} {isEnglish ? 'visits' : 'visitas'}</span></div>
        <div className="cost-hero-stat"><Clock size={18} /><span>{formatSeconds(totalTimeMs)}</span></div>
        <div className="cost-hero-stat"><Coins size={18} /><span>{formatNumber(totalInputTokens + totalOutputTokens)} {isEnglish ? 'tokens' : 'tokens'}</span></div>
        <div className="cost-hero-stat"><BookOpenCheck size={18} /><span>{totalRecallMastered}/{totalRecallAttempts} recall</span></div>
        <div className="cost-hero-stat"><BarChart3 size={18} /><span>{totalAssessmentPassed}/{totalAssessmentRounds} assessment</span></div>
        <div className="cost-hero-stat cost-hero-total"><DollarSign size={18} /><span>{formatUsd(totalCostUsd)} ({isEnglish ? 'est.' : 'estimado'})</span></div>
      </div>

      {summary ? <div className="stat-card-grid tenant-section-gap">
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Total engrams' : 'Total de engramas'}</p><h3>{summary.engramsCount}</h3></div>
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Total chapters' : 'Total de capítulos'}</p><h3>{summary.chaptersCount}</h3></div>
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Total knowledge facts' : 'Total de facts'}</p><h3>{summary.factsCount}</h3></div>
      </div> : null}

      <section className="engram-stat-section">
        <div className="section-heading-row">
          <div>
            <p className="eyebrow">{isEnglish ? "This user's statistics" : 'Estadísticas de este usuario'}</p>
            <h3>Engrams</h3>
          </div>
          <span className="section-count">{engrams.length} {isEnglish ? 'engrams' : 'engrams'}</span>
        </div>
        {engramsError ? <div className="data-message error">{engramsError}</div>
        : engrams.length === 0 ? <div className="data-message">{isEnglish ? 'Loading engrams...' : 'Cargando engrams...'}</div>
        : <div className="engram-stat-grid">
          {engrams.map((engram, index) => (
            <div className="engram-stat-card" key={engram.sectionId}>
              <div className="engram-stat-number">{String(index + 1).padStart(2, '0')}</div>
              <div className="engram-stat-content">
                <p className="engram-stat-chapter">{engram.chapterTitle || (isEnglish ? 'Chapter' : 'Capítulo')}</p>
                <h4>{engram.subchapterTitle || (isEnglish ? 'Untitled engram' : 'Engram sin título')}</h4>
                <div className="engram-stat-metrics">
                  <span><strong>{engram.totalSessionsVisited}</strong> {isEnglish ? 'visits' : 'visitas'}</span>
                  <span><strong>{formatSeconds(engram.totalTimeSeconds * 1000)}</strong> {isEnglish ? 'time' : 'tiempo'}</span>
                  <span><strong>{engram.recallMastered}/{engram.recallAttempts}</strong> recall</span>
                  <span><strong>{engram.assessmentPassed}/{engram.assessmentRounds}</strong> assessment</span>
                </div>
                <p className="engram-stat-tokens">{formatNumber(engram.inputTokens + engram.outputTokens)} {isEnglish ? 'tokens' : 'tokens'}</p>
              </div>
            </div>
          ))}
        </div>}
      </section>
    </>}
  </>;
}

function TenantCourseTotalsView({ totals, consumedCourseCount, consumedCourseError, monthlyCosts, monthlyCostsError, isEnglish }: {
  totals: TenantCourseTotals;
  consumedCourseCount: number | null;
  consumedCourseError: string | null;
  monthlyCosts: TenantMonthlyCost[] | null;
  monthlyCostsError: string | null;
  isEnglish: boolean;
}) {
  return (
    <>
      <div className="cost-hero tenant-section-gap">
        <div className="cost-hero-stat"><BookOpen size={18} /><span>{totals.courseCount} {isEnglish ? 'courses created' : 'cursos creados'}</span></div>
        <div className="cost-hero-stat">
          <BookOpenCheck size={18} />
          <span>{consumedCourseError
            ? (isEnglish ? 'Unable to load consumed courses' : 'No se pudieron cargar los cursos consumidos')
            : consumedCourseCount === null
              ? (isEnglish ? 'Loading consumed courses...' : 'Cargando cursos consumidos...')
              : `${consumedCourseCount} ${isEnglish ? 'courses consumed' : 'cursos consumidos'}`}</span>
        </div>
        <div className="cost-hero-stat"><Clock size={18} /><span>{formatSeconds(totals.totalElapsedMilliseconds)}</span></div>
        <div className="cost-hero-stat"><Coins size={18} /><span>{formatNumber(totals.totalTokens)} {isEnglish ? 'tokens' : 'tokens'}</span></div>
        <div className="cost-hero-stat"><ImageIcon size={18} /><span>{totals.imagesGeneratedCount} {isEnglish ? 'images' : 'imágenes'}</span></div>
        <div className="cost-hero-stat"><Video size={18} /><span>{totals.videosGeneratedCount} {isEnglish ? 'videos' : 'videos'}</span></div>
        <div className="cost-hero-stat cost-hero-total"><DollarSign size={18} /><span>{formatUsd(totals.estimatedCostUsd)} ({isEnglish ? 'est.' : 'estimado'})</span></div>
      </div>

      <div className="stat-card-grid tenant-section-gap">
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Total engrams' : 'Total de engramas'}</p><h3>{totals.engramsCount}</h3></div>
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Total chapters' : 'Total de capítulos'}</p><h3>{totals.chaptersCount}</h3></div>
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Total knowledge facts' : 'Total de facts'}</p><h3>{totals.factsCount}</h3></div>
      </div>

      <div className="tenant-section-gap">
        <p className="customer-label">{isEnglish ? 'Estimated spend by month' : 'Gasto estimado por mes'}</p>
        {monthlyCostsError ? <div className="data-message error">{monthlyCostsError}</div>
        : monthlyCosts === null ? <div className="data-message">{isEnglish ? 'Loading monthly costs...' : 'Cargando costos mensuales...'}</div>
        : monthlyCosts.length === 0 ? <div className="data-message">{isEnglish ? 'No cost history yet.' : 'Aún no hay historial de costos.'}</div>
        : <MonthlyCostChart months={monthlyCosts} />}
      </div>

      <div className="cost-table-wrap tenant-section-gap">
        <table className="cost-table">
          <thead>
            <tr>
              <th>{isEnglish ? 'Step' : 'Paso'}</th>
              <th>{isEnglish ? 'Model' : 'Modelo'}</th>
              <th>{isEnglish ? 'Time' : 'Tiempo'}</th>
              <th>{isEnglish ? 'Tokens in' : 'Tokens entrada'}</th>
              <th>{isEnglish ? 'Tokens out' : 'Tokens salida'}</th>
              <th>{isEnglish ? 'Images' : 'Imágenes'}</th>
              <th>{isEnglish ? 'Videos' : 'Videos'}</th>
              <th>{isEnglish ? 'Est. cost' : 'Costo est.'}</th>
            </tr>
          </thead>
          <tbody>
            {totals.steps.map((step, index) => (
              <tr key={`${step.stepName}-${index}`}>
                <td>{step.stepName}</td>
                <td>{step.models || '—'}</td>
                <td>{formatSeconds(step.totalElapsedMilliseconds)}</td>
                <td>{formatNumber(step.inputTokens)}</td>
                <td>{formatNumber(step.outputTokens)}</td>
                <td>{step.imagesGeneratedCount || '—'}</td>
                <td>{step.videosGeneratedCount || '—'}</td>
                <td>{formatUsd(step.estimatedCostUsd)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

function TenantCourseConsumptionTotalsView({ totals, isEnglish }: { totals: TenantCourseConsumptionTotals; isEnglish: boolean }) {
  const voiceUsage = totals.usageByChannel.filter((u) => u.channel === 'Voice');
  const voiceCostShare = voiceUsage.reduce((sum, u) => sum + u.inputTokens + u.outputTokens, 0);
  const voiceDurationSeconds = voiceUsage.reduce((sum, u) => sum + u.durationSeconds, 0);

  return (
    <>
      <div className="cost-hero tenant-section-gap">
        <div className="cost-hero-stat"><BookOpenCheck size={18} /><span>{totals.courseCount} {isEnglish ? 'courses with activity' : 'cursos con actividad'}</span></div>
        <div className="cost-hero-stat"><Layers3 size={18} /><span>{totals.engramsVisitedCount} {isEnglish ? 'engrams visited' : 'engramas visitados'}</span></div>
        <div className="cost-hero-stat"><Activity size={18} /><span>{totals.sessionsCount} {isEnglish ? 'sessions' : 'sesiones'}</span></div>
        <div className="cost-hero-stat"><Headphones size={18} /><span>{totals.voiceSessionCount} {isEnglish ? 'realtime voice sessions' : 'sesiones de voz en tiempo real'}</span></div>
        <div className="cost-hero-stat"><Clock size={18} /><span>{formatSeconds(totals.totalTimeSeconds * 1000)}</span></div>
        <div className="cost-hero-stat"><Coins size={18} /><span>{formatNumber(totals.totalTokens)} {isEnglish ? 'tokens' : 'tokens'}</span></div>
        <div className="cost-hero-stat cost-hero-total"><DollarSign size={18} /><span>{formatUsd(totals.estimatedCostUsd)} ({isEnglish ? 'est.' : 'estimado'})</span></div>
      </div>

      <div className="stat-card-grid tenant-section-gap">
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Recall attempts' : 'Intentos de Recall'}</p><h3>{totals.recall.attemptsCount}</h3></div>
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Recall mastered' : 'Recall dominados'}</p><h3>{totals.recall.masteredCount}</h3></div>
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Recall failed' : 'Recall fallidos'}</p><h3>{totals.recall.failedCount}</h3></div>
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Assessment rounds' : 'Rondas de Assessment'}</p><h3>{totals.assessment.roundsCount}</h3></div>
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Assessment passed' : 'Assessment aprobados'}</p><h3>{totals.assessment.passedRounds}</h3></div>
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Assessment failed' : 'Assessment reprobados'}</p><h3>{totals.assessment.failedRounds}</h3></div>
      </div>

      <div className="stat-card-grid tenant-section-gap">
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Realtime voice tokens' : 'Tokens de voz en tiempo real'}</p><h3>{formatNumber(voiceCostShare)}</h3></div>
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Realtime voice duration' : 'Duración de voz en tiempo real'}</p><h3>{formatSeconds(voiceDurationSeconds * 1000)}</h3></div>
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Text-to-speech characters' : 'Caracteres de texto a voz'}</p><h3>{formatNumber(totals.textToSpeechCharacterCount)}</h3></div>
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Text-to-speech est. cost' : 'Costo est. texto a voz'}</p><h3>{formatUsd(totals.textToSpeechEstimatedCostUsd)}</h3></div>
      </div>

      <div className="cost-table-wrap tenant-section-gap">
        <table className="cost-table">
          <thead>
            <tr>
              <th>{isEnglish ? 'Channel' : 'Canal'}</th>
              <th>{isEnglish ? 'Model' : 'Modelo'}</th>
              <th>{isEnglish ? 'Tokens in' : 'Tokens entrada'}</th>
              <th>{isEnglish ? 'Tokens out' : 'Tokens salida'}</th>
              <th>{isEnglish ? 'Duration' : 'Duración'}</th>
            </tr>
          </thead>
          <tbody>
            {totals.usageByChannel.map((usage, index) => (
              <tr key={`${usage.channel}-${usage.modelName}-${index}`}>
                <td>{usage.channel || '—'}</td>
                <td>{usage.modelName || '—'}</td>
                <td>{formatNumber(usage.inputTokens)}</td>
                <td>{formatNumber(usage.outputTokens)}</td>
                <td>{formatSeconds(usage.durationSeconds * 1000)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

function MonthlyCostChart({ months }: { months: TenantMonthlyCost[] }) {
  const maxCost = Math.max(...months.map((m) => m.estimatedCostUsd), 0.01);
  return (
    <div className="monthly-cost-chart">
      {months.map((month) => (
        <div className="monthly-cost-bar-col" key={month.yearMonth}>
          <p className="monthly-cost-value">{formatUsd(month.estimatedCostUsd)}</p>
          <div className="monthly-cost-bar" style={{ height: `${(month.estimatedCostUsd / maxCost) * 100}%` }} />
          <p className="monthly-cost-label">{month.yearMonth}</p>
        </div>
      ))}
    </div>
  );
}

/** Generic bar-per-month chart for the platform usage dashboard — same
 * visual language as {@link MonthlyCostChart} but accepts any metric
 * (cost, tokens, sessions, minutes) via a caller-supplied formatter. */
function MonthlyMetricChart({ data, formatValue }: { data: { yearMonth: string; value: number }[]; formatValue: (value: number) => string }) {
  const maxValue = Math.max(...data.map((d) => d.value), 0.01);
  return (
    <div className="monthly-cost-chart">
      {data.map((d) => (
        <div className="monthly-cost-bar-col" key={d.yearMonth}>
          <p className="monthly-cost-value">{formatValue(d.value)}</p>
          <div className="monthly-cost-bar" style={{ height: `${(d.value / maxValue) * 100}%` }} />
          <p className="monthly-cost-label">{d.yearMonth}</p>
        </div>
      ))}
    </div>
  );
}

type PlatformCoursesGeneratedMonth = {
  yearMonth: string;
  coursesCreatedCount: number;
  inputTokens: number;
  outputTokens: number;
  imagesGeneratedCount: number;
  videosGeneratedCount: number;
  estimatedCostUsd: number;
};

type PlatformCoursesTakenMonth = {
  yearMonth: string;
  sessionsCount: number;
  voiceSessionCount: number;
  voiceDurationSeconds: number;
  inputTokens: number;
  outputTokens: number;
  textToSpeechCharacterCount: number;
  estimatedCostUsd: number;
};

type PlatformBuddyMonth = { yearMonth: string; sessionCount: number; totalDurationSeconds: number; estimatedCostUsd: number };

type PlatformAgentMonth = {
  yearMonth: string;
  stepType: string;
  modelName: string;
  channel: string;
  callCount: number;
  inputTokens: number;
  outputTokens: number;
  estimatedCostUsd: number;
};

type PlatformLabsMonth = {
  yearMonth: string;
  attemptCount: number;
  voiceSessionCount: number;
  voiceDurationSeconds: number;
  evaluationCount: number;
  evaluationInputTokens: number;
  evaluationOutputTokens: number;
  estimatedCostUsd: number;
};

type PlatformBrainMonth = {
  yearMonth: string;
  sessionCount: number;
  totalDurationSeconds: number;
  brainStructuredLearningSessionCount: number;
  brainStructuredLearningInputTokens: number;
  brainStructuredLearningOutputTokens: number;
  brainStructuredLearningVoiceDurationSeconds: number;
  estimatedCostUsd: number;
  legacyReviewCostUsd: number;
  stepBreakdown: PlatformAgentMonth[];
};

function defaultUsageDashboardRange(): { from: string; to: string } {
  const now = new Date();
  const to = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
  const fromDate = new Date(now.getFullYear(), now.getMonth() - 5, 1);
  const from = `${fromDate.getFullYear()}-${String(fromDate.getMonth() + 1).padStart(2, '0')}`;
  return { from, to };
}

/** Platform-wide (ALL tenants combined), Power-BI-style usage dashboard
 * (2026-10-06) — one master month-range filter drives 6 panels: courses
 * created, courses taken, Buddy, in-course agents, Labs, and Brain review.
 * Backed by <see cref="HumanOS.Services.PlatformUsageDashboardService"/>'s
 * 6 /api/usage/* endpoints, each already bucketed by calendar month. */
function UsageDashboardPage({ isEnglish }: { isEnglish: boolean }) {
  const [{ from, to }, setRange] = useState(defaultUsageDashboardRange);

  const [coursesGenerated, setCoursesGenerated] = useState<PlatformCoursesGeneratedMonth[] | null>(null);
  const [coursesGeneratedError, setCoursesGeneratedError] = useState<string | null>(null);
  const [coursesTaken, setCoursesTaken] = useState<PlatformCoursesTakenMonth[] | null>(null);
  const [coursesTakenError, setCoursesTakenError] = useState<string | null>(null);
  const [buddy, setBuddy] = useState<PlatformBuddyMonth[] | null>(null);
  const [buddyError, setBuddyError] = useState<string | null>(null);
  const [agents, setAgents] = useState<PlatformAgentMonth[] | null>(null);
  const [agentsError, setAgentsError] = useState<string | null>(null);
  const [labs, setLabs] = useState<PlatformLabsMonth[] | null>(null);
  const [labsError, setLabsError] = useState<string | null>(null);
  const [brain, setBrain] = useState<PlatformBrainMonth[] | null>(null);
  const [brainError, setBrainError] = useState<string | null>(null);

  useEffect(() => {
    const qs = `?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`;

    setCoursesGenerated(null); setCoursesGeneratedError(null);
    fetch(`/api/usage/courses-generated${qs}`)
      .then((r) => { if (!r.ok) throw new Error('Unable to load courses-generated usage.'); return r.json(); })
      .then(setCoursesGenerated)
      .catch((err: Error) => setCoursesGeneratedError(err.message));

    setCoursesTaken(null); setCoursesTakenError(null);
    fetch(`/api/usage/courses-taken${qs}`)
      .then((r) => { if (!r.ok) throw new Error('Unable to load courses-taken usage.'); return r.json(); })
      .then(setCoursesTaken)
      .catch((err: Error) => setCoursesTakenError(err.message));

    setBuddy(null); setBuddyError(null);
    fetch(`/api/usage/buddy${qs}`)
      .then((r) => { if (!r.ok) throw new Error('Unable to load Buddy usage.'); return r.json(); })
      .then(setBuddy)
      .catch((err: Error) => setBuddyError(err.message));

    setAgents(null); setAgentsError(null);
    fetch(`/api/usage/agents${qs}`)
      .then((r) => { if (!r.ok) throw new Error('Unable to load agent usage.'); return r.json(); })
      .then(setAgents)
      .catch((err: Error) => setAgentsError(err.message));

    setLabs(null); setLabsError(null);
    fetch(`/api/usage/labs${qs}`)
      .then((r) => { if (!r.ok) throw new Error('Unable to load Labs usage.'); return r.json(); })
      .then(setLabs)
      .catch((err: Error) => setLabsError(err.message));

    setBrain(null); setBrainError(null);
    fetch(`/api/usage/brain${qs}`)
      .then((r) => { if (!r.ok) throw new Error('Unable to load Brain usage.'); return r.json(); })
      .then(setBrain)
      .catch((err: Error) => setBrainError(err.message));
  }, [from, to]);

  return (
    <>
      <div className="main-title-row">
        <div>
          <p className="eyebrow">{isEnglish ? 'Platform-wide' : 'Toda la plataforma'}</p>
          <h2>{isEnglish ? 'Usage dashboard' : 'Dashboard de uso'}</h2>
        </div>
        <div className="usage-dashboard-filterbar">
          <label>
            {isEnglish ? 'From' : 'Desde'}
            <input type="month" value={from} onChange={(e) => setRange((r) => ({ ...r, from: e.target.value }))} />
          </label>
          <label>
            {isEnglish ? 'To' : 'Hasta'}
            <input type="month" value={to} onChange={(e) => setRange((r) => ({ ...r, to: e.target.value }))} />
          </label>
        </div>
      </div>

      {(() => {
        const cg = coursesGenerated ?? [];
        const ct = coursesTaken ?? [];
        const bd = buddy ?? [];
        const lb = labs ?? [];
        const br = brain ?? [];
        const allLoaded = coursesGenerated !== null && coursesTaken !== null && buddy !== null && labs !== null && brain !== null;

        // Tokens/cost are summed WITHOUT double-counting: Panel 4 (Agentes)
        // and Panel 6's Structured-Learning slice both re-slice the SAME
        // rows already totalled in "Toma de cursos", so only Panel 6's
        // legacy (non-SL) review cost is added on top of it here.
        const totalInputTokens = cg.reduce((s, m) => s + m.inputTokens, 0) + ct.reduce((s, m) => s + m.inputTokens, 0) + lb.reduce((s, m) => s + m.evaluationInputTokens, 0);
        const totalOutputTokens = cg.reduce((s, m) => s + m.outputTokens, 0) + ct.reduce((s, m) => s + m.outputTokens, 0) + lb.reduce((s, m) => s + m.evaluationOutputTokens, 0);
        const totalVoiceSeconds = ct.reduce((s, m) => s + m.voiceDurationSeconds, 0) + bd.reduce((s, m) => s + m.totalDurationSeconds, 0) + lb.reduce((s, m) => s + m.voiceDurationSeconds, 0);
        const totalImages = cg.reduce((s, m) => s + m.imagesGeneratedCount, 0);
        const totalVideos = cg.reduce((s, m) => s + m.videosGeneratedCount, 0);
        const totalCoursesCreated = cg.reduce((s, m) => s + m.coursesCreatedCount, 0);
        const totalEngramsOpened = ct.reduce((s, m) => s + m.sessionsCount, 0);
        const totalGlobalCostUsd = cg.reduce((s, m) => s + m.estimatedCostUsd, 0)
          + ct.reduce((s, m) => s + m.estimatedCostUsd, 0)
          + bd.reduce((s, m) => s + m.estimatedCostUsd, 0)
          + lb.reduce((s, m) => s + m.estimatedCostUsd, 0)
          + br.reduce((s, m) => s + m.legacyReviewCostUsd, 0);

        return (
          <section className="usage-grand-total tenant-section-gap">
            <p className="usage-grand-total-title">
              {isEnglish ? 'Grand total — selected period' : 'Totales finales — periodo seleccionado'}
              {!allLoaded ? ` (${isEnglish ? 'partial, still loading...' : 'parcial, cargando...'})` : ''}
            </p>
            <div className="cost-hero">
              <div className="cost-hero-stat"><BookOpen size={18} /><span>{totalCoursesCreated} {isEnglish ? 'courses created' : 'cursos creados'}</span></div>
              <div className="cost-hero-stat"><BookOpenCheck size={18} /><span>{totalEngramsOpened} {isEnglish ? 'times engrams opened' : 'veces se abrieron engramas'}</span></div>
              <div className="cost-hero-stat"><Coins size={18} /><span>{formatNumber(totalInputTokens)} {isEnglish ? 'tokens in' : 'tokens in'}</span></div>
              <div className="cost-hero-stat"><Coins size={18} /><span>{formatNumber(totalOutputTokens)} {isEnglish ? 'tokens out' : 'tokens out'}</span></div>
              <div className="cost-hero-stat"><Headphones size={18} /><span>{formatSeconds(totalVoiceSeconds * 1000)} {isEnglish ? 'realtime voice' : 'voz en tiempo real'}</span></div>
              <div className="cost-hero-stat"><ImageIcon size={18} /><span>{totalImages} {isEnglish ? 'images' : 'imágenes'}</span></div>
              <div className="cost-hero-stat"><Video size={18} /><span>{totalVideos} {isEnglish ? 'videos' : 'videos'}</span></div>
              <div className="cost-hero-stat cost-hero-total"><DollarSign size={18} /><span>{formatUsd(totalGlobalCostUsd)} {isEnglish ? 'total est. cost' : 'costo total estimado'}</span></div>
            </div>
          </section>
        );
      })()}

      <UsagePanel
        icon={BookOpen}
        title={isEnglish ? '1 · Courses created (global)' : '1 · Creación de cursos (global)'}
        error={coursesGeneratedError}
        loading={coursesGenerated === null}
        isEnglish={isEnglish}
      >
        {coursesGenerated && (
          <>
            <div className="cost-hero">
              <div className="cost-hero-stat"><BookOpen size={18} /><span>{coursesGenerated.reduce((s, m) => s + m.coursesCreatedCount, 0)} {isEnglish ? 'courses' : 'cursos'}</span></div>
              <div className="cost-hero-stat"><Coins size={18} /><span>{formatNumber(coursesGenerated.reduce((s, m) => s + m.inputTokens + m.outputTokens, 0))} {isEnglish ? 'tokens' : 'tokens'}</span></div>
              <div className="cost-hero-stat"><ImageIcon size={18} /><span>{coursesGenerated.reduce((s, m) => s + m.imagesGeneratedCount, 0)} {isEnglish ? 'images' : 'imágenes'}</span></div>
              <div className="cost-hero-stat"><Video size={18} /><span>{coursesGenerated.reduce((s, m) => s + m.videosGeneratedCount, 0)} {isEnglish ? 'videos' : 'videos'}</span></div>
              <div className="cost-hero-stat cost-hero-total"><DollarSign size={18} /><span>{formatUsd(coursesGenerated.reduce((s, m) => s + m.estimatedCostUsd, 0))}</span></div>
            </div>
            <MonthlyMetricChart data={coursesGenerated.map((m) => ({ yearMonth: m.yearMonth, value: m.estimatedCostUsd }))} formatValue={formatUsd} />
            <UsageMonthlyTable
              columns={[isEnglish ? 'Month' : 'Mes', isEnglish ? 'Courses' : 'Cursos', 'Tokens in', 'Tokens out', isEnglish ? 'Images' : 'Imágenes', isEnglish ? 'Videos' : 'Videos', isEnglish ? 'Est. cost' : 'Costo est.']}
              rows={coursesGenerated.map((m) => [m.yearMonth, m.coursesCreatedCount, formatNumber(m.inputTokens), formatNumber(m.outputTokens), m.imagesGeneratedCount, m.videosGeneratedCount, formatUsd(m.estimatedCostUsd)])}
            />
          </>
        )}
      </UsagePanel>

      <UsagePanel
        icon={BookOpenCheck}
        title={isEnglish ? '2 · Courses taken (global)' : '2 · Toma de cursos (global)'}
        error={coursesTakenError}
        loading={coursesTaken === null}
        isEnglish={isEnglish}
      >
        {coursesTaken && (
          <>
            <div className="cost-hero">
              <div className="cost-hero-stat"><Activity size={18} /><span>{coursesTaken.reduce((s, m) => s + m.sessionsCount, 0)} {isEnglish ? 'times engrams were opened' : 'veces se abrieron engramas'}</span></div>
              <div className="cost-hero-stat"><Headphones size={18} /><span>{coursesTaken.reduce((s, m) => s + m.voiceSessionCount, 0)} {isEnglish ? 'voice sessions' : 'sesiones de voz'}</span></div>
              <div className="cost-hero-stat"><Coins size={18} /><span>{formatNumber(coursesTaken.reduce((s, m) => s + m.inputTokens + m.outputTokens, 0))} {isEnglish ? 'tokens' : 'tokens'}</span></div>
              <div className="cost-hero-stat cost-hero-total"><DollarSign size={18} /><span>{formatUsd(coursesTaken.reduce((s, m) => s + m.estimatedCostUsd, 0))}</span></div>
            </div>
            <MonthlyMetricChart data={coursesTaken.map((m) => ({ yearMonth: m.yearMonth, value: m.estimatedCostUsd }))} formatValue={formatUsd} />
            <UsageMonthlyTable
              columns={[isEnglish ? 'Month' : 'Mes', isEnglish ? 'Sessions' : 'Sesiones', isEnglish ? 'Voice sessions' : 'Sesiones voz', isEnglish ? 'Voice duration' : 'Duración voz', 'Tokens in', 'Tokens out', isEnglish ? 'TTS chars' : 'Caract. TTS', isEnglish ? 'Est. cost' : 'Costo est.']}
              rows={coursesTaken.map((m) => [m.yearMonth, m.sessionsCount, m.voiceSessionCount, formatSeconds(m.voiceDurationSeconds * 1000), formatNumber(m.inputTokens), formatNumber(m.outputTokens), formatNumber(m.textToSpeechCharacterCount), formatUsd(m.estimatedCostUsd)])}
            />
          </>
        )}
      </UsagePanel>

      <UsagePanel
        icon={Bot}
        title={isEnglish ? '3 · Buddy usage' : '3 · Uso de Buddy'}
        error={buddyError}
        loading={buddy === null}
        isEnglish={isEnglish}
      >
        {buddy && (
          <>
            <div className="cost-hero">
              <div className="cost-hero-stat"><Activity size={18} /><span>{buddy.reduce((s, m) => s + m.sessionCount, 0)} {isEnglish ? 'sessions' : 'sesiones'}</span></div>
              <div className="cost-hero-stat"><Clock size={18} /><span>{formatSeconds(buddy.reduce((s, m) => s + m.totalDurationSeconds, 0) * 1000)}</span></div>
              <div className="cost-hero-stat cost-hero-total"><DollarSign size={18} /><span>{formatUsd(buddy.reduce((s, m) => s + m.estimatedCostUsd, 0))}</span></div>
            </div>
            <MonthlyMetricChart data={buddy.map((m) => ({ yearMonth: m.yearMonth, value: m.estimatedCostUsd }))} formatValue={formatUsd} />
            <UsageMonthlyTable
              columns={[isEnglish ? 'Month' : 'Mes', isEnglish ? 'Sessions' : 'Sesiones', isEnglish ? 'Duration' : 'Duración', isEnglish ? 'Est. cost' : 'Costo est.']}
              rows={buddy.map((m) => [m.yearMonth, m.sessionCount, formatSeconds(m.totalDurationSeconds * 1000), formatUsd(m.estimatedCostUsd)])}
            />
          </>
        )}
      </UsagePanel>

      <UsagePanel
        icon={Sparkles}
        title={isEnglish ? '4 · Agents usage (in-course)' : '4 · Uso de Agentes (cursos)'}
        error={agentsError}
        loading={agents === null}
        isEnglish={isEnglish}
      >
        {agents && (
          <>
            <div className="cost-hero">
              <div className="cost-hero-stat"><Bot size={18} /><span>{agents.reduce((s, m) => s + m.callCount, 0)} {isEnglish ? 'calls' : 'llamadas'}</span></div>
              <div className="cost-hero-stat"><Coins size={18} /><span>{formatNumber(agents.reduce((s, m) => s + m.inputTokens + m.outputTokens, 0))} {isEnglish ? 'tokens' : 'tokens'}</span></div>
              <div className="cost-hero-stat"><Sparkles size={18} /><span>{formatUsd(agents.filter((m) => m.stepType === 'Production').reduce((s, m) => s + m.estimatedCostUsd, 0))} {isEnglish ? '"In your role"' : '"En tu rol"'}</span></div>
              <div className="cost-hero-stat cost-hero-total"><DollarSign size={18} /><span>{formatUsd(agents.reduce((s, m) => s + m.estimatedCostUsd, 0))}</span></div>
            </div>
            <UsageMonthlyTable
              columns={[isEnglish ? 'Month' : 'Mes', isEnglish ? 'Step' : 'Paso', isEnglish ? 'Model' : 'Modelo', isEnglish ? 'Channel' : 'Canal', isEnglish ? 'Calls' : 'Llamadas', 'Tokens in', 'Tokens out', isEnglish ? 'Est. cost' : 'Costo est.']}
              rows={agents.map((m) => [m.yearMonth, STEP_TYPE_LABELS[m.stepType]?.[isEnglish ? 'en' : 'es'] ?? m.stepType, m.modelName, m.channel, m.callCount, formatNumber(m.inputTokens), formatNumber(m.outputTokens), formatUsd(m.estimatedCostUsd)])}
            />
          </>
        )}
      </UsagePanel>

      <UsagePanel
        icon={FlaskConical}
        title={isEnglish ? '5 · Labs usage' : '5 · Uso de Labs'}
        error={labsError}
        loading={labs === null}
        isEnglish={isEnglish}
      >
        {labs && (
          <>
            <div className="cost-hero">
              <div className="cost-hero-stat"><FlaskConical size={18} /><span>{labs.reduce((s, m) => s + m.attemptCount, 0)} {isEnglish ? 'attempts' : 'intentos'}</span></div>
              <div className="cost-hero-stat"><Headphones size={18} /><span>{labs.reduce((s, m) => s + m.voiceSessionCount, 0)} {isEnglish ? 'voice sessions' : 'sesiones de voz'}</span></div>
              <div className="cost-hero-stat"><Coins size={18} /><span>{formatNumber(labs.reduce((s, m) => s + m.evaluationInputTokens + m.evaluationOutputTokens, 0))} {isEnglish ? 'eval. tokens' : 'tokens evaluación'}</span></div>
              <div className="cost-hero-stat cost-hero-total"><DollarSign size={18} /><span>{formatUsd(labs.reduce((s, m) => s + m.estimatedCostUsd, 0))}</span></div>
            </div>
            <MonthlyMetricChart data={labs.map((m) => ({ yearMonth: m.yearMonth, value: m.estimatedCostUsd }))} formatValue={formatUsd} />
            <UsageMonthlyTable
              columns={[isEnglish ? 'Month' : 'Mes', isEnglish ? 'Attempts' : 'Intentos', isEnglish ? 'Voice sessions' : 'Sesiones voz', isEnglish ? 'Voice duration' : 'Duración voz', isEnglish ? 'Evaluations' : 'Evaluaciones', isEnglish ? 'Est. cost' : 'Costo est.']}
              rows={labs.map((m) => [m.yearMonth, m.attemptCount, m.voiceSessionCount, formatSeconds(m.voiceDurationSeconds * 1000), m.evaluationCount, formatUsd(m.estimatedCostUsd)])}
            />
          </>
        )}
      </UsagePanel>

      <UsagePanel
        icon={Brain}
        title={isEnglish ? '6 · "My Brain" page usage' : '6 · Uso de la página "Mi Cerebro"'}
        error={brainError}
        loading={brain === null}
        isEnglish={isEnglish}
      >
        {brain && (
          <>
            <p className="customer-label tenant-section-gap">
              {isEnglish
                ? 'Every time an employee reviews something they already mastered from the "My Brain" page (chatting with the Agent by voice or text, or doing a spaced-repetition review).'
                : 'Cada vez que un empleado repasa algo que ya dominó desde la página "Mi Cerebro" (hablando con el Agente por voz o texto, o haciendo un repaso espaciado).'}
            </p>
            <div className="cost-hero">
              <div className="cost-hero-stat">
                <BookOpenCheck size={18} />
                <span>
                  {brain.reduce((s, m) => s + m.sessionCount + m.brainStructuredLearningSessionCount, 0)} {isEnglish ? 'times reviewed' : 'veces repasado'}
                </span>
              </div>
              <div className="cost-hero-stat">
                <Clock size={18} />
                <span>{formatSeconds(brain.reduce((s, m) => s + m.totalDurationSeconds + m.brainStructuredLearningVoiceDurationSeconds, 0) * 1000)} {isEnglish ? 'total time' : 'tiempo total'}</span>
              </div>
              <div className="cost-hero-stat">
                <Coins size={18} />
                <span>{formatNumber(brain.reduce((s, m) => s + m.brainStructuredLearningInputTokens + m.brainStructuredLearningOutputTokens, 0))} {isEnglish ? 'tokens' : 'tokens'}</span>
              </div>
              <div className="cost-hero-stat cost-hero-total"><DollarSign size={18} /><span>{formatUsd(brain.reduce((s, m) => s + m.estimatedCostUsd, 0))} ({isEnglish ? 'est.' : 'estimado'})</span></div>
            </div>
            <MonthlyMetricChart data={brain.map((m) => ({ yearMonth: m.yearMonth, value: m.estimatedCostUsd }))} formatValue={formatUsd} />
            <UsageMonthlyTable
              columns={[isEnglish ? 'Month' : 'Mes', isEnglish ? 'Times reviewed' : 'Veces repasado', isEnglish ? 'Time' : 'Tiempo', isEnglish ? 'Tokens' : 'Tokens', isEnglish ? 'Est. cost' : 'Costo est.']}
              rows={brain.map((m) => [
                m.yearMonth,
                m.sessionCount + m.brainStructuredLearningSessionCount,
                formatSeconds((m.totalDurationSeconds + m.brainStructuredLearningVoiceDurationSeconds) * 1000),
                formatNumber(m.brainStructuredLearningInputTokens + m.brainStructuredLearningOutputTokens),
                formatUsd(m.estimatedCostUsd),
              ])}
            />
            <p className="customer-label tenant-section-gap">
              {isEnglish
                ? 'Breakdown by learning step — ONLY conversations that happened inside "My Brain" (never the course-taking Agents, which is a separate cost in Panel 4):'
                : 'Desglose por paso de aprendizaje — SOLO conversaciones que ocurrieron dentro de "Mi Cerebro" (nunca los Agentes de los cursos, que es un costo totalmente aparte en el Panel 4):'}
            </p>
            {(() => {
              const brainBreakdown = brain.flatMap((m) => m.stepBreakdown);
              return brainBreakdown.length === 0
                ? <div className="data-message">{isEnglish ? 'No "My Brain" conversations yet in this range.' : 'Aún no hay conversaciones de "Mi Cerebro" en este rango.'}</div>
                : <UsageMonthlyTable
                    columns={[isEnglish ? 'Month' : 'Mes', isEnglish ? 'Step' : 'Paso', isEnglish ? 'Model' : 'Modelo', isEnglish ? 'Channel' : 'Canal', isEnglish ? 'Calls' : 'Llamadas', 'Tokens in', 'Tokens out', isEnglish ? 'Est. cost' : 'Costo est.']}
                    rows={brainBreakdown.map((m) => [m.yearMonth, STEP_TYPE_LABELS[m.stepType]?.[isEnglish ? 'en' : 'es'] ?? m.stepType, m.modelName, m.channel, m.callCount, formatNumber(m.inputTokens), formatNumber(m.outputTokens), formatUsd(m.estimatedCostUsd)])}
                  />;
            })()}
          </>
        )}
      </UsagePanel>
    </>
  );
}

function UsagePanel({ icon: Icon, title, error, loading, isEnglish, children }: {
  icon: typeof Activity;
  title: string;
  error: string | null;
  loading: boolean;
  isEnglish: boolean;
  children: ReactNode;
}) {
  return (
    <section className="usage-panel tenant-section-gap">
      <h3 className="usage-panel-title"><Icon size={18} /> {title}</h3>
      {error ? <div className="data-message error">{error}</div>
        : loading ? <div className="data-message">{isEnglish ? 'Loading...' : 'Cargando...'}</div>
        : children}
    </section>
  );
}

function UsageMonthlyTable({ columns, rows }: { columns: string[]; rows: (string | number)[][] }) {
  if (rows.length === 0) return null;
  return (
    <div className="cost-table-wrap tenant-section-gap">
      <table className="cost-table">
        <thead>
          <tr>{columns.map((c) => <th key={c}>{c}</th>)}</tr>
        </thead>
        <tbody>
          {rows.map((row, i) => (
            <tr key={i}>{row.map((cell, j) => <td key={j}>{cell}</td>)}</tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function computeStats(values: number[]): { max: number; min: number; average: number; stdDev: number } {
  if (values.length === 0) return { max: 0, min: 0, average: 0, stdDev: 0 };
  const max = Math.max(...values);
  const min = Math.min(...values);
  const average = values.reduce((sum, v) => sum + v, 0) / values.length;
  const variance = values.reduce((sum, v) => sum + (v - average) ** 2, 0) / values.length;
  return { max, min, average, stdDev: Math.sqrt(variance) };
}

function MediaHistogramStats({ courses, isEnglish }: { courses: TenantCourseMediaBreakdown[]; isEnglish: boolean }) {
  const imageStats = computeStats(courses.map((c) => c.imagesCount));
  const videoStats = computeStats(courses.map((c) => c.videosCount));
  const fmt = (n: number) => n.toFixed(1);
  return (
    <div className="stat-card-grid tenant-section-gap">
      <div className="stat-card"><p className="customer-label">{isEnglish ? 'Images — max/min' : 'Imágenes — máx/mín'}</p><h3>{imageStats.max} / {imageStats.min}</h3></div>
      <div className="stat-card"><p className="customer-label">{isEnglish ? 'Images — average / std. dev' : 'Imágenes — promedio / desv. est.'}</p><h3>{fmt(imageStats.average)} / {fmt(imageStats.stdDev)}</h3></div>
      <div className="stat-card"><p className="customer-label">{isEnglish ? 'Videos — max/min' : 'Videos — máx/mín'}</p><h3>{videoStats.max} / {videoStats.min}</h3></div>
      <div className="stat-card"><p className="customer-label">{isEnglish ? 'Videos — average / std. dev' : 'Videos — promedio / desv. est.'}</p><h3>{fmt(videoStats.average)} / {fmt(videoStats.stdDev)}</h3></div>
    </div>
  );
}

function MediaHistogram({ courses, isEnglish }: { courses: TenantCourseMediaBreakdown[]; isEnglish: boolean }) {
  const maxTotal = Math.max(...courses.map((c) => c.imagesCount + c.videosCount), 1);
  return (
    <div className="media-histogram">
      {courses.map((course) => (
        <div className="media-histogram-row" key={course.courseId}>
          <p className="media-histogram-label" title={course.name}>{course.name}</p>
          <div className="media-histogram-bars">
            <div className="media-histogram-bar media-histogram-bar-images" style={{ width: `${(course.imagesCount / maxTotal) * 100}%` }} />
            <div className="media-histogram-bar media-histogram-bar-videos" style={{ width: `${(course.videosCount / maxTotal) * 100}%` }} />
          </div>
          <p className="media-histogram-counts">
            <ImageIcon size={12} /> {course.imagesCount} &nbsp; <Video size={12} /> {course.videosCount}
          </p>
        </div>
      ))}
      <div className="media-histogram-legend">
        <span><span className="legend-swatch legend-swatch-images" /> {isEnglish ? 'Images' : 'Imágenes'}</span>
        <span><span className="legend-swatch legend-swatch-videos" /> {isEnglish ? 'Videos' : 'Videos'}</span>
      </div>
    </div>
  );
}

type CostStep = {
  stepName: string;
  agents: string;
  models: string;
  inputTokens: number;
  outputTokens: number;
  cachedInputTokens: number;
  totalTokens: number;
  totalElapsedMilliseconds: number;
  callCount: number;
  imagesGeneratedCount: number;
  imageCostUsd: number;
  videosGeneratedCount: number;
  videoCostUsd: number;
  estimatedCostUsd: number;
};

type CostDetail = {
  name: string;
  steps: CostStep[];
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  totalElapsedMilliseconds: number;
  imagesGeneratedCount: number;
  videosGeneratedCount: number;
  estimatedCostUsd: number;
  isEstimate: boolean;
};

type CourseSummary = { engramsCount: number; chaptersCount: number; factsCount: number } | null;

type EngramStepSummary = {
  stepType: string;
  sessionsCount: number;
  totalDurationSeconds: number;
  averageDurationSeconds: number;
  inputTokens: number;
  outputTokens: number;
  modelNames: string;
  estimatedCostUsd: number;
};

type GlobalEngramStat = {
  sectionId: string;
  chapterTitle: string;
  subchapterTitle: string;
  totalSessionsVisited: number;
  totalTimeSeconds: number;
  averageTimeSecondsPerVisit: number;
  recallAttempts: number;
  recallMastered: number;
  assessmentRounds: number;
  assessmentPassed: number;
  inputTokens: number;
  outputTokens: number;
  estimatedCostUsd: number;
  stepSummaries: EngramStepSummary[];
};

function formatSeconds(ms: number): string {
  const totalSeconds = Math.round(ms / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return minutes > 0 ? `${minutes}m ${seconds}s` : `${seconds}s`;
}

function formatNumber(n: number): string {
  return n.toLocaleString();
}

function formatUsd(n: number): string {
  return `$${n.toFixed(4)}`;
}

function formatUsdPrecise(n: number): string {
  return `$${n.toFixed(8)}`;
}

function CourseDetailPage({ isEnglish }: { isEnglish: boolean }) {
  const { tenantId, courseType, courseId } = useParams<{ tenantId: string; courseType: string; courseId: string }>();
  const navigate = useNavigate();
  const [pageTab, setPageTab] = useState<'overview' | 'dashboard'>('overview');
  const [cost, setCost] = useState<CostDetail | null>(null);
  const [costError, setCostError] = useState<string | null>(null);
  const [summary, setSummary] = useState<CourseSummary>(null);
  const [engrams, setEngrams] = useState<GlobalEngramStat[]>([]);
  const [engramsError, setEngramsError] = useState<string | null>(null);

  useEffect(() => {
    if (!courseId || !courseType) return;
    const endpoint = courseType === 'StructuredLearning'
      ? `/api/studio/structured-learning-costs/${courseId}`
      : `/api/studio/capability-costs/${courseId}`;

    fetch(endpoint)
      .then((response) => {
        if (!response.ok) throw new Error('Unable to load cost detail.');
        return response.json() as Promise<Record<string, unknown>>;
      })
      .then((row) => {
        const rawSteps = (row.Steps ?? row.steps ?? row.Sections ?? row.sections ?? []) as Record<string, unknown>[];
        const steps: CostStep[] = rawSteps.map((s) => ({
          stepName: String(s.StepName ?? s.stepName ?? s.SectionLabel ?? s.sectionLabel ?? ''),
          agents: String(s.Agents ?? s.agents ?? ''),
          models: String(s.Models ?? s.models ?? ''),
          inputTokens: Number(s.InputTokens ?? s.inputTokens ?? 0),
          outputTokens: Number(s.OutputTokens ?? s.outputTokens ?? 0),
          cachedInputTokens: Number(s.CachedInputTokens ?? s.cachedInputTokens ?? 0),
          totalTokens: Number(s.TotalTokens ?? s.totalTokens ?? 0),
          totalElapsedMilliseconds: Number(s.TotalElapsedMilliseconds ?? s.totalElapsedMilliseconds ?? 0),
          callCount: Number(s.CallCount ?? s.callCount ?? 0),
          imagesGeneratedCount: Number(s.ImagesGeneratedCount ?? s.imagesGeneratedCount ?? 0),
          imageCostUsd: Number(s.ImageCostUsd ?? s.imageCostUsd ?? 0),
          videosGeneratedCount: Number(s.VideosGeneratedCount ?? s.videosGeneratedCount ?? 0),
          videoCostUsd: Number(s.VideoCostUsd ?? s.videoCostUsd ?? 0),
          estimatedCostUsd: Number(s.EstimatedCostUsd ?? s.estimatedCostUsd ?? 0),
        }));
        setCost({
          name: String(row.DocumentName ?? row.documentName ?? row.CapabilityName ?? row.capabilityName ?? ''),
          steps,
          inputTokens: Number(row.InputTokens ?? row.inputTokens ?? 0),
          outputTokens: Number(row.OutputTokens ?? row.outputTokens ?? 0),
          totalTokens: Number(row.TotalTokens ?? row.totalTokens ?? 0),
          totalElapsedMilliseconds: Number(row.TotalElapsedMilliseconds ?? row.totalElapsedMilliseconds ?? 0),
          imagesGeneratedCount: Number(row.ImagesGeneratedCount ?? row.imagesGeneratedCount ?? 0),
          videosGeneratedCount: Number(row.VideosGeneratedCount ?? row.videosGeneratedCount ?? 0),
          estimatedCostUsd: Number(row.EstimatedCostUsd ?? row.estimatedCostUsd ?? 0),
          isEstimate: Boolean(row.IsEstimate ?? row.isEstimate ?? true),
        });
      })
      .catch((err: Error) => setCostError(err.message));

    if (courseType === 'StructuredLearning') {
      fetch(`/api/studio/structured-learning/${courseId}/summary`)
        .then((response) => {
          if (!response.ok) throw new Error('Unable to load course summary.');
          return response.json() as Promise<Record<string, unknown>>;
        })
        .then((row) => setSummary({
          engramsCount: Number(row.EngramsCount ?? row.engramsCount ?? 0),
          chaptersCount: Number(row.ChaptersCount ?? row.chaptersCount ?? 0),
          factsCount: Number(row.FactsCount ?? row.factsCount ?? 0),
        }))
        .catch(() => setSummary(null));

      fetch(`/api/studio/structured-learning/${courseId}`)
        .then((response) => {
          if (!response.ok) throw new Error('Unable to load course engrams.');
          return response.json() as Promise<Record<string, unknown>>;
        })
        .then(async (document) => {
          const sections = (document.Sections ?? document.sections ?? []) as Record<string, unknown>[];
          const rows = await Promise.all(sections.map(async (section) => {
            const sectionId = String(section.StructuredLearningDocumentSectionId ?? section.structuredLearningDocumentSectionId);
            const statsResponse = await fetch(`/api/structured-learning/sections/${sectionId}/stats`);
            const stats = statsResponse.ok ? await statsResponse.json() as Record<string, unknown> : {};
            const recall = (stats.Recall ?? stats.recall ?? {}) as Record<string, unknown>;
            const assessment = (stats.Assessment ?? stats.assessment ?? {}) as Record<string, unknown>;
            const rawStepSummaries = (stats.StepSummaries ?? stats.stepSummaries ?? []) as Record<string, unknown>[];
            return {
              sectionId,
              chapterTitle: String(section.ChapterTitle ?? section.chapterTitle ?? ''),
              subchapterTitle: String(section.SubchapterTitle ?? section.subchapterTitle ?? ''),
              totalSessionsVisited: Number(stats.TotalSessionsVisited ?? stats.totalSessionsVisited ?? 0),
              totalTimeSeconds: Number(stats.TotalTimeSeconds ?? stats.totalTimeSeconds ?? 0),
              averageTimeSecondsPerVisit: Number(stats.AverageTimeSecondsPerVisit ?? stats.averageTimeSecondsPerVisit ?? 0),
              recallAttempts: Number(recall.AttemptsCount ?? recall.attemptsCount ?? 0),
              recallMastered: Number(recall.MasteredCount ?? recall.masteredCount ?? 0),
              assessmentRounds: Number(assessment.RoundsCount ?? assessment.roundsCount ?? 0),
              assessmentPassed: Number(assessment.PassedRounds ?? assessment.passedRounds ?? 0),
              inputTokens: Number(stats.TotalInputTokens ?? stats.totalInputTokens ?? 0),
              outputTokens: Number(stats.TotalOutputTokens ?? stats.totalOutputTokens ?? 0),
              estimatedCostUsd: Number(stats.EstimatedCostUsd ?? stats.estimatedCostUsd ?? 0),
              stepSummaries: rawStepSummaries.map((s) => ({
                stepType: String(s.StepType ?? s.stepType ?? ''),
                sessionsCount: Number(s.SessionsCount ?? s.sessionsCount ?? 0),
                totalDurationSeconds: Number(s.TotalDurationSeconds ?? s.totalDurationSeconds ?? 0),
                averageDurationSeconds: Number(s.AverageDurationSeconds ?? s.averageDurationSeconds ?? 0),
                inputTokens: Number(s.InputTokens ?? s.inputTokens ?? 0),
                outputTokens: Number(s.OutputTokens ?? s.outputTokens ?? 0),
                modelNames: String(s.ModelNames ?? s.modelNames ?? ''),
                estimatedCostUsd: Number(s.EstimatedCostUsd ?? s.estimatedCostUsd ?? 0),
              })),
            };
          }));
          setEngrams(rows);
        })
        .catch((err: Error) => setEngramsError(err.message));
    }
  }, [courseId, courseType]);

  const studioUrl = courseType === 'StructuredLearning'
    ? `http://localhost:3010/structured-learning/${courseId}/course?mode=edit`
    : `http://localhost:3010/capabilities/${courseId}`;

  return <>
    <div className="main-title-row">
      <div>
        <p className="eyebrow">{isEnglish ? 'Course detail' : 'Detalle del curso'}</p>
        <h2>{cost?.name ?? (isEnglish ? 'Loading...' : 'Cargando...')}</h2>
      </div>
      <button type="button" className="btn-ghost" onClick={() => navigate(`/tenants/${tenantId}`)}>
        {isEnglish ? '← Back to tenant' : '← Volver al tenant'}
      </button>
    </div>

    {courseType === 'StructuredLearning' ? <div className="tenant-tabs">
      <button type="button" className={`tenant-tab${pageTab === 'overview' ? ' active' : ''}`} onClick={() => setPageTab('overview')}>
        <BookOpenCheck size={16} /> {isEnglish ? 'Overview' : 'Vista general'}
      </button>
      <button type="button" className={`tenant-tab${pageTab === 'dashboard' ? ' active' : ''}`} onClick={() => setPageTab('dashboard')}>
        <BarChart3 size={16} /> {isEnglish ? 'Course dashboard' : 'Dashboard del curso'}
      </button>
    </div> : null}

    {pageTab === 'dashboard' && courseType === 'StructuredLearning' ? (
      <CourseRuntimeDashboard engrams={engrams} engramsError={engramsError} isEnglish={isEnglish} />
    ) : <>

    {costError ? <div className="data-message error">{costError}</div>
    : cost === null ? <div className="data-message">{isEnglish ? 'Loading cost data...' : 'Cargando datos de costo...'}</div>
    : <>
      <div className="cost-hero">
        <div className="cost-hero-stat"><Clock size={18} /><span>{formatSeconds(cost.totalElapsedMilliseconds)}</span></div>
        <div className="cost-hero-stat"><Coins size={18} /><span>{formatNumber(cost.totalTokens)} {isEnglish ? 'tokens' : 'tokens'}</span></div>
        <div className="cost-hero-stat"><ImageIcon size={18} /><span>{cost.imagesGeneratedCount} {isEnglish ? 'images' : 'imágenes'}</span></div>
        <div className="cost-hero-stat"><Video size={18} /><span>{cost.videosGeneratedCount} {isEnglish ? 'videos' : 'videos'}</span></div>
        <div className="cost-hero-stat cost-hero-total"><DollarSign size={18} /><span>{formatUsd(cost.estimatedCostUsd)}{cost.isEstimate ? ` (${isEnglish ? 'est.' : 'estimado'})` : ''}</span></div>
      </div>

      {summary ? <div className="stat-card-grid tenant-section-gap">
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Total engrams' : 'Total de engramas'}</p><h3>{summary.engramsCount}</h3></div>
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Total chapters' : 'Total de capítulos'}</p><h3>{summary.chaptersCount}</h3></div>
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Total knowledge facts' : 'Total de facts'}</p><h3>{summary.factsCount}</h3></div>
      </div> : null}

      {courseType === 'StructuredLearning' ? <section className="engram-stat-section">
        <div className="section-heading-row">
          <div>
            <p className="eyebrow">{isEnglish ? 'Global course statistics' : 'Estadísticas globales del curso'}</p>
            <h3>Engrams</h3>
          </div>
          <span className="section-count">{engrams.length} {isEnglish ? 'engrams' : 'engrams'}</span>
        </div>
        {engramsError ? <div className="data-message error">{engramsError}</div>
        : engrams.length === 0 ? <div className="data-message">{isEnglish ? 'Loading engrams...' : 'Cargando engrams...'}</div>
        : <div className="engram-stat-grid">
          {engrams.map((engram, index) => <Link className="engram-stat-card" key={engram.sectionId} to={`/tenants/${tenantId}/courses/${courseType}/${courseId}/engrams/${engram.sectionId}`}>
            <div className="engram-stat-number">{String(index + 1).padStart(2, '0')}</div>
            <div className="engram-stat-content">
              <p className="engram-stat-chapter">{engram.chapterTitle || (isEnglish ? 'Chapter' : 'Capítulo')}</p>
              <h4>{engram.subchapterTitle || (isEnglish ? 'Untitled engram' : 'Engram sin título')}</h4>
              <div className="engram-stat-metrics">
                <span><strong>{engram.totalSessionsVisited}</strong> {isEnglish ? 'visits' : 'visitas'}</span>
                <span><strong>{formatSeconds(engram.totalTimeSeconds * 1000)}</strong> {isEnglish ? 'time' : 'tiempo'}</span>
                <span><strong>{engram.recallMastered}/{engram.recallAttempts}</strong> recall</span>
                <span><strong>{engram.assessmentPassed}/{engram.assessmentRounds}</strong> assessment</span>
              </div>
              <p className="engram-stat-tokens">{formatNumber(engram.inputTokens + engram.outputTokens)} {isEnglish ? 'global tokens' : 'tokens globales'}</p>
            </div>
          </Link>)}
        </div>}
      </section> : null}

      <div className="cost-table-wrap">
        <table className="cost-table">
          <thead>
            <tr>
              <th>{isEnglish ? 'Step' : 'Paso'}</th>
              <th>{isEnglish ? 'Model' : 'Modelo'}</th>
              <th>{isEnglish ? 'Time' : 'Tiempo'}</th>
              <th>{isEnglish ? 'Tokens in' : 'Tokens entrada'}</th>
              <th>{isEnglish ? 'Tokens out' : 'Tokens salida'}</th>
              <th>{isEnglish ? 'Images' : 'Imágenes'}</th>
              <th>{isEnglish ? 'Videos' : 'Videos'}</th>
              <th>{isEnglish ? 'Est. cost' : 'Costo est.'}</th>
            </tr>
          </thead>
          <tbody>
            {cost.steps.map((step, index) => (
              <tr key={`${step.stepName}-${index}`}>
                <td>{step.stepName}</td>
                <td>{step.models || step.agents}</td>
                <td>{formatSeconds(step.totalElapsedMilliseconds)}</td>
                <td>{formatNumber(step.inputTokens)}</td>
                <td>{formatNumber(step.outputTokens)}</td>
                <td>{step.imagesGeneratedCount || '—'}</td>
                <td>{step.videosGeneratedCount || '—'}</td>
                <td>{formatUsd(step.estimatedCostUsd)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="stat-card stat-card-wide tenant-section-gap">
        <p className="customer-label">{isEnglish ? 'Source' : 'Fuente'}</p>
        <a href={studioUrl} target="_blank" rel="noreferrer">HumanOS Studio ↗</a>
      </div>
    </>}
    </>}
  </>;
}

/** Bar as a simple CSS width percentage \u2014 no chart library dependency. */
function MiniBar({ label, value, max, color, valueLabel }: { label: string; value: number; max: number; color: string; valueLabel: string }) {
  const pct = max <= 0 ? 0 : Math.max(2, Math.round((value / max) * 100));
  return <div className="mini-bar-row">
    <span className="mini-bar-label">{label}</span>
    <div className="mini-bar-track"><div className="mini-bar-fill" style={{ width: `${pct}%`, background: color }} /></div>
    <span className="mini-bar-value">{valueLabel}</span>
  </div>;
}

const COURSE_STEP_ORDER = ['Hypothesis', 'Teaching', 'DeepDive', 'Production', 'Recall', 'Assessment'];
const COURSE_STEP_LABELS: Record<string, { en: string; es: string }> = {
  Hypothesis: { en: 'Before you start', es: 'Antes de iniciar' },
  Teaching: { en: 'Learn', es: 'Aprender' },
  DeepDive: { en: 'Deep dive', es: 'Profundización' },
  Production: { en: 'Your role', es: 'Tu puesto' },
  Recall: { en: 'Recall', es: 'Evocación' },
  Assessment: { en: 'Assessment', es: 'Evaluación' },
};

/** Course-wide runtime dashboard (2026-10-02) \u2014 sums every engram's own
 * StructuredLearningEngramStatsService stats across the whole course: total/
 * average time, total tokens in/out, and a per-step rollup across all
 * engrams, with simple CSS bar visualizations (no chart library needed). */
function CourseRuntimeDashboard({ engrams, engramsError, isEnglish }: { engrams: GlobalEngramStat[]; engramsError: string | null; isEnglish: boolean }) {
  if (engramsError) return <div className="data-message error">{engramsError}</div>;
  if (engrams.length === 0) return <div className="data-message">{isEnglish ? 'Loading course dashboard...' : 'Cargando dashboard del curso...'}</div>;

  const totalInputTokens = engrams.reduce((total, e) => total + e.inputTokens, 0);
  const totalOutputTokens = engrams.reduce((total, e) => total + e.outputTokens, 0);
  const totalTimeSeconds = engrams.reduce((total, e) => total + e.totalTimeSeconds, 0);
  const totalVisits = engrams.reduce((total, e) => total + e.totalSessionsVisited, 0);
  const averageTimePerVisit = totalVisits === 0 ? 0 : Math.round(totalTimeSeconds / totalVisits);
  const totalRecallAttempts = engrams.reduce((total, e) => total + e.recallAttempts, 0);
  const totalRecallMastered = engrams.reduce((total, e) => total + e.recallMastered, 0);
  const totalAssessmentRounds = engrams.reduce((total, e) => total + e.assessmentRounds, 0);
  const totalAssessmentPassed = engrams.reduce((total, e) => total + e.assessmentPassed, 0);

  const stepRollup = COURSE_STEP_ORDER.map((stepType) => {
    const matching = engrams.flatMap((e) => e.stepSummaries.filter((s) => s.stepType === stepType));
    const sessionsCount = matching.reduce((total, s) => total + s.sessionsCount, 0);
    const totalDurationSeconds = matching.reduce((total, s) => total + s.totalDurationSeconds, 0);
    return {
      stepType,
      sessionsCount,
      totalDurationSeconds,
      averageDurationSeconds: sessionsCount === 0 ? 0 : Math.round(totalDurationSeconds / sessionsCount),
      inputTokens: matching.reduce((total, s) => total + s.inputTokens, 0),
      outputTokens: matching.reduce((total, s) => total + s.outputTokens, 0),
      estimatedCostUsd: matching.reduce((total, s) => total + s.estimatedCostUsd, 0),
      modelNames: [...new Set(matching.flatMap((s) => s.modelNames.split(',').map((m) => m.trim()).filter(Boolean)))].join(', '),
    };
  });

  const totalEstimatedCostUsd = engrams.reduce((total, e) => total + e.estimatedCostUsd, 0);
  const maxStepTime = Math.max(1, ...stepRollup.map((s) => s.totalDurationSeconds));
  const maxStepTokens = Math.max(1, ...stepRollup.map((s) => s.inputTokens + s.outputTokens));
  const topEngramsByTokens = [...engrams].sort((a, b) => (b.inputTokens + b.outputTokens) - (a.inputTokens + a.outputTokens)).slice(0, 8);
  const maxEngramTokens = Math.max(1, ...topEngramsByTokens.map((e) => e.inputTokens + e.outputTokens));

  return <>
    <div className="cost-hero">
      <div className="cost-hero-stat"><Layers3 size={18} /><span>{engrams.length} {isEnglish ? 'engrams' : 'engrams'}</span></div>
      <div className="cost-hero-stat"><Clock size={18} /><span>{formatSeconds(totalTimeSeconds * 1000)} {isEnglish ? 'total' : 'total'}</span></div>
      <div className="cost-hero-stat"><Clock size={18} /><span>{formatSeconds(averageTimePerVisit * 1000)} {isEnglish ? 'avg / visit' : 'prom. / visita'}</span></div>
      <div className="cost-hero-stat"><Coins size={18} /><span>{formatNumber(totalInputTokens)} {isEnglish ? 'tokens in' : 'tokens entrada'}</span></div>
      <div className="cost-hero-stat"><Coins size={18} /><span>{formatNumber(totalOutputTokens)} {isEnglish ? 'tokens out' : 'tokens salida'}</span></div>
      <div className="cost-hero-stat"><BarChart3 size={18} /><span>{formatNumber(totalInputTokens + totalOutputTokens)} {isEnglish ? 'total tokens' : 'tokens totales'}</span></div>
      <div className="cost-hero-stat cost-hero-total"><DollarSign size={18} /><span>{formatUsd(totalEstimatedCostUsd)} {isEnglish ? 'est. total' : 'costo est.'}</span></div>
    </div>

    <div className="stat-card-grid tenant-section-gap">
      <div className="stat-card"><p className="customer-label">{isEnglish ? 'Total visits (all engrams)' : 'Visitas totales (todos los engrams)'}</p><h3>{formatNumber(totalVisits)}</h3></div>
      <div className="stat-card"><p className="customer-label">Recall {isEnglish ? 'mastered' : 'logrado'}</p><h3>{totalRecallMastered} / {totalRecallAttempts}</h3></div>
      <div className="stat-card"><p className="customer-label">Assessment {isEnglish ? 'passed' : 'aprobado'}</p><h3>{totalAssessmentPassed} / {totalAssessmentRounds}</h3></div>
    </div>

    <div className="engram-step-section">
      <div className="section-heading-row"><div><p className="eyebrow">{isEnglish ? 'Course-wide' : 'Todo el curso'}</p><h3>{isEnglish ? 'Time per step (sum of all engrams)' : 'Tiempo por paso (suma de todos los engrams)'}</h3></div></div>
      <div className="mini-chart-card">
        {stepRollup.map((step) => <MiniBar
          key={step.stepType}
          label={isEnglish ? COURSE_STEP_LABELS[step.stepType]?.en ?? step.stepType : COURSE_STEP_LABELS[step.stepType]?.es ?? step.stepType}
          value={step.totalDurationSeconds}
          max={maxStepTime}
          color="linear-gradient(90deg, #315fc0, #6ea3ff)"
          valueLabel={`${formatSeconds(step.totalDurationSeconds * 1000)} · ${isEnglish ? 'avg' : 'prom.'} ${formatSeconds(step.averageDurationSeconds * 1000)} · ${step.sessionsCount} ${isEnglish ? (step.sessionsCount === 1 ? 'visit' : 'visits') : (step.sessionsCount === 1 ? 'visita' : 'visitas')}`}
        />)}
      </div>
    </div>

    <div className="engram-step-section">
      <div className="section-heading-row"><div><p className="eyebrow">{isEnglish ? 'Course-wide' : 'Todo el curso'}</p><h3>{isEnglish ? 'Tokens per step (in + out)' : 'Tokens por paso (entrada + salida)'}</h3></div></div>
      <div className="mini-chart-card">
        {stepRollup.map((step) => <MiniBar
          key={step.stepType}
          label={isEnglish ? COURSE_STEP_LABELS[step.stepType]?.en ?? step.stepType : COURSE_STEP_LABELS[step.stepType]?.es ?? step.stepType}
          value={step.inputTokens + step.outputTokens}
          max={maxStepTokens}
          color="linear-gradient(90deg, #b5651d, #f0a860)"
          valueLabel={`${formatNumber(step.inputTokens)} ${isEnglish ? 'in' : 'entrada'} / ${formatNumber(step.outputTokens)} ${isEnglish ? 'out' : 'salida'}`}
        />)}
      </div>
    </div>

    <div className="engram-step-section">
      <div className="section-heading-row"><div><p className="eyebrow">{isEnglish ? 'Course-wide' : 'Todo el curso'}</p><h3>{isEnglish ? 'Cost and model per step' : 'Costo y modelo por paso'}</h3></div></div>
      <div className="cost-table-wrap">
        <table className="cost-table">
          <thead>
            <tr>
              <th>{isEnglish ? 'Step' : 'Paso'}</th>
              <th>{isEnglish ? 'Model' : 'Modelo'}</th>
              <th>{isEnglish ? 'Tokens in' : 'Tokens entrada'}</th>
              <th>{isEnglish ? 'Tokens out' : 'Tokens salida'}</th>
              <th>{isEnglish ? 'Est. cost' : 'Costo est.'}</th>
            </tr>
          </thead>
          <tbody>
            {stepRollup.map((step) => <tr key={step.stepType}>
              <td>{isEnglish ? COURSE_STEP_LABELS[step.stepType]?.en ?? step.stepType : COURSE_STEP_LABELS[step.stepType]?.es ?? step.stepType}</td>
              <td>{step.modelNames || '—'}</td>
              <td>{formatNumber(step.inputTokens)}</td>
              <td>{formatNumber(step.outputTokens)}</td>
              <td>{formatUsd(step.estimatedCostUsd)}</td>
            </tr>)}
          </tbody>
        </table>
      </div>
    </div>

    <div className="engram-step-section">
      <div className="section-heading-row"><div><p className="eyebrow">{isEnglish ? 'Top engrams' : 'Engrams principales'}</p><h3>{isEnglish ? 'Highest token usage' : 'Mayor consumo de tokens'}</h3></div></div>
      <div className="mini-chart-card">
        {topEngramsByTokens.map((engram) => <MiniBar
          key={engram.sectionId}
          label={engram.subchapterTitle || (isEnglish ? 'Untitled engram' : 'Engram sin título')}
          value={engram.inputTokens + engram.outputTokens}
          max={maxEngramTokens}
          color="linear-gradient(90deg, #2e7d5b, #5fd3a1)"
          valueLabel={`${formatNumber(engram.inputTokens + engram.outputTokens)} tokens · ${formatUsd(engram.estimatedCostUsd)} · ${formatSeconds(engram.totalTimeSeconds * 1000)}`}
        />)}
      </div>
    </div>
  </>;
}

function EngramDetailPage({ isEnglish }: { isEnglish: boolean }) {
  const { tenantId, courseType, courseId, sectionId } = useParams<{ tenantId: string; courseType: string; courseId: string; sectionId: string }>();
  const navigate = useNavigate();
  const [stats, setStats] = useState<Record<string, unknown> | null>(null);
  const [section, setSection] = useState<Record<string, unknown> | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!sectionId || !courseId) return;
    Promise.all([
      fetch(`/api/structured-learning/sections/${sectionId}/stats`).then((response) => {
        if (!response.ok) throw new Error('Unable to load engram statistics.');
        return response.json() as Promise<Record<string, unknown>>;
      }),
      fetch(`/api/studio/structured-learning/${courseId}`).then((response) => response.json() as Promise<Record<string, unknown>>),
    ])
      .then(([engramStats, document]) => {
        const sections = (document.Sections ?? document.sections ?? []) as Record<string, unknown>[];
        setStats(engramStats);
        setSection(sections.find((item) => String(item.StructuredLearningDocumentSectionId ?? item.structuredLearningDocumentSectionId) === sectionId) ?? null);
      })
      .catch((err: Error) => setError(err.message));
  }, [courseId, sectionId]);

  const recall = (stats?.Recall ?? stats?.recall ?? {}) as Record<string, unknown>;
  const assessment = (stats?.Assessment ?? stats?.assessment ?? {}) as Record<string, unknown>;
  const stepSummaries = (stats?.StepSummaries ?? stats?.stepSummaries ?? []) as Record<string, unknown>[];
  const usageCostBreakdown = (stats?.UsageCostBreakdown ?? stats?.usageCostBreakdown ?? []) as Record<string, unknown>[];
  const totalInputTokens = Number(stats?.TotalInputTokens ?? stats?.totalInputTokens ?? 0);
  const totalOutputTokens = Number(stats?.TotalOutputTokens ?? stats?.totalOutputTokens ?? 0);
  const unassignedInputTokens = Number(stats?.UnassignedInputTokens ?? stats?.unassignedInputTokens ?? 0);
  const unassignedOutputTokens = Number(stats?.UnassignedOutputTokens ?? stats?.unassignedOutputTokens ?? 0);
  const averageTimeSecondsPerVisit = Number(stats?.AverageTimeSecondsPerVisit ?? stats?.averageTimeSecondsPerVisit ?? 0);
  const inputCostTotal = usageCostBreakdown.reduce((total, row) => total + Number(row.InputCostUsd ?? row.inputCostUsd ?? 0), 0);
  const outputCostTotal = usageCostBreakdown.reduce((total, row) => total + Number(row.OutputCostUsd ?? row.outputCostUsd ?? 0), 0);
  const breakdownCostTotal = usageCostBreakdown.reduce((total, row) => total + Number(row.TotalCostUsd ?? row.totalCostUsd ?? 0), 0);

  const STEP_LABELS: Record<string, { en: string; es: string; voiceText: boolean }> = {
    Hypothesis: { en: 'Hypothesis', es: 'Hipótesis', voiceText: false },
    Teaching: { en: 'Learn (Voice & Text)', es: 'Aprender (Voz y Texto)', voiceText: true },
    DeepDive: { en: 'Deep Dive (Voice & Text)', es: 'Profundización (Voz y Texto)', voiceText: true },
    Production: { en: 'Your Role', es: 'Tu Puesto', voiceText: false },
    Recall: { en: 'Recall', es: 'Evocación', voiceText: false },
    Assessment: { en: 'Assessment', es: 'Evaluación', voiceText: false },
  };

  return <>
    <div className="main-title-row">
      <div>
        <p className="eyebrow">{isEnglish ? 'Global engram statistics' : 'Estadísticas globales del engram'}</p>
        <h2>{String(section?.SubchapterTitle ?? section?.subchapterTitle ?? (isEnglish ? 'Engram' : 'Engram'))}</h2>
      </div>
      <button type="button" className="btn-ghost" onClick={() => navigate(`/tenants/${tenantId}/courses/${courseType}/${courseId}`)}>
        {isEnglish ? '← Back to course' : '← Volver al curso'}
      </button>
    </div>
    {error ? <div className="data-message error">{error}</div> : !stats ? <div className="data-message">{isEnglish ? 'Loading statistics...' : 'Cargando estadísticas...'}</div> : <>
      <div className="stat-card-grid tenant-section-gap">
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Total engram time (all users)' : 'Tiempo total del engram (todos)'}</p><h3>{formatSeconds(Number(stats.TotalTimeSeconds ?? stats.totalTimeSeconds ?? 0) * 1000)}</h3></div>
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Average time per visit' : 'Tiempo promedio por visita'}</p><h3>{formatSeconds(averageTimeSecondsPerVisit * 1000)}</h3></div>
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Global visits' : 'Visitas globales'}</p><h3>{Number(stats.TotalSessionsVisited ?? stats.totalSessionsVisited ?? 0)}</h3></div>
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Total tokens in' : 'Tokens totales de entrada'}</p><h3>{formatNumber(totalInputTokens)}</h3></div>
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Total tokens out' : 'Tokens totales de salida'}</p><h3>{formatNumber(totalOutputTokens)}</h3></div>
        <div className="stat-card"><p className="customer-label">{isEnglish ? 'Estimated cost' : 'Costo estimado'}</p><h3>{formatUsd(Number(stats.EstimatedCostUsd ?? stats.estimatedCostUsd ?? 0))}</h3></div>
      </div>
      {usageCostBreakdown.length > 0 && (
        <div className="engram-step-section">
          <div className="section-heading-row">
            <div>
              <p className="eyebrow">{isEnglish ? 'Estimated usage cost' : 'Costo estimado de uso'}</p>
              <h3>{isEnglish ? 'Cost by model and channel' : 'Costo por modelo y canal'}</h3>
            </div>
          </div>
          <div className="cost-table-wrap">
            <table className="cost-table">
              <thead>
                <tr>
                  <th>{isEnglish ? 'Model' : 'Modelo'}</th>
                  <th>{isEnglish ? 'Channel' : 'Canal'}</th>
                  <th>{isEnglish ? 'Usage records' : 'Registros de consumo'}</th>
                  <th>{isEnglish ? 'Input tokens' : 'Tokens entrada'}</th>
                  <th>{isEnglish ? 'Input rate / 1M' : 'Tarifa entrada / 1M'}</th>
                  <th>{isEnglish ? 'Input cost' : 'Costo entrada'}</th>
                  <th>{isEnglish ? 'Output tokens' : 'Tokens salida'}</th>
                  <th>{isEnglish ? 'Output rate / 1M' : 'Tarifa salida / 1M'}</th>
                  <th>{isEnglish ? 'Output cost' : 'Costo salida'}</th>
                  <th>{isEnglish ? 'Subtotal' : 'Subtotal'}</th>
                </tr>
              </thead>
              <tbody>
                {usageCostBreakdown.map((row, index) => (
                  <tr key={`${String(row.Channel ?? row.channel)}-${String(row.ModelName ?? row.modelName)}-${index}`}>
                    <td>{String(row.ModelName ?? row.modelName ?? '—')}</td>
                    <td>{String(row.Channel ?? row.channel ?? '—')}</td>
                    <td>{formatNumber(Number(row.CallCount ?? row.callCount ?? 0))}</td>
                    <td>{formatNumber(Number(row.InputTokens ?? row.inputTokens ?? 0))}</td>
                    <td>{formatUsd(Number(row.InputRatePerMillionTokens ?? row.inputRatePerMillionTokens ?? 0))}</td>
                    <td>{formatUsdPrecise(Number(row.InputCostUsd ?? row.inputCostUsd ?? 0))}</td>
                    <td>{formatNumber(Number(row.OutputTokens ?? row.outputTokens ?? 0))}</td>
                    <td>{formatUsd(Number(row.OutputRatePerMillionTokens ?? row.outputRatePerMillionTokens ?? 0))}</td>
                    <td>{formatUsdPrecise(Number(row.OutputCostUsd ?? row.outputCostUsd ?? 0))}</td>
                    <td><strong>{formatUsdPrecise(Number(row.TotalCostUsd ?? row.totalCostUsd ?? 0))}</strong></td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr>
                  <td colSpan={3}><strong>{isEnglish ? 'Sum before rounding' : 'Suma antes del redondeo'}</strong></td>
                  <td>{formatNumber(totalInputTokens)}</td>
                  <td>—</td>
                  <td><strong>{formatUsdPrecise(inputCostTotal)}</strong></td>
                  <td>{formatNumber(totalOutputTokens)}</td>
                  <td>—</td>
                  <td><strong>{formatUsdPrecise(outputCostTotal)}</strong></td>
                  <td><strong>{formatUsdPrecise(breakdownCostTotal)}</strong></td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>
      )}
      <div className="engram-step-section">
        <div className="section-heading-row"><div><p className="eyebrow">{isEnglish ? 'Step totals' : 'Totales por paso'}</p><h3>{isEnglish ? 'Six-step breakdown' : 'Desglose de los seis pasos'}</h3></div></div>
        {(unassignedInputTokens > 0 || unassignedOutputTokens > 0) && <p className="engram-step-metric-label">{isEnglish
          ? `Historical usage without an assigned step: ${formatNumber(unassignedInputTokens)} input / ${formatNumber(unassignedOutputTokens)} output tokens. Included in engram totals, not in individual steps.`
          : `Consumo histórico sin paso asignado: ${formatNumber(unassignedInputTokens)} entrada / ${formatNumber(unassignedOutputTokens)} salida. Incluido en los totales del engram, no en los pasos individuales.`}</p>}
        <div className="engram-step-grid engram-step-grid-6">
          {stepSummaries.map((step, index) => {
            const stepType = String(step.StepType ?? step.stepType ?? `Step ${index + 1}`);
            const meta = STEP_LABELS[stepType] ?? { en: stepType, es: stepType, voiceText: false };
            const totalSeconds = Number(step.TotalDurationSeconds ?? step.totalDurationSeconds ?? 0);
            const avgSeconds = Number(step.AverageDurationSeconds ?? step.averageDurationSeconds ?? 0);
            const inputTokens = Number(step.InputTokens ?? step.inputTokens ?? 0);
            const outputTokens = Number(step.OutputTokens ?? step.outputTokens ?? 0);
            const voiceIn = Number(step.VoiceInputTokens ?? step.voiceInputTokens ?? 0);
            const voiceOut = Number(step.VoiceOutputTokens ?? step.voiceOutputTokens ?? 0);
            const textIn = Number(step.TextInputTokens ?? step.textInputTokens ?? 0);
            const textOut = Number(step.TextOutputTokens ?? step.textOutputTokens ?? 0);
            const sessionsCount = Number(step.SessionsCount ?? step.sessionsCount ?? 0);
            const modelNames = String(step.ModelNames ?? step.modelNames ?? '');
            const estimatedCostUsd = Number(step.EstimatedCostUsd ?? step.estimatedCostUsd ?? 0);
            const hasEstimatedTiming = Boolean(step.HasEstimatedTiming ?? step.hasEstimatedTiming);
            const hasTiming = sessionsCount > 0;
            const hasUsage = inputTokens > 0 || outputTokens > 0;
            const unavailableTime = isEnglish ? 'Not recorded' : 'Sin registro';
            return <article className="engram-step-card engram-step-card-v2" key={`${stepType}-${index}`}>
              <p className="engram-stat-chapter">{isEnglish ? meta.en : meta.es}</p>
              <div className="engram-step-time-row">
                <div><span className="engram-step-metric-label">{!hasTiming
                  ? (isEnglish ? 'Step time' : 'Tiempo del paso')
                  : hasEstimatedTiming
                  ? (isEnglish ? 'Recorded time' : 'Tiempo registrado')
                  : (isEnglish ? 'Active time' : 'Tiempo activo')}</span><h4>{hasTiming ? formatSeconds(totalSeconds * 1000) : unavailableTime}</h4></div>
                <div><span className="engram-step-metric-label">{isEnglish ? 'Avg / visit' : 'Prom. / visita'}</span><h4>{hasTiming ? formatSeconds(avgSeconds * 1000) : unavailableTime}</h4></div>
              </div>
              {!hasTiming && <p className="engram-step-metric-label">{hasUsage
                ? (isEnglish
                  ? 'Activity was recorded, but step timing is missing. Token counts cannot determine time spent.'
                  : 'Hay actividad registrada, pero falta la captura de tiempo del paso. Los tokens no permiten calcular cuánto duró.')
                : (isEnglish ? 'No step timing has been recorded.' : 'No hay tiempo registrado para este paso.')}</p>}
              {hasEstimatedTiming && <p className="engram-step-metric-label">{isEnglish
                ? 'Includes historical estimates bounded by the next step. Past background time cannot be recovered.'
                : 'Incluye estimaciones históricas limitadas por el siguiente paso. No se puede recuperar el tiempo en segundo plano de visitas anteriores.'}</p>}
              <div className="engram-step-token-row">
                <div className="engram-token-pill engram-token-pill-in"><span>{isEnglish ? 'Model input tokens' : 'Tokens de entrada al modelo'}</span><strong>{formatNumber(inputTokens)}</strong></div>
                <div className="engram-token-pill engram-token-pill-out"><span>{isEnglish ? 'Model output tokens' : 'Tokens de salida del modelo'}</span><strong>{formatNumber(outputTokens)}</strong></div>
              </div>
              {meta.voiceText && (voiceIn > 0 || voiceOut > 0 || textIn > 0 || textOut > 0) && (
                <div className="engram-step-channel-breakdown">
                  <p>🎙️ {isEnglish ? 'Voice sessions (Realtime)' : 'Sesiones de voz (Realtime)'}: {formatNumber(voiceIn)} {isEnglish ? 'in' : 'entrada'} / {formatNumber(voiceOut)} {isEnglish ? 'out' : 'salida'}</p>
                  <p>💬 {isEnglish ? 'Text chat' : 'Chat de texto'}: {formatNumber(textIn)} {isEnglish ? 'in' : 'entrada'} / {formatNumber(textOut)} {isEnglish ? 'out' : 'salida'}</p>
                  <p>{isEnglish
                    ? 'Input includes instructions, lesson content, conversation history and student audio when present. Output is generated by the agent. Realtime totals do not separate audio, text or cached tokens.'
                    : 'La entrada incluye instrucciones, contenido de la lección, historial y audio del estudiante cuando lo hay. La salida la genera el agente. Los totales Realtime no separan audio, texto ni tokens en caché.'}</p>
                </div>
              )}
              <span>{!hasTiming && hasUsage
                ? (isEnglish ? 'Visits not recorded' : 'Visitas sin registro')
                : `${sessionsCount} ${isEnglish ? (sessionsCount === 1 ? 'visit' : 'visits') : (sessionsCount === 1 ? 'visita' : 'visitas')}`}</span>
              {(modelNames || estimatedCostUsd > 0) && (
                <div className="engram-step-cost-row">
                  <span className="engram-step-model-badge">{modelNames || (isEnglish ? 'No model yet' : 'Sin modelo aún')}</span>
                  <span className="engram-step-cost-value">{formatUsd(estimatedCostUsd)}</span>
                </div>
              )}
            </article>;
          })}
        </div>
      </div>
      <div className="stat-card-grid tenant-section-gap">
        <div className="stat-card"><p className="customer-label">Recall</p><h3>{Number(recall.MasteredCount ?? recall.masteredCount ?? 0)} / {Number(recall.AttemptsCount ?? recall.attemptsCount ?? 0)}</h3></div>
        <div className="stat-card"><p className="customer-label">Assessment</p><h3>{Number(assessment.PassedRounds ?? assessment.passedRounds ?? 0)} / {Number(assessment.RoundsCount ?? assessment.roundsCount ?? 0)}</h3></div>
      </div>
    </>}
  </>;
}

function Root() { return <BrowserRouter><AuthGate><App /></AuthGate></BrowserRouter>; }

export default Root;
