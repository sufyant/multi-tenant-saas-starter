import type { ReactNode } from "react";

/** The frame of the three roles pages: the settings menu of the sidebar marks Roles. */
export function RolesFrame({ children }: { children: ReactNode }) {
  return <section className="flex min-w-0 flex-col gap-4">{children}</section>;
}
