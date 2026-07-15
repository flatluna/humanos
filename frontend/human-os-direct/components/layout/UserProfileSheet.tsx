"use client";

import { LogOut, Settings } from "lucide-react";
import { useLanguage } from "@/lib/i18n/LanguageProvider";
import { mockUser } from "@/lib/mock-user";
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetFooter,
} from "@/components/ui/sheet";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Separator } from "@/components/ui/separator";
import { Button } from "@/components/ui/button";

const initials = mockUser.name
  .split(" ")
  .map((part) => part[0])
  .slice(0, 2)
  .join("")
  .toUpperCase();

export function UserProfileSheet({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const { t } = useLanguage();

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="right">
        <SheetHeader>
          <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            {mockUser.universityName}
          </span>
          <SheetTitle>{t.profile.title}</SheetTitle>
        </SheetHeader>

        <div className="flex flex-col gap-4 px-4">
          <div className="flex items-center gap-3">
            <Avatar size="lg">
              <AvatarFallback>{initials}</AvatarFallback>
            </Avatar>
            <div className="flex flex-col">
              <span className="text-sm font-semibold">{mockUser.name}</span>
              <span className="text-xs text-muted-foreground">
                {mockUser.universityName}
              </span>
            </div>
          </div>

          <Separator />

          <dl className="flex flex-col gap-3 text-sm">
            <div className="flex flex-col gap-0.5">
              <dt className="text-xs font-medium text-muted-foreground">
                {t.profile.email}
              </dt>
              <dd className="break-all">{mockUser.email}</dd>
            </div>
            <div className="flex flex-col gap-0.5">
              <dt className="text-xs font-medium text-muted-foreground">
                {t.profile.objectId}
              </dt>
              <dd className="break-all font-mono text-xs text-muted-foreground">
                {mockUser.oid}
              </dd>
            </div>
          </dl>
        </div>

        <SheetFooter>
          <Button variant="ghost" className="justify-start gap-2 rounded-2xl">
            <Settings className="size-4" aria-hidden />
            {t.profile.settings}
          </Button>
          <Button variant="ghost" className="justify-start gap-2 rounded-2xl text-destructive">
            <LogOut className="size-4" aria-hidden />
            {t.profile.signOut}
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

export function ProfileTriggerAvatar() {
  return (
    <Avatar size="sm">
      <AvatarFallback>{initials}</AvatarFallback>
    </Avatar>
  );
}
