"use client";

import { brand } from "@repo/brand";
import { useAppFrame } from "@repo/ui/components/app-frame";
import { supportLink } from "@repo/ui/lib/support-link";
import { useTranslations } from "next-intl";
import type { ReactNode } from "react";

/**
 * "contact support" inside a sentence: the brand's support address with the app's version (the App version pattern), underlined,
 * so it is not told apart by colour alone (WCAG 2.2, 1.4.1). Leave's last Owner and Delete my account's last Owner end with it.
 */
export function SupportLink({ children }: { children: ReactNode }) {
  const t = useTranslations();
  const { version } = useAppFrame();
  return (
    <a
      href={supportLink(
        brand.supportUrl,
        version && t("appShell.supportBody", { version }),
      )}
      className="rounded-sm underline underline-offset-2 outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      {children}
    </a>
  );
}
