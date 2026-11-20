import { CreateOrganisation } from "@/features/organisations/create-organisation";

/** Create your organisation (1.28), at `/new`, a reserved slug, so no organisation's address takes it. */
export default function NewOrganisationPage() {
  return <CreateOrganisation />;
}
