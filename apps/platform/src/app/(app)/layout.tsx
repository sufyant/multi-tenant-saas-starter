import { railCookieName, railExpandedFrom } from "@repo/ui/lib/rail-state";
import { cookies } from "next/headers";
import { PlatformShell } from "@/features/shell/platform-shell";

export default async function PlatformLayout({ children }: LayoutProps<"/">) {
  // The rail's width comes from its cookie on the server, so the first paint is right (F49).
  const railExpanded = railExpandedFrom(
    (await cookies()).get(railCookieName)?.value,
  );
  return <PlatformShell railExpanded={railExpanded}>{children}</PlatformShell>;
}
