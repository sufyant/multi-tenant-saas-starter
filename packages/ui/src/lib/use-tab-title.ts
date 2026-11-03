"use client";

import { useEffect } from "react";

/**
 * The browser tab's title (the Browser tab screens): "(2) Members · Starter", with the environment's mark first outside production,
 * "[Staging] (2) Members · Starter". The count is the bell's unread count; at zero it leaves the title.
 *
 * Next.js streams the page's metadata after the first paint and writes its title again on each navigation (generateMetadata,
 * "Streaming metadata"), so the shell writes its title again whenever another write changes it.
 */
export function useTabTitle(title: string | undefined) {
  useEffect(() => {
    if (!title) return;
    const keep = () => {
      if (document.title !== title) document.title = title;
    };
    keep();
    const observer = new MutationObserver(keep);
    observer.observe(document.documentElement, {
      subtree: true,
      childList: true,
      characterData: true,
    });
    return () => observer.disconnect();
  }, [title]);
}
