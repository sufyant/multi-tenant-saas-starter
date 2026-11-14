import { VersionPage } from "@/features/terms/version-page";

export default async function PlatformTermsVersionPage({
  params,
}: PageProps<"/settings/terms/[versionId]">) {
  const { versionId } = await params;
  return <VersionPage versionId={versionId} />;
}
