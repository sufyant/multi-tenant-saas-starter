import { EditVersionPage } from "@/features/terms/version-form";

export default async function PlatformEditTermsVersionPage({
  params,
}: PageProps<"/settings/terms/[versionId]/edit">) {
  const { versionId } = await params;
  return <EditVersionPage versionId={versionId} />;
}
