import { DeletedAccount } from "@/features/account/deleted-account";

// After the deletion the person is signed out, so the page is open in the proxy (F62).
export default function DeletedAccountPage() {
  return <DeletedAccount />;
}
