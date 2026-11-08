import type { ReactNode } from "react";

/** The card of a role's name (9.08, 9.10): the field at the frame's width of 360 px, inside a card of its own. */
export function NameCard({ children }: { children: ReactNode }) {
  return (
    <div className="rounded-lg border border-border bg-card p-5">
      <div className="max-w-90">{children}</div>
    </div>
  );
}
