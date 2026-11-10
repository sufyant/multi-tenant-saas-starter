import { SystemAdminPage } from "@/features/system-admins/system-admin-page";

export default async function PlatformSystemAdminPage({
  params,
}: PageProps<"/settings/system-admins/[userId]">) {
  const { userId } = await params;
  return <SystemAdminPage userId={userId} />;
}
