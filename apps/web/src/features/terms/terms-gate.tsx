"use client";

import {
  actionFor,
  getGetMyTermsQueryKey,
  getGetMyTermsQueryOptions,
  MyTermsResponseAction,
  ProblemDetailsError,
} from "@repo/api-client";
import { dismissToasts } from "@repo/ui/components/toast";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { type ReactNode, useEffect, useRef, useState } from "react";
import { TermsAcceptance } from "./terms-acceptance";

/**
 * Every signed-in page passes the terms first (GetMyTerms, the API's terms flow, section 4). While the answer loads, or when it
 * fails, the page shows: the API's next read asks again, and the page's own reads start at once instead of after it. `acceptance`
 * puts the gate in place of the page until the person accepts; `notice` and `nothing` show the page, and Home shows the notice.
 *
 * The API refuses every other call of a person who must accept (403 `terms_version.acceptance_required`, section 8). Such a refusal
 * of any read or command leads here, never to an error: the page goes, with its toasts, and GetMyTerms reads again, so the gate
 * shows the version to accept.
 */
export function TermsGate({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const terms = useQuery(getGetMyTermsQueryOptions());
  const [refusedAt, setRefusedAt] = useState<number>();
  const lastRefusal = useRef<number>(undefined);
  useEffect(() => {
    const refused = (error: unknown) => {
      if (!isTermsRefusal(error)) return;
      // When GetMyTerms failed after the last refusal, the terms cannot be read now: the page shows the refusal as any error, and
      // GetMyTerms' next read, such as on focus, asks again. So a page whose reads keep being refused never loops.
      const terms = queryClient.getQueryState(getGetMyTermsQueryKey());
      if (
        lastRefusal.current !== undefined &&
        (terms?.errorUpdatedAt ?? 0) >= lastRefusal.current
      )
        return;
      dismissToasts();
      lastRefusal.current = Date.now();
      setRefusedAt(lastRefusal.current);
      void queryClient.invalidateQueries({
        queryKey: getGetMyTermsQueryKey(),
      });
    };
    const reads = queryClient.getQueryCache().subscribe((event) => {
      if (event.type === "updated" && event.action.type === "error")
        refused(event.action.error);
    });
    const commands = queryClient.getMutationCache().subscribe((event) => {
      if (event.type === "updated" && event.action.type === "error")
        refused(event.action.error);
    });
    return () => {
      reads();
      commands();
    };
  }, [queryClient]);

  const version = terms.data?.version;
  if (terms.data?.action === MyTermsResponseAction.Acceptance && version)
    return <TermsAcceptance key={version.id} version={version} />;
  // After a refusal, the page waits for GetMyTerms' next answer, so it never shows the refused read's error. A failed answer shows the
  // page, as a failed first read does.
  const answeredSince = (at: number) =>
    terms.dataUpdatedAt >= at || terms.errorUpdatedAt >= at;
  if (refusedAt !== undefined && !answeredSince(refusedAt)) return null;
  return children;
}

const isTermsRefusal = (error: unknown) =>
  error instanceof ProblemDetailsError &&
  actionFor(error.status, error.problem).kind === "terms-acceptance";
