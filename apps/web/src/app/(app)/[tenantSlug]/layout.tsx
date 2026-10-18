import { TenantShell } from "@/features/shell/tenant-shell";
import { TenantProvider } from "@/shared/tenant-context";

export default async function TenantLayout({
  children,
  params,
}: LayoutProps<"/[tenantSlug]">) {
  const { tenantSlug } = await params;
  return (
    <TenantProvider slug={tenantSlug}>
      <TenantShell>{children}</TenantShell>
    </TenantProvider>
  );
}
