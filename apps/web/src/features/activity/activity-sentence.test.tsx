import {
  type AuditActorResponse,
  AuditActorResponseKind,
  AuditOperation,
  AuditTargetResponseKind,
  MemberRoleChangedDetailsResponseOperation,
  Permission,
  RoleUpdatedDetailsResponseOperation,
} from "@repo/api-client";
import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { messages } from "@/i18n/messages";
import {
  billingLead,
  detailsOf,
  entry,
  roleTarget,
} from "../../../test/activity";
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

/** The label of a permission in a message file, found by its path: `permissions.tenant.audit.read`. */
const labelIn = (language: keyof typeof messages, permission: string) =>
  `permissions.${permission}`
    .split(".")
    .reduce<unknown>(
      (node, key) => (node as Record<string, unknown> | undefined)?.[key],
      messages[language],
    );

const tenantPermissions = Object.values(Permission).filter((name) =>
  name.startsWith("tenant."),
);

const thePlatform: AuditActorResponse = {
  kind: AuditActorResponseKind.Platform,
  userId: null,
  displayName: null,
  deleted: null,
};

/** A `role.updated` entry of Billing lead: its old name, and the permissions added and removed. */
const roleUpdated = (
  oldName: string,
  permissionsAdded: string[],
  permissionsRemoved: string[],
) =>
  entry({
    operation: AuditOperation.RoleUpdated,
    target: roleTarget(),
    details: {
      operation: RoleUpdatedDetailsResponseOperation.RoleUpdated,
      oldName,
      newName: billingLead.name,
      permissionsAdded,
      permissionsRemoved,
    },
  });

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

  it.each(Object.keys(messages) as (keyof typeof messages)[])(
    "Messages_EveryTenantPermissionIn%s_HasALabel",
    (language) => {
      expect(
        tenantPermissions.filter(
          (permission) => typeof labelIn(language, permission) !== "string",
        ),
      ).toEqual([]);
    },
  );

  it("ActivitySentence_RoleCreated_NamesTheCustomRole", () => {
    renderWithProviders(
      <ActivitySentence
        entry={entry({
          operation: AuditOperation.RoleCreated,
          target: roleTarget(),
          details: detailsOf[AuditOperation.RoleCreated],
        })}
      />,
    );

    expect(
      shown("Ayşe Yılmaz created the role Billing lead."),
    ).toBeInTheDocument();
    expect(screen.getByText("Billing lead").tagName).toBe("BDI");
  });

  it("ActivitySentence_RoleDeleted_NamesTheRoleItHad", () => {
    renderWithProviders(
      <ActivitySentence
        entry={entry({
          operation: AuditOperation.RoleDeleted,
          target: roleTarget(),
        })}
      />,
    );

    expect(
      shown("Ayşe Yılmaz deleted the role Billing lead."),
    ).toBeInTheDocument();
  });

  it("ActivitySentence_RoleUpdated_NamesThePermissionsAddedAndRemoved", () => {
    renderWithProviders(
      <ActivitySentence
        entry={roleUpdated(
          billingLead.name,
          [Permission.TenantInvitationsCreate, Permission.TenantRolesRead],
          [Permission.TenantAuditRead],
        )}
      />,
    );

    expect(
      shown(
        "Ayşe Yılmaz changed the role Billing lead: added Invite people and See roles, removed See Activity.",
      ),
    ).toBeInTheDocument();
  });

  it("ActivitySentence_RoleUpdatedWithMoreThanThreeAdded_NamesThreeAndCountsTheRest", () => {
    renderWithProviders(
      <ActivitySentence
        entry={roleUpdated(
          billingLead.name,
          [
            Permission.TenantAuditRead,
            Permission.TenantInvitationsRead,
            Permission.TenantMembersUpdate,
            Permission.TenantRolesRead,
            Permission.TenantMembersRead,
          ],
          [],
        )}
      />,
    );

    expect(
      shown(
        "Ayşe Yılmaz changed the role Billing lead: added See Activity, See invitations, Change members’ roles and 2 more.",
      ),
    ).toBeInTheDocument();
  });

  it("ActivitySentence_RoleUpdatedWithOnlyRemoved_SaysRemoved", () => {
    renderWithProviders(
      <ActivitySentence
        entry={roleUpdated(billingLead.name, [], [Permission.TenantAuditRead])}
      />,
    );

    expect(
      shown("Ayşe Yılmaz changed the role Billing lead: removed See Activity."),
    ).toBeInTheDocument();
  });

  it("ActivitySentence_RoleRenamed_SaysTheOldAndTheNewName", () => {
    renderWithProviders(
      <ActivitySentence entry={roleUpdated("Billing", [], [])} />,
    );

    expect(
      shown("Ayşe Yılmaz renamed the role Billing to Billing lead."),
    ).toBeInTheDocument();
  });

  it("ActivitySentence_RoleRenamedWithPermissionChanges_SaysTheRenameFirst", () => {
    renderWithProviders(
      <ActivitySentence
        entry={roleUpdated("Billing", [Permission.TenantRolesRead], [])}
      />,
    );

    expect(
      shown(
        "Ayşe Yılmaz renamed the role Billing to Billing lead. Ayşe Yılmaz changed the role Billing lead: added See roles.",
      ),
    ).toBeInTheDocument();
  });

  it("ActivitySentence_UnknownPermission_ShowsItsRawName", () => {
    renderWithProviders(
      <ActivitySentence
        entry={roleUpdated(billingLead.name, ["tenant.reports.read"], [])}
      />,
    );

    expect(
      shown(
        "Ayşe Yılmaz changed the role Billing lead: added tenant.reports.read.",
      ),
    ).toBeInTheDocument();
  });

  it("ActivitySentence_RoleUpdatedInTurkish_WritesTheTurkishSentence", () => {
    renderWithProviders(
      <ActivitySentence
        entry={roleUpdated(
          billingLead.name,
          [Permission.TenantInvitationsCreate, Permission.TenantRolesRead],
          [Permission.TenantAuditRead],
        )}
      />,
      "tr",
    );

    expect(
      shown(
        "Ayşe Yılmaz, Billing lead rolünü değiştirdi: Kişileri davet edebilir ve Rolleri görebilir eklendi, Etkinliği görebilir kaldırıldı.",
      ),
    ).toBeInTheDocument();
  });

  it("ActivitySentence_RoleUpdatedInArabic_WritesTheArabicSentence", () => {
    renderWithProviders(
      <ActivitySentence
        entry={roleUpdated(
          billingLead.name,
          [Permission.TenantInvitationsCreate, Permission.TenantRolesRead],
          [Permission.TenantAuditRead],
        )}
      />,
      "ar",
    );

    expect(
      shown(
        "غيّر Ayşe Yılmaz الدور Billing lead: أضاف دعوة الأشخاص وعرض الأدوار، وأزال عرض النشاط.",
      ),
    ).toBeInTheDocument();
  });

  it("ActivitySentence_PlatformChangedARoleToACustomRole_ShowsTheBadgeAndTheRoles", () => {
    renderWithProviders(
      <ActivitySentence
        entry={entry({
          operation: AuditOperation.MemberRoleChanged,
          actor: thePlatform,
          details: {
            operation:
              MemberRoleChangedDetailsResponseOperation.MemberRoleChanged,
            oldRole: {
              id: "0198f0a2-0000-7000-8000-0000000000f3",
              name: "member",
              builtIn: true,
            },
            newRole: billingLead,
          },
        })}
      />,
    );

    expect(
      shown(
        "Platform The platform changed the role of Khalid Al-Harbi from Member to Billing lead.",
      ),
    ).toBeInTheDocument();
    expect(screen.getByText("Billing lead").tagName).toBe("BDI");
  });

  it("ActivitySentence_PlatformChangedARoleInArabic_UsesTheFeminineVerb", () => {
    renderWithProviders(
      <ActivitySentence
        entry={entry({
          operation: AuditOperation.MemberRoleChanged,
          actor: thePlatform,
          details: detailsOf[AuditOperation.MemberRoleChanged],
        })}
      />,
      "ar",
    );

    expect(
      shown("المنصة غيّرت المنصة دور Khalid Al-Harbi من عضو إلى مشرف."),
    ).toBeInTheDocument();
  });

  it("ActivitySentence_TenantPurgeWarned_NamesTheOrganisationAndNoDate", () => {
    renderWithProviders(
      <ActivitySentence
        entry={entry({
          operation: AuditOperation.TenantPurgeWarned,
          actor: thePlatform,
          target: {
            kind: AuditTargetResponseKind.Tenant,
            id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f",
            name: "Northwind Traders",
            deleted: null,
          },
        })}
      />,
    );

    expect(
      shown(
        "Platform The Owners were told that Northwind Traders will be deleted.",
      ),
    ).toBeInTheDocument();
  });

  it("ActivitySentence_TenantReopened_ShowsTheBadge", () => {
    renderWithProviders(
      <ActivitySentence
        entry={entry({
          operation: AuditOperation.TenantReopened,
          actor: thePlatform,
        })}
      />,
    );

    expect(
      shown("Platform The platform reopened this organisation."),
    ).toBeInTheDocument();
  });

  it("ActivitySentence_TenantReopenedInArabic_UsesTheFeminineVerbOfThePlatform", () => {
    renderWithProviders(
      <ActivitySentence
        entry={entry({
          operation: AuditOperation.TenantReopened,
          actor: thePlatform,
        })}
      />,
      "ar",
    );

    expect(shown("المنصة أعادت المنصة فتح هذه المؤسسة.")).toBeInTheDocument();
  });
});
