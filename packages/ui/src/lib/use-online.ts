"use client";

import { useSyncExternalStore } from "react";

const subscribe = (changed: () => void) => {
  window.addEventListener("online", changed);
  window.addEventListener("offline", changed);
  return () => {
    window.removeEventListener("online", changed);
    window.removeEventListener("offline", changed);
  };
};

/** Whether the browser has a connection (`navigator.onLine`), only a hint (F59). The server renders online. */
export function useOnline(): boolean {
  return useSyncExternalStore(
    subscribe,
    () => navigator.onLine,
    () => true,
  );
}

/** Whether the app is offline, by the rule of F59, from the verdict of `serverReach`. */
export function useOffline(serverDown: boolean, noAnswer: boolean): boolean {
  const online = useOnline();
  return serverDown && noAnswer && !online;
}
