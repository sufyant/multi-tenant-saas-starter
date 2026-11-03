"use client";

import { useEffect } from "react";

/**
 * The browser tab's title (the Browser tab screens): "(2) Members · Starter", with the environment's mark first outside production,
 * "[Staging] (2) Members · Starter". The count is the bell's unread count; at zero it leaves the title.
 */
export function useTabTitle(title: string | undefined) {
  useEffect(() => {
    if (title) document.title = title;
  }, [title]);
}
