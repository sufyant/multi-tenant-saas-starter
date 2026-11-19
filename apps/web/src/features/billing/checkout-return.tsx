"use client";

import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { showToast } from "@repo/ui/components/toast";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { parseAsStringLiteral, useQueryState } from "nuqs";
import { useEffect } from "react";
import { useTenant } from "@/shared/tenant-context";

/** What each result that Stripe's page comes back with shows (the API's CheckoutSessionResponse): a toast or a banner. */
const shows = { success: "toast", cancelled: "banner" } as const;
const results = Object.keys(shows) as (keyof typeof shows)[];

/**
 * The Billing page after Stripe's checkout (10.11, 10.12): a payment shows a toast, and the plan follows when Stripe's event
 * arrives (B6); a payment that was not completed says nothing changed, with Try again to Change plan. The address loses its
 * query once the person saw the result.
 */
export function CheckoutReturn({ plan }: { plan: string | null }) {
  const t = useTranslations();
  const { tenant } = useTenant();
  const [result, setResult] = useQueryState(
    "checkout",
    parseAsStringLiteral(results).withOptions({ history: "replace" }),
  );
  const shown = result ? shows[result] : null;
  useEffect(() => {
    if (shown !== "toast") return;
    showToast({
      title: plan
        ? t("billing.checkout.paid", { plan })
        : t("billing.checkout.paidPending"),
    });
    void setResult(null);
  }, [shown, plan, setResult, t]);
  if (shown !== "banner") return null;
  return (
    <Banner
      tone="info"
      title={t("billing.checkout.notCompleted")}
      closeLabel={t("common.close")}
      onClose={() => void setResult(null)}
      action={
        <Button
          render={<Link href={`/${tenant.slug}/settings/billing/plans`} />}
          nativeButton={false}
        >
          {t("common.tryAgain")}
        </Button>
      }
    >
      {t("billing.checkout.notCompletedText")}
    </Banner>
  );
}
