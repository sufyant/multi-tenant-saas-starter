import { OrganisationShell } from "@/features/shell/tenant-shell";

export default async function TenantShellLayout({
  children,
  params,
}: LayoutProps<"/[tenantSlug]">) {
  const { tenantSlug } = await params;
  return <OrganisationShell slug={tenantSlug}>{children}</OrganisationShell>;
}
