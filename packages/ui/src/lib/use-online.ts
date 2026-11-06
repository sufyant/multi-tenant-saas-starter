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

/** Offline (F59): the app's calls got no answer on two tries (`serverReach`), and the browser says offline too. */
export function useOffline(serverDown: boolean, noAnswer: boolean): boolean {
  const online = useOnline();
  return serverDown && noAnswer && !online;
}
