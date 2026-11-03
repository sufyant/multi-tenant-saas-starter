"use client";

import { brand } from "@repo/brand";
import { useTranslations } from "next-intl";
import { supportLink } from "../../lib/support-link";
import { Button } from "../button";
import { EmptyState } from "../empty-state";

export type AppErrorProps = {
  /**
   * section: a part of the page failed; the shell and the rest of the page stay, and the error shows in its place. start: the app
   * could not start at all, the only full-page error (the Section failed and App couldn't start screens).
   */
  kind: "section" | "start";
  onRetry: () => void;
  /** The error's digest, which support can look up. */
  code?: string;
  /** The image's version, for the body of "Contact support". */
  version?: string;
};

/** The error of a page that could not render (F27). */
export function AppError({ kind, onRetry, code, version }: AppErrorProps) {
  const t = useTranslations();
  if (kind === "section")
    return (
      <EmptyState
        kind="error"
        title={t("problems.errorTitle")}
        description={t("problems.sectionText")}
        code={code}
        action={<Button onClick={onRetry}>{t("common.tryAgain")}</Button>}
      />
    );
  return (
    <main className="mx-auto flex min-h-dvh max-w-xl items-center p-6">
      <EmptyState
        kind="error"
        title={t("problems.startTitle", { product: brand.name })}
        description={t("problems.startText")}
        code={code}
        action={
          <div className="flex flex-wrap justify-center gap-2">
            <Button variant="primary" onClick={onRetry}>
              {t("problems.reload")}
            </Button>
            <Button
              render={
                <a
                  href={supportLink(
                    brand.supportUrl,
                    version && t("appShell.supportBody", { version }),
                  )}
                />
              }
              nativeButton={false}
            >
              {t("problems.contactSupport")}
            </Button>
          </div>
        }
      />
    </main>
  );
}
