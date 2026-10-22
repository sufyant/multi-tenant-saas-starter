import {
  AuditActorResponseKind,
  AuditOperation,
  AuditTargetResponseKind,
} from "@repo/api-client";
import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { messages } from "@/i18n/messages";
import { detailsOf, entry } from "../../../test/activity";
import { renderWithProviders } from "../../../test/render";
import { ActivitySentence } from "./activity-sentence";

/** The sentence of the operation in a message file, found by its path: `activity.operations.member.joined`. */
const sentenceIn = (language: keyof typeof messages, operation: string) =>
  `activity.operations.${operation}`
    .split(".")
    .reduce<unknown>(
      (node, key) => (node as Record<string, unknown> | undefined)?.[key],
      messages[language],
    );

const operations = Object.values(AuditOperation);

const shown = (text: string) =>
  screen.getByText((_, element) => element?.textContent === text, {
    selector: "p",
  });

describe("ActivitySentence", () => {
  // A new operation in the API's contract fails here until each language has its sentence.
  it.each(Object.keys(messages) as (keyof typeof messages)[])(
    "Messages_EveryAuditOperationIn%s_HasASentence",
    (language) => {
      expect(
        operations.filter(
          (operation) => typeof sentenceIn(language, operation) !== "string",
        ),
      ).toEqual([]);
    },
  );

  it.each(operations.map((operation) => [operation, detailsOf[operation]]))(
    "ActivitySentence_%s_RendersEveryPlaceholder",
    (operation, details) => {
      const { container } = renderWithProviders(
        <ActivitySentence entry={entry({ operation, details })} />,
      );

      expect(container.textContent).not.toMatch(/activity\.|[{}<>]/);
    },
  );

  it("ActivitySentence_MemberInvited_NamesNoEmailAddress", () => {
    renderWithProviders(
      <ActivitySentence
        entry={entry({
          operation: AuditOperation.MemberInvited,
          details: detailsOf[AuditOperation.MemberInvited],
          target: {
            kind: AuditTargetResponseKind.Invitation,
            id: "0198f0a2-0000-7000-8000-0000000000a1",
            name: null,
            deleted: null,
          },
        })}
      />,
    );

    expect(shown("Ayşe Yılmaz invited someone as Member.")).toBeInTheDocument();
  });

  it("ActivitySentence_InvitationRevoked_SaysAnInvitation", () => {
    renderWithProviders(
      <ActivitySentence
        entry={entry({ operation: AuditOperation.InvitationRevoked })}
      />,
    );

    expect(shown("Ayşe Yılmaz revoked an invitation.")).toBeInTheDocument();
  });

  it("ActivitySentence_MemberJoined_NamesTheTarget", () => {
    renderWithProviders(<ActivitySentence entry={entry()} />);

    expect(
      shown("Khalid Al-Harbi joined the organisation."),
    ).toBeInTheDocument();
  });

  it("ActivitySentence_RoleChanged_NamesTheOldAndTheNewRole", () => {
    renderWithProviders(
      <ActivitySentence
        entry={entry({
          operation: AuditOperation.MemberRoleChanged,
          details: detailsOf[AuditOperation.MemberRoleChanged],
        })}
      />,
    );

    expect(
      shown(
        "Ayşe Yılmaz changed the role of Khalid Al-Harbi from Member to Admin.",
      ),
    ).toBeInTheDocument();
  });

  it("ActivitySentence_TenantRenamed_NamesTheOldAndTheNewName", () => {
    renderWithProviders(
      <ActivitySentence
        entry={entry({
          operation: AuditOperation.TenantRenamed,
          details: detailsOf[AuditOperation.TenantRenamed],
        })}
      />,
    );

    expect(
      shown(
        "Ayşe Yılmaz renamed the organisation from Northwind to Northwind Traders.",
      ),
    ).toBeInTheDocument();
  });

  it("ActivitySentence_DeletedActor_SaysDeletedUser", () => {
    renderWithProviders(
      <ActivitySentence
        entry={entry({
          operation: AuditOperation.InvitationRevoked,
          actor: {
            kind: AuditActorResponseKind.User,
            userId: "0198f0a2-0000-7000-8000-000000000009",
            displayName: null,
            deleted: true,
          },
        })}
      />,
    );

    expect(shown("Deleted user revoked an invitation.")).toBeInTheDocument();
  });

  it("ActivitySentence_DeletedTarget_SaysDeletedUser", () => {
    renderWithProviders(
      <ActivitySentence
        entry={entry({
          target: {
            kind: AuditTargetResponseKind.User,
            id: "0198f0a2-0000-7000-8000-000000000009",
            name: null,
            deleted: true,
          },
        })}
      />,
    );

    expect(shown("Deleted user joined the organisation.")).toBeInTheDocument();
  });

  it("ActivitySentence_PlatformActor_ShowsTheBadgeAndNoPerson", () => {
    renderWithProviders(
      <ActivitySentence
        entry={entry({
          operation: AuditOperation.TenantReactivated,
          actor: {
            kind: AuditActorResponseKind.Platform,
            userId: null,
            displayName: null,
            deleted: null,
          },
        })}
      />,
    );

    expect(
      shown("Platform The platform reactivated this organisation."),
    ).toBeInTheDocument();
  });

  it("ActivitySentence_TenantSuspended_ShowsNoReason", () => {
    const { container } = renderWithProviders(
      <ActivitySentence
        entry={entry({
          operation: AuditOperation.TenantSuspended,
          details: detailsOf[AuditOperation.TenantSuspended],
        })}
      />,
    );

    expect(
      shown("Ayşe Yılmaz suspended this organisation."),
    ).toBeInTheDocument();
    expect(container).not.toHaveTextContent("Unpaid invoice");
  });

  it("ActivitySentence_InArabic_WritesTheArabicSentence", () => {
    renderWithProviders(
      <ActivitySentence
        entry={entry({ operation: AuditOperation.InvitationRevoked })}
      />,
      "ar",
    );

    expect(shown("Ayşe Yılmaz ألغى دعوة.")).toBeInTheDocument();
  });
});
