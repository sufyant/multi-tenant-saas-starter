"use client";

import { openPortalSession, problemOf } from "@repo/api-client";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useTenant } from "@/shared/tenant-context";

/** The page that Stripe's pages come back to: Billing, without a query (the API's `returnUrl` rule). */
export const billingAddress = (slug: string): string =>
  `${window.location.origin}/${slug}/settings/billing`;

/**
 * Opens Stripe's customer portal in a new tab (10.01: "Changes open Stripe in a new tab"), where the card, the address, the VAT ID and
 * the invoices change, and where an open invoice is paid (10.15 to 10.17). The tab opens at the click, before the API answers, so
 * the browser does not block it as a pop-up; it closes again when the API refuses, and a toast says why.
 */
export function usePortal() {
  const t = useTranslations();
  const failureText = useFailureText();
  const { tenant } = useTenant();
  const portal = useMutation({
    mutationFn: () =>
      openPortalSession(tenant.id, { returnUrl: billingAddress(tenant.slug) }),
  });
  return {
    pending: portal.isPending,
    open: () => {
      const tab = window.open("", "_blank");
      // Stripe's page gets no way back to this tab (OWASP, "HTML5 Security Cheat Sheet", tabnabbing).
      if (tab) tab.opener = null;
      portal.mutate(undefined, {
        onSuccess: ({ url }) => {
          if (tab) tab.location.href = url;
          else window.location.assign(url);
        },
        onError: (error) => {
          tab?.close();
          showToast({
            tone: "destructive",
            title: t("billing.portalFailed"),
            description: failureText(problemOf(error)),
          });
        },
      });
    },
  };
}
