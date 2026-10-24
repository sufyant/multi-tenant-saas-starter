import { UserPage } from "@/features/users/user-page";

export default async function PlatformUserPage({
  params,
}: PageProps<"/users/[userId]">) {
  const { userId } = await params;
  return <UserPage userId={userId} />;
}
