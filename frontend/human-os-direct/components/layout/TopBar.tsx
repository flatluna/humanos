"use client";

import { useEffect, useState } from "react";
import { Bell, Menu, Moon, Search, Sun, Languages } from "lucide-react";
import { useTheme } from "next-themes";
import { useLanguage } from "@/lib/i18n/LanguageProvider";
import { UserProfileSheet, ProfileTriggerAvatar } from "@/components/layout/UserProfileSheet";

export function TopBar() {
  const { resolvedTheme, setTheme } = useTheme();
  const { locale, setLocale, t } = useLanguage();
  const [profileOpen, setProfileOpen] = useState(false);
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  return (
    <header className="sticky top-0 z-40 flex items-center gap-3 border-b border-border bg-background/80 px-4 py-3 backdrop-blur md:px-6">
      <div className="flex flex-1 justify-center">
        <label className="relative flex w-full max-w-md items-center">
          <Search
            className="pointer-events-none absolute left-3 size-4 text-muted-foreground"
            aria-hidden
          />
          <input
            type="search"
            placeholder={t.topbar.searchPlaceholder}
            aria-label={t.topbar.searchPlaceholder}
            className="w-full rounded-2xl border border-border bg-card py-2 pl-9 pr-3 text-sm shadow-subtle outline-none placeholder:text-muted-foreground focus-visible:ring-2 focus-visible:ring-ring"
          />
        </label>
      </div>

      <div className="flex items-center gap-1.5">
        <button
          type="button"
          onClick={() => setLocale(locale === "en" ? "es" : "en")}
          aria-label={t.common.language}
          title={t.common.language}
          className="flex items-center gap-1.5 rounded-2xl px-3 py-2 text-sm font-medium text-muted-foreground shadow-subtle transition-colors hover:bg-accent hover:text-accent-foreground"
        >
          <Languages className="size-4" aria-hidden />
          {locale.toUpperCase()}
        </button>

        <button
          type="button"
          aria-label={t.topbar.notifications}
          title={t.topbar.notifications}
          className="rounded-2xl p-2 text-muted-foreground shadow-subtle transition-colors hover:bg-accent hover:text-accent-foreground"
        >
          <Bell className="size-4" aria-hidden />
        </button>

        <button
          type="button"
          onClick={() => setTheme(resolvedTheme === "dark" ? "light" : "dark")}
          aria-label={t.topbar.toggleTheme}
          title={t.topbar.toggleTheme}
          className="rounded-2xl p-2 text-muted-foreground shadow-subtle transition-colors hover:bg-accent hover:text-accent-foreground"
        >
          {mounted && resolvedTheme === "dark" ? (
            <Sun className="size-4" aria-hidden />
          ) : (
            <Moon className="size-4" aria-hidden />
          )}
        </button>

        <button
          type="button"
          onClick={() => setProfileOpen(true)}
          aria-label={t.topbar.openProfile}
          title={t.topbar.openProfile}
          className="ml-1 flex items-center gap-2 rounded-2xl p-1.5 text-muted-foreground shadow-subtle transition-colors hover:bg-accent hover:text-accent-foreground"
        >
          <Menu className="size-4" aria-hidden />
          <ProfileTriggerAvatar />
        </button>
      </div>

      <UserProfileSheet open={profileOpen} onOpenChange={setProfileOpen} />
    </header>
  );
}
