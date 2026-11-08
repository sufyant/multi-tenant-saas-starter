import { permanentRedirect } from "next/navigation";
import { personalPages } from "@/shared/personal-pages";

// The page opens without an organisation (decision 69): an old link or bookmark under the slug leads there, as a moved resource.
export default function TenantPage(): never {
  permanentRedirect(personalPages.account);
}
