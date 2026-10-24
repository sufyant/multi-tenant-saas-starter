"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useRef, useState } from "react";

/** Where the person tried to go: a page of this app, or back in the browser's history. */
type Leaving = { href: string } | { back: true };

/**
 * Asks before the editor's changes are lost (the RoleUnsaved board). While `dirty`, a click on a link of this app is held, and so is
 * the browser's back: the hook adds one history entry of the same address, so back first returns to it, and the page asks. A
 * reload or a closed tab gets the browser's own question (`beforeunload`). `discard` goes on; `keep` stays, and back is held again.
 * Next.js has no event that blocks a navigation of the App Router, so the hook listens on the document (Next.js documentation,
 * `Link`, `onNavigate`, covers only a link of its own).
 */
export function useLeaveGuard(dirty: boolean) {
  const router = useRouter();
  const [leaving, setLeaving] = useState<Leaving | null>(null);
  const released = useRef(false);
  const guarded = useRef(false);

  const guardBack = useCallback(() => {
    window.history.pushState(window.history.state, "", window.location.href);
    guarded.current = true;
  }, []);

  useEffect(() => {
    if (!dirty) return;
    const onClick = (event: MouseEvent) => {
      if (
        released.current ||
        event.defaultPrevented ||
        event.button !== 0 ||
        event.metaKey ||
        event.ctrlKey ||
        event.shiftKey ||
        event.altKey
      )
        return;
      const anchor =
        event.target instanceof Element
          ? event.target.closest("a[href]")
          : null;
      if (
        !(anchor instanceof HTMLAnchorElement) ||
        anchor.target === "_blank" ||
        anchor.hasAttribute("download")
      )
        return;
      const url = new URL(anchor.href);
      if (url.origin !== window.location.origin) return;
      if (url.href === window.location.href) return;
      // The capture phase runs before React's handler on the root, so Next.js does not navigate.
      event.preventDefault();
      event.stopPropagation();
      setLeaving({ href: `${url.pathname}${url.search}${url.hash}` });
    };
    const onPopState = () => {
      if (released.current || !guarded.current) return;
      guarded.current = false;
      setLeaving({ back: true });
    };
    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      if (!released.current) event.preventDefault();
    };
    if (!guarded.current) guardBack();
    document.addEventListener("click", onClick, true);
    window.addEventListener("popstate", onPopState);
    window.addEventListener("beforeunload", onBeforeUnload);
    return () => {
      document.removeEventListener("click", onClick, true);
      window.removeEventListener("popstate", onPopState);
      window.removeEventListener("beforeunload", onBeforeUnload);
    };
  }, [dirty, guardBack]);

  const discard = () => {
    if (!leaving) return;
    released.current = true;
    setLeaving(null);
    if ("href" in leaving) router.push(leaving.href);
    else window.history.back();
  };
  const keep = () => {
    if (leaving && "back" in leaving) guardBack();
    setLeaving(null);
  };
  /** After a save or a delete the page leaves on purpose. */
  const release = useCallback(() => {
    released.current = true;
  }, []);

  return { leaving: leaving !== null, discard, keep, release };
}
