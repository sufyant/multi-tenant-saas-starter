import { PreferencesPage } from "@/features/preferences/preferences-page";

export default async function TenantPreferencesPage({
  params,
}: PageProps<"/[tenantSlug]/settings/preferences">) {
  const { tenantSlug } = await params;
  return <PreferencesPage settingsBase={`/${tenantSlug}/settings`} />;
}
