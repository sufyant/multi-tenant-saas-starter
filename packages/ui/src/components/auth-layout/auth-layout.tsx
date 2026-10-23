import type { ReactNode } from "react";

export type AuthLayoutProps = {
  /** The product's name. Its first letter is the logo. */
  brand: string;
  /** The platform app's ink label, in place of the product's name. */
  platformLabel?: string;
  /** Who is signed in, at the end of the header: "Signed in as ali@northwind.io" and Sign out. */
  account?: ReactNode;
  /** The footer, such as the language before sign-in. */
  footer?: ReactNode;
  children: ReactNode;
};

/**
 * The frame of the screens around sign-in (the design system's boards): the logo and the product's name, or the Platform label, a centred
 * column of at most 400px, and an optional footer. It has no navigation: the person is not in the app yet.
 */
export function AuthLayout({
  brand,
  platformLabel,
  account,
  footer,
  children,
}: AuthLayoutProps) {
  return (
    <div className="flex min-h-dvh flex-col bg-background text-foreground">
      <header className="flex flex-wrap items-center justify-between gap-4 px-6 py-5">
        <div className="flex items-center gap-2.5 font-semibold">
          <span
            aria-hidden="true"
            className="inline-flex size-8 items-center justify-center rounded-lg bg-primary text-primary-foreground"
          >
            {brand.charAt(0)}
          </span>
          {platformLabel ? (
            <span className="rounded-sm bg-platform px-1.5 py-0.5 text-status text-platform-foreground">
              {platformLabel}
            </span>
          ) : (
            <span>{brand}</span>
          )}
        </div>
        {account && (
          <div className="flex flex-wrap items-center gap-4 text-small text-muted-foreground">
            {account}
          </div>
        )}
      </header>
      <main className="flex flex-1 items-center justify-center px-4 pt-6 pb-16">
        <div className="flex w-full max-w-[400px] flex-col gap-6">
          {children}
        </div>
      </main>
      {footer && (
        <footer className="flex flex-wrap items-center justify-between gap-4 px-6 pt-3 pb-4 text-small text-muted-foreground">
          {footer}
        </footer>
      )}
    </div>
  );
}

/** The card that holds a step's controls. */
export function AuthCard({ children }: { children: ReactNode }) {
  return (
    <div className="flex flex-col gap-3 rounded-xl border border-border bg-card p-6 shadow-xs">
      {children}
    </div>
  );
}
