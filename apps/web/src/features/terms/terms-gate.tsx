"use client";

import {
  getGetMyTermsQueryOptions,
  MyTermsResponseAction,
} from "@repo/api-client";
import { useQuery } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { TermsAcceptance } from "./terms-acceptance";

/**
 * Every signed-in page passes the terms first (GetMyTerms, the API's terms flow, section 4). While the answer loads, or when it
 * fails, the page shows: the API's next read asks again, and the page's own reads start at once instead of after it. `acceptance`
 * puts the gate in place of the page until the person accepts; `notice` and `nothing` show the page, and Home shows the notice.
 */
export function TermsGate({ children }: { children: ReactNode }) {
  const terms = useQuery(getGetMyTermsQueryOptions());
  const version = terms.data?.version;
  if (terms.data?.action === MyTermsResponseAction.Acceptance && version)
    return <TermsAcceptance key={version.id} version={version} />;
  return children;
}
