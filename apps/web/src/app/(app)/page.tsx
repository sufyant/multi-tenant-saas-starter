import { cookies } from "next/headers";
import { ChooseOrganisation } from "@/features/organisations/choose-organisation";
import { lastOrganisationCookie } from "@/shared/last-organisation-cookie";

export default async function ChooseOrganisationPage() {
  // The cookie is HttpOnly, so the page reads it here and gives the slug to the list (frame 1.18).
  const lastUsed = (await cookies()).get(lastOrganisationCookie)?.value;
  return <ChooseOrganisation lastUsed={lastUsed} />;
}
