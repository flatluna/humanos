"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  Home,
  Orbit,
  Boxes,
  Briefcase,
  Target,
  Bot,
  TrendingUp,
  ChevronsLeft,
  ChevronsRight,
} from "lucide-react";
import { useLanguage } from "@/lib/i18n/LanguageProvider";
import { mockUser } from "@/lib/mock-user";
import { cn } from "@/lib/utils";
import { UserProfileSheet, ProfileTriggerAvatar } from "@/components/layout/UserProfileSheet";

const STORAGE_KEY = "human-os-direct.sidebar-collapsed";

const navItems = [
  { href: "/", labelKey: "today", icon: Home } as const,
  { href: "/goals", labelKey: "goals", icon: Target } as const,
  { href: "/capabilities", labelKey: "capabilities", icon: Boxes } as const,
  { href: "/portfolio", labelKey: "portfolio", icon: Briefcase } as const,
  { href: "/universe", labelKey: "universe", icon: Orbit } as const,
  { href: "/agents", labelKey: "agents", icon: Bot } as const,
  { href: "/evolution", labelKey: "evolution", icon: TrendingUp } as const,
];

export function Sidebar() {
  const pathname = usePathname();
  const { t } = useLanguage();
  const [collapsed, setCollapsed] = useState(false);
  const [profileOpen, setProfileOpen] = useState(false);

  useEffect(() => {
    const stored = window.localStorage.getItem(STORAGE_KEY);
    if (stored === "1") setCollapsed(true);
  }, []);

  const toggleCollapsed = () => {
    setCollapsed((prev) => {
      const next = !prev;
      window.localStorage.setItem(STORAGE_KEY, next ? "1" : "0");
      return next;
    });
  };

  return (
    <>
      <aside
        className={cn(
          "hidden md:flex md:flex-col border-r border-border bg-card transition-[width] duration-200",
          collapsed ? "md:w-[4.5rem]" : "md:w-64",
        )}
      >
        <div className="flex items-center justify-between gap-2 px-4 py-6">
          {!collapsed && (
            <span className="truncate text-lg font-semibold tracking-tight">
              ✨ {t.common.appName}
            </span>
          )}
          <button
            type="button"
            onClick={toggleCollapsed}
            aria-label={collapsed ? t.nav.expand : t.nav.collapse}
            title={collapsed ? t.nav.expand : t.nav.collapse}
            className={cn(
              "flex size-8 shrink-0 items-center justify-center rounded-2xl text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground",
              collapsed && "mx-auto",
            )}
          >
            {collapsed ? (
              <ChevronsRight className="size-4" aria-hidden />
            ) : (
              <ChevronsLeft className="size-4" aria-hidden />
            )}
          </button>
        </div>

        <nav className="flex-1 space-y-1 px-3">
          {navItems.map(({ href, labelKey, icon: Icon }) => {
            const isActive = href === "/" ? pathname === "/" : pathname.startsWith(href);
            const label = t.nav[labelKey];
            return (
              <Link
                key={href}
                href={href}
                title={collapsed ? label : undefined}
                className={cn(
                  "flex items-center gap-3 rounded-2xl px-3 py-2.5 text-sm font-medium transition-colors",
                  collapsed && "justify-center px-0",
                  isActive
                    ? "bg-accent text-accent-foreground shadow-subtle"
                    : "text-muted-foreground hover:bg-accent hover:text-accent-foreground",
                )}
              >
                <Icon className="size-[1.125rem] shrink-0" aria-hidden />
                {!collapsed && <span className="truncate">{label}</span>}
              </Link>
            );
          })}
        </nav>

        <div className="border-t border-border p-3">
          <button
            type="button"
            onClick={() => setProfileOpen(true)}
            className={cn(
              "flex w-full items-center gap-3 rounded-2xl px-2 py-2 text-left transition-colors hover:bg-accent",
              collapsed && "justify-center px-0",
            )}
          >
            <ProfileTriggerAvatar />
            {!collapsed && (
              <span className="truncate text-sm font-medium">
                {mockUser.name.split(" ")[0]} · {mockUser.universityName}
              </span>
            )}
          </button>
        </div>
      </aside>

      <UserProfileSheet open={profileOpen} onOpenChange={setProfileOpen} />
    </>
  );
}
