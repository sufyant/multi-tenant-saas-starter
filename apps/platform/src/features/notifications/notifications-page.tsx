"use client";

import { NotificationPage } from "@repo/ui/components/notification-bell";
import { usePlatformBell } from "@/shared/notification-bell";

/** The page of notifications, which the bell opens on a phone (3.08): on Unread while something is unread, else on All. */
export function PlatformNotifications() {
  const { bell } = usePlatformBell(true, () => {});
  return <NotificationPage {...bell} />;
}
