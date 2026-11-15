"use client";

import { Banner } from "@repo/ui/components/banner";
import type { ReactNode } from "react";

/**
 * At and over a limit (E4), on the page where it applies: a warning, what to do, and the one action its page gives, or none (F56).
 * Only the limit's dialog offers support ("Request a plan change").
 */
export function PlanBanner({
  id,
  title,
  text,
  action,
}: {
  id: string;
  title: string;
  text: string;
  /** The banner's one action; null for none. */
  action: ReactNode;
}) {
  return (
    <Banner
      tone="warning"
      title={<span id={`${id}-title`}>{title}</span>}
      action={action}
    >
      <span id={`${id}-text`}>{text}</span>
    </Banner>
  );
}
