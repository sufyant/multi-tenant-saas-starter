import { AcceptInvitationPage } from "@/features/invitations/accept-invitation-page";

// The invitation's accept link (ControlPlane:Invitations:AcceptUrl). Clerk adds __clerk_ticket for a person without an account.
export default async function AcceptInvitation({
  searchParams,
}: PageProps<"/invitations/accept">) {
  const { code, __clerk_ticket: ticket } = await searchParams;
  return (
    <AcceptInvitationPage
      code={typeof code === "string" ? code : ""}
      ticket={typeof ticket === "string" ? ticket : undefined}
    />
  );
}
