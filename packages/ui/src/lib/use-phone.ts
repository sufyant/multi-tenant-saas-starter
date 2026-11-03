"use client";

import { useSyncExternalStore } from "react";

// A media query takes no variable, so the breakpoint comes from its token at run time (F20). Without it, as in jsdom, the screen
// counts as wide.
const subscribe = (changed: () => void) => {
  window.addEventListener("resize", changed);
  return () => window.removeEventListener("resize", changed);
};
const isPhone = () => {
  const tablet = getComputedStyle(document.documentElement)
    .getPropertyValue("--bp-tablet")
    .trim();
  return tablet !== "" && !window.matchMedia(`(min-width: ${tablet})`).matches;
};

/** True under bp-tablet: the phone layout of the Screen sizes rules. The server renders the wide layout. */
export function usePhone(): boolean {
  return useSyncExternalStore(subscribe, isPhone, () => false);
}
