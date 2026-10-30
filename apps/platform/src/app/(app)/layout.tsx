import { railCookieName, railExpandedFrom } from "@repo/ui/lib/rail-state";
import { cookies } from "next/headers";
import { PlatformShell } from "@/features/shell/platform-shell";
import { NotificationStream } from "@/shared/notification-stream";

export default async function PlatformLayout({ children }: LayoutProps<"/">) {
  // The rail's width comes from its cookie on the server, so the first paint is right (F49).
  const railExpanded = railExpandedFrom(
    (await cookies()).get(railCookieName)?.value,
  );
  // One live stream of notifications for the tab, across every page of the shell.
  return (
    <>
      <NotificationStream />
      <PlatformShell railExpanded={railExpanded}>{children}</PlatformShell>
    </>
  );
}
