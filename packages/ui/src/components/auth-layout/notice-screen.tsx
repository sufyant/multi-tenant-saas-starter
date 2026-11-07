import type { ReactNode } from "react";

export type NoticeScreenProps = {
  title: ReactNode;
  text: ReactNode;
  /** The buttons under the text, side by side. */
  actions?: ReactNode;
  /** The top end of the page, outside the card: the bell and the account button. */
  corner?: ReactNode;
};

/**
 * A page that replaces the shell (frames 1.19, 2.13, 2.14): a card with the title, the sentence and its buttons, start aligned,
 * on the page's fill. It has no mark: the person is signed in, and the page says only why they cannot go on.
 */
export function NoticeScreen({
  title,
  text,
  actions,
  corner,
}: NoticeScreenProps) {
  return (
    <div className="flex min-h-[calc(100dvh-var(--strip-height,0px))] flex-col bg-background text-foreground">
      {corner && (
        <header className="flex items-center justify-end gap-2 px-4 pt-4 tablet:px-5 tablet:pt-5">
          {corner}
        </header>
      )}
      <main className="flex flex-1 items-center justify-center px-4 pt-6 pb-16">
        <div className="flex w-full max-w-notice-card-max flex-col gap-3 rounded-xl border border-border bg-card px-8 pt-8 pb-7 shadow-md">
          <h1 className="text-page-title">{title}</h1>
          <p className="text-body text-muted-foreground">{text}</p>
          {actions && (
            <div className="flex flex-wrap gap-2 pt-2 max-tablet:flex-col">
              {actions}
            </div>
          )}
        </div>
      </main>
    </div>
  );
}
