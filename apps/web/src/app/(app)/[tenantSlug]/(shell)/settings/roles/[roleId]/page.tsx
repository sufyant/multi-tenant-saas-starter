import { RolePage } from "@/features/roles/role-page";

export default async function TenantRolePage({
  params,
}: PageProps<"/[tenantSlug]/settings/roles/[roleId]">) {
  const { roleId } = await params;
  return <RolePage roleId={roleId} />;
}
