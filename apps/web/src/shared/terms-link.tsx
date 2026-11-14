"use client";

import { getGetTermsInForceQueryKey, getTermsInForce } from "@repo/api-client";
import { brand } from "@repo/brand";
import { AuthFooter } from "@repo/ui/components/auth-layout";
import { queryOptions, useQuery } from "@tanstack/react-query";
import { setLocale } from "@/i18n/set-locale";

/**
 * GetTermsInForce as a query. Its 204 before the first version has no body, and a query may not hold undefined (TanStack Query,
 * "Query Functions"), so the generated function's answer becomes null there. The footer and the invitation share it.
 */
export const termsInForceQueryOptions = () =>
  queryOptions({
    queryKey: getGetTermsInForceQueryKey(),
    queryFn: async ({ signal }) => (await getTermsInForce({ signal })) ?? null,
  });

/**
 * The link of the terms version in force, from the public endpoint through the BFF, which sends it without a session (F6). Undefined
 * while it loads, when it fails, and before the first version (204): the footer then hides "Terms".
 */
export function useTermsUrl(): string | undefined {
  return useQuery(termsInForceQueryOptions()).data?.url;
}

/** The Auth footer of this app's screens before sign-in: the language, "Terms" of the version in force and "Privacy". */
export function TermsAuthFooter() {
  return (
    <AuthFooter
      onLanguageChange={setLocale}
      privacyUrl={brand.privacyUrl}
      termsUrl={useTermsUrl()}
    />
  );
}
