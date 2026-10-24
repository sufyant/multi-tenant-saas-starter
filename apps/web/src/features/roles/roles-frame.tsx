import type { ReactNode } from "react";
import { SettingsNav } from "@/shared/settings-nav";

/** The Settings frame of the three roles pages: the SubNav with Roles current, and the page. */
export function RolesFrame({ children }: { children: ReactNode }) {
  return (
    <div className="flex flex-col gap-6 tablet:flex-row">
      <SettingsNav current="roles" />
      <section className="flex min-w-0 flex-1 flex-col gap-4">
        {children}
      </section>
    </div>
  );
}
