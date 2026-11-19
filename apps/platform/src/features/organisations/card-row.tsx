"use client";

import { type ReactNode, useId } from "react";

/** One row of the card (4.14): its name in a column, the value and its details after it, the row's buttons at the end. */
export function Row({
  label,
  actions,
  children,
}: {
  label: string;
  actions?: ReactNode;
  children: ReactNode;
}) {
  const labelId = useId();
  return (
    // biome-ignore lint/a11y/useSemanticElements: a fieldset would draw a frame the card does not have; the group names the row.
    <div
      role="group"
      aria-labelledby={labelId}
      className="flex flex-col gap-2 px-5 py-3 tablet:flex-row tablet:items-center tablet:gap-6"
    >
      <span
        id={labelId}
        className="text-small text-muted-foreground tablet:w-48 tablet:shrink-0"
      >
        {label}
      </span>
      <div className="flex min-w-0 flex-1 flex-col gap-0.5 text-body">
        {children}
      </div>
      {actions && <div className="flex shrink-0 gap-2">{actions}</div>}
    </div>
  );
}
