import { useAuth } from "@/auth/AuthContext";
import { useTranslation } from "react-i18next";
import { LanguageSwitcher } from "@/components/layout/LanguageSwitcher";
import Hero from "../components/Hero";
import ProblemSection from "../components/ProblemSection";
import MemoryParadox from "../components/MemoryParadox";
import GraphLearning from "../components/GraphLearning";
import CapabilityGrowth from "../components/CapabilityGrowth";
import GrowthLoop from "../components/GrowthLoop";
import Layers from "../components/Layers";
import ForIndividuals from "../components/ForIndividuals";
import ForOrganizations from "../components/ForOrganizations";
import Vision from "../components/Vision";
import CTA from "../components/CTA";
import Footer from "../components/Footer";

export default function LandingPage() {
  const { isAuthenticated, isLoading, user, login, logout } = useAuth();
  const { t } = useTranslation();

  return (
    <main className="bg-slate-50 text-slate-900">
      <div className="pointer-events-none fixed inset-x-0 top-0 z-50 flex items-center justify-between p-4 sm:p-6">
        <div className="pointer-events-auto flex items-center gap-2">
          <img src="/LogoEngram.png" alt="Engram Academy" className="h-24 w-24 rounded-full sm:h-36 sm:w-36" />
        </div>
        <div className="pointer-events-auto flex items-center gap-3">
          <div className="rounded-full bg-white/90 backdrop-blur shadow-sm">
            <LanguageSwitcher />
          </div>
          {isAuthenticated ? (
            <>
              <a
                href="/today"
                className="inline-flex min-h-11 items-center justify-center rounded-full bg-white px-5 py-2.5 text-sm font-medium text-slate-900 shadow-sm transition hover:bg-slate-100"
              >
                {user?.onboarded ? t("landing.topBar.goToApp") : t("landing.topBar.continueOnboarding")}
              </a>
              <button
                type="button"
                onClick={() => void logout()}
                disabled={isLoading}
                className="inline-flex min-h-11 items-center justify-center rounded-full border border-white/20 bg-white/10 px-5 py-2.5 text-sm font-medium text-white backdrop-blur transition hover:bg-white/20 disabled:opacity-50"
              >
                {t("landing.topBar.signOut")}
              </button>
            </>
          ) : (
            <button
              type="button"
              onClick={() => void login()}
              disabled={isLoading}
              className="inline-flex min-h-11 items-center justify-center rounded-full bg-white px-5 py-2.5 text-sm font-medium text-slate-900 shadow-sm transition hover:bg-slate-100 disabled:opacity-50"
            >
              {t("landing.topBar.signIn")}
            </button>
          )}
        </div>
      </div>

      <Hero />

      <ProblemSection />

      <MemoryParadox />

      <GraphLearning />

      <CapabilityGrowth />

      <GrowthLoop />

      <Layers />

      <ForIndividuals />

      <ForOrganizations />

      <Vision />

      <CTA />

      <Footer />
    </main>
  );
}
