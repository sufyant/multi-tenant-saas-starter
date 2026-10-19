import { railCookieName, railExpandedFrom } from "@repo/ui/lib/rail-state";
import { cookies } from "next/headers";
import { TenantShell } from "@/features/shell/tenant-shell";
import { TenantProvider } from "@/shared/tenant-context";

export default async function TenantLayout({
  children,
  params,
}: LayoutProps<"/[tenantSlug]">) {
  const { tenantSlug } = await params;
  // The rail's width comes from its cookie on the server, so the first paint is right (F49).
  const railExpanded = railExpandedFrom(
    (await cookies()).get(railCookieName)?.value,
  );
  return (
    <TenantProvider slug={tenantSlug}>
      <TenantShell railExpanded={railExpanded}>{children}</TenantShell>
    </TenantProvider>
  );
}
