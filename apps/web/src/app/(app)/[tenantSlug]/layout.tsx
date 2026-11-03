import { ReservedSlug } from "@repo/api-client";
import { notFound } from "next/navigation";
import { PendingTenantShell, TenantShell } from "@/features/shell/tenant-shell";
import { TenantProvider } from "@/shared/tenant-context";

export default async function TenantLayout({
  children,
  params,
}: LayoutProps<"/[tenantSlug]">) {
  const { tenantSlug } = await params;
  // No organisation has a reserved slug (F58). Such an address is a top-level route's path that no page has, such as
  // /sign-in/missing, which Next.js gives to this segment: it is "Page not found" outside the shell.
  if (Object.values<string>(ReservedSlug).includes(tenantSlug)) notFound();
  return (
    <TenantProvider
      slug={tenantSlug}
      pending={<PendingTenantShell slug={tenantSlug} />}
    >
      <TenantShell>{children}</TenantShell>
    </TenantProvider>
  );
}
