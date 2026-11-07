import type { ReactNode } from "react";
import { PersonalShell } from "@/features/shell/personal-shell";

// Settings without an organisation (decision 69): the person's own pages, at the platform's paths. `settings` is a reserved slug
// (F58), so no organisation's address is under it.
export default function PersonalSettingsLayout({
  children,
}: {
  children: ReactNode;
}) {
  return <PersonalShell>{children}</PersonalShell>;
}
