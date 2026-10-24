import { OrganisationPage } from "@/features/organisations/organisation-page";

export default async function PlatformOrganisationPage({
  params,
}: PageProps<"/organisations/[tenantId]">) {
  const { tenantId } = await params;
  return <OrganisationPage tenantId={tenantId} />;
}
