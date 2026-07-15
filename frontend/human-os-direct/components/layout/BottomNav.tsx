"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Home, Orbit, Boxes, Bot } from "lucide-react";
import { useLanguage } from "@/lib/i18n/LanguageProvider";
import { cn } from "@/lib/utils";

const navItems = [
  { href: "/", labelKey: "today", icon: Home } as const,
  { href: "/universe", labelKey: "universe", icon: Orbit } as const,
  { href: "/capabilities", labelKey: "capabilities", icon: Boxes } as const,
  { href: "/agents", labelKey: "agents", icon: Bot } as const,
];

export function BottomNav() {
  const pathname = usePathname();
  const { t } = useLanguage();

  return (
    <nav
      className="fixed inset-x-0 bottom-0 z-40 flex items-stretch justify-around border-t border-border bg-background/95 backdrop-blur md:hidden"
      style={{ paddingBottom: "env(safe-area-inset-bottom)" }}
    >
      {navItems.map(({ href, labelKey, icon: Icon }) => {
        const isActive = href === "/" ? pathname === "/" : pathname.startsWith(href);
        const label = t.nav[labelKey];
        return (
          <Link
            key={href}
            href={href}
            className={cn(
              "flex flex-1 flex-col items-center gap-1 py-2.5 text-xs font-medium transition-colors",
              isActive ? "text-foreground" : "text-muted-foreground",
            )}
          >
            <Icon className="size-5" aria-hidden />
            <span className="truncate">{label}</span>
          </Link>
        );
      })}
    </nav>
  );
}
