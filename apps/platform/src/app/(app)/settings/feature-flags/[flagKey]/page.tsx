import { FlagPage } from "@/features/feature-flags/flag-page";

export default async function PlatformFeatureFlagPage({
  params,
}: PageProps<"/settings/feature-flags/[flagKey]">) {
  const { flagKey } = await params;
  return <FlagPage flagKey={decodeURIComponent(flagKey)} />;
}
