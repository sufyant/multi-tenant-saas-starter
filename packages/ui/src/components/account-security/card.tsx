import { type ReactNode, useId } from "react";

/** A card of Account and security: its title and text, then its rows. `lead` is a first sentence that a phone leaves out (6.07). */
export function Card({
  title,
  lead,
  text,
  children,
}: {
  title: string;
  lead?: string;
  text: string;
  children: ReactNode;
}) {
  const id = useId();
  return (
    <section
      aria-labelledby={id}
      className="flex flex-col rounded-lg border border-border bg-card"
    >
      <div className="flex flex-col gap-1 px-5 pt-4 pb-3">
        <h2 id={id} className="text-section-title">
          {title}
        </h2>
        <p className="text-small text-muted-foreground">
          {lead && <span className="hidden tablet:inline">{lead} </span>}
          {text}
        </p>
      </div>
      {children}
    </section>
  );
}

/** A row of a card: its words, and its actions at the end, under them on a phone. */
export function Row({
  children,
  action,
  as: Tag = "div",
}: {
  children: ReactNode;
  action?: ReactNode;
  as?: "div" | "li";
}) {
  return (
    <Tag className="flex flex-col gap-3 border-t border-border px-5 py-3 tablet:flex-row tablet:items-center tablet:justify-between">
      <div className="min-w-0">{children}</div>
      {action && <div className="flex shrink-0 flex-wrap gap-2">{action}</div>}
    </Tag>
  );
}
