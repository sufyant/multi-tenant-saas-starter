"use client";

import {
  apiUrl,
  bellKeysOf,
  bellOf,
  everyBellKey,
  getStreamNotificationsUrl,
} from "@repo/api-client";
import { useLiveSignal } from "@repo/ui/lib/use-live-signal";
import { type QueryKey, useQueryClient } from "@tanstack/react-query";

/**
 * The tab's one live stream of notifications (`apps/api/docs/flows/notifications.md`, section 4), through the BFF (F6). A signal
 * names a bell; its list and count are read again by their generated keys, and a personal signal reads every tenant bell too. When
 * the stream opens again, the tab gets focus or the browser comes back online, every open bell reads again. The signed-in layout
 * mounts it once; no component opens its own.
 */
export function NotificationStream() {
  const queryClient = useQueryClient();
  const readAgain = (keys: QueryKey[]) => {
    for (const queryKey of keys)
      void queryClient.invalidateQueries({ queryKey });
  };
  useLiveSignal({
    url: apiUrl(getStreamNotificationsUrl()),
    event: "notifications",
    onSignal: (data) => {
      const bell = bellOf(data);
      if (bell) readAgain(bellKeysOf(bell));
    },
    onResync: () => readAgain(everyBellKey),
  });
  return null;
}
