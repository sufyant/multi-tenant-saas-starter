import type { ReactNode } from "react";

export type AuthScreenProps = {
  /** The product's name. Its first letter is the mark, and the mark's accessible name. */
  brand: string;
  /** The platform app's ink label, under the mark (F41). */
  platformLabel?: string;
  /** The footer at the end of the card: the language and the links before sign-in. */
  footer?: ReactNode;
  /** A line under the card, such as who is signed in and Sign out. */
  below?: ReactNode;
  children?: ReactNode;
};

/**
 * One screen before sign-in (the Entry frames): a centred card with the brand's mark, the step (its `AuthHeading` and controls) and
 * the footer. From bp-tablet the card sits on the sidebar fill; on a phone the card has no edge and fills the page.
 */
export function AuthScreen({
  brand,
  platformLabel,
  footer,
  below,
  children,
}: AuthScreenProps) {
  return (
    <main className="flex min-h-[calc(100dvh-var(--strip-height,0px))] flex-col items-center bg-background px-4 pt-20 pb-16 text-foreground tablet:justify-center tablet:bg-sidebar tablet:pt-16">
      <div className="flex w-full flex-col gap-4 tablet:max-w-auth-card-max tablet:rounded-xl tablet:border tablet:border-border tablet:bg-card tablet:p-8 tablet:shadow-md">
        <div className="flex flex-col items-center gap-2">
          <span
            role="img"
            aria-label={brand}
            className="flex size-10 items-center justify-center rounded-lg bg-linear-135 from-primary to-brand-gradient-end to-50% text-section-title text-primary-foreground"
          >
            <span aria-hidden="true">{brand.charAt(0)}</span>
          </span>
          {platformLabel && (
            <span className="rounded-sm bg-platform px-1.5 py-0.5 text-status text-platform-foreground">
              {platformLabel}
            </span>
          )}
        </div>
        {children}
        {footer}
      </div>
      {below && (
        <div className="mt-4 flex flex-wrap items-center justify-center gap-x-2 gap-y-1 text-small text-muted-foreground">
          {below}
        </div>
      )}
    </main>
  );
}

/** The title of a step and the sentence under it, centred in the card. */
export function AuthHeading({
  title,
  text,
}: {
  title: ReactNode;
  text?: ReactNode;
}) {
  return (
    <>
      <h1 className="text-center text-page-title">{title}</h1>
      {text && (
        <p className="text-center text-body text-muted-foreground">{text}</p>
      )}
    </>
  );
}
