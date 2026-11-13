"use client";

import { useClerk } from "@clerk/nextjs";
import { useMemo } from "react";

/** What the hook needs of the app's router. */
export type Router = {
  push: (href: string) => void;
  replace: (href: string) => void;
};

type Clerk = ReturnType<typeof useClerk>;

/** Resolves once clerk-js has loaded, or has failed to load, so a navigation never waits for ever. */
const settled = (clerk: Clerk) =>
  new Promise<void>((done) => {
    const handler = (status: string) => {
      if (status === "loading") return;
      clerk.off("status", handler);
      done();
    };
    clerk.on("status", handler, { notify: true });
  });

/**
 * The router for a navigation after a change, whose toast must reach the next page (F63). It first asks clerk-js for the session
 * token, after clerk-js has loaded: clerk-js answers the token it holds, or a new one when that one has expired or is about to, and
 * writes it to the session cookie. So the navigation carries a token the proxy accepts and stays a client-side navigation, which
 * keeps the toast. With an expired token the proxy answers 401, the page loads as a document, and the toast, which lives only in
 * memory, is lost. When clerk-js cannot load or the token cannot be read, the navigation still happens, as a document load. The
 * app passes its own router, Next.js's `useRouter`, as the shells pass their navigation, so this package does not depend on Next.js.
 */
export function useFreshRouter(router: Router) {
  const clerk = useClerk();
  return useMemo(() => {
    const fresh = async () => {
      await settled(clerk);
      try {
        await clerk.session?.getToken();
      } catch {
        // The proxy answers 401, and the page loads as a document (F63).
      }
    };
    return {
      push: async (href: string) => {
        await fresh();
        router.push(href);
      },
      replace: async (href: string) => {
        await fresh();
        router.replace(href);
      },
    };
  }, [router, clerk]);
}
