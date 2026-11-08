"use client";

import {
  type InvitationResponse,
  InvitationResponseState,
  type InviteMemberRequest,
  inviteMember,
  ListInvitationsSort,
  Permission,
  problemOf,
} from "@repo/api-client";
import { Label } from "@repo/ui/components/label";
import { Pill, type PillTone } from "@repo/ui/components/pill";
import { RowMenu, type RowMenuItem } from "@repo/ui/components/row-menu";
import {
  sortFrom,
  sortOf,
  Table,
  type TableColumn,
} from "@repo/ui/components/table";
import { Time } from "@repo/ui/components/time";
import { showToast } from "@repo/ui/components/toast";
import { formatDate } from "@repo/ui/lib/format-date-time";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { afterRefusal, refreshInvitations } from "@/shared/refresh";
import { useCan, useTenant } from "@/shared/tenant-context";
import { useRoleName } from "@/shared/use-role-name";
import { type InvitationStatus, invitationStatus } from "./invitation-status";
import { nameOf } from "./person";
import { RevokeInvitationDialog } from "./revoke-invitation-dialog";
import { useRoleReach } from "./use-role-reach";

const sorts = {
  email: {
    asc: ListInvitationsSort.Email,
    desc: ListInvitationsSort.EmailDescending,
  },
  expires: {
    asc: ListInvitationsSort.ExpiresAt,
    desc: ListInvitationsSort.ExpiresAtDescending,
  },
};

const tones: Record<InvitationStatus, PillTone> = {
  invited: "info",
  expiresSoon: "warning",
  [InvitationResponseState.Expired]: "neutral",
};

/**
 * The Invitations tab's table (8.29): Email on one line, Role as a Label, Invited by, Expires as a date, Status as a Pill. The rows
 * are every page loaded so far (F9, F10). An expired invitation stays and can be cancelled (decision 3 of step 21c). "Invite again" is a new InviteMember with a new Idempotency-Key, which replaces the
 * pending one (K2, F48). The row menu follows F5 and F51; a disabled item says why.
 */
export function InvitationsTable({
  rows,
  sort,
  timeZone,
  onSortChange,
}: {
  rows: InvitationResponse[];
  sort: string | null;
  timeZone: string | null | undefined;
  onSortChange: (sort: string | null) => void;
}) {
  const t = useTranslations();
  const roleName = useRoleName();
  const can = useCan();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const { tenant } = useTenant();
  const reach = useRoleReach();
  const [revoking, setRevoking] = useState<InvitationResponse | null>(null);
  const now = new Date();
  const locale = t("locale.format");
  const canInvite = can(Permission.TenantInvitationsCreate);
  const canRevoke = can(Permission.TenantInvitationsDelete);

  const inviteAgain = useMutation({
    // The new email keeps the row's language, not the viewer's (K8).
    mutationFn: (request: InviteMemberRequest) =>
      inviteMember(tenant.id, request, {
        "Idempotency-Key": crypto.randomUUID(),
      }),
    onSuccess: () => refreshInvitations(queryClient, tenant.id),
    onError: (error) => {
      afterRefusal(queryClient, tenant.id, error);
      showToast({
        tone: "destructive",
        title: t("members.notSent"),
        description: failureText(problemOf(error)),
      });
    },
  });

  const statusOf = (invitation: InvitationResponse) =>
    invitationStatus(invitation, now);
  const roleText = (invitation: InvitationResponse) =>
    roleName(invitation.role);

  const columns: TableColumn<InvitationResponse>[] = [
    {
      key: "email",
      header: t("members.email"),
      sortable: true,
      // The email reads left to right inside a block of the page's direction (8.36); in the table a long one ends with "…" at
      // 250 px, the frame's width (8.30).
      render: (invitation) => (
        <span className="block truncate font-medium @min-[640px]:max-w-62.5">
          <bdi dir="ltr">{invitation.email}</bdi>
        </span>
      ),
    },
    {
      key: "role",
      header: t("members.role"),
      width: "w-38.5",
      render: (invitation) => <Label>{roleText(invitation)}</Label>,
    },
    {
      key: "invitedBy",
      header: t("members.invitedBy"),
      width: "w-46",
      secondary: true,
      card: "hide",
      render: (invitation) => (
        <bdi>
          {nameOf(invitation.invitedByName) ?? t("members.deletedUser")}
        </bdi>
      ),
    },
    {
      key: "expires",
      header: t("members.expires"),
      width: "w-41",
      secondary: true,
      sortable: true,
      card: "hide",
      render: (invitation) => (
        <Time
          at={invitation.expiresAt}
          show="date"
          locale={locale}
          timeZone={timeZone}
        />
      ),
    },
    {
      key: "status",
      header: t("members.status"),
      width: "w-31",
      card: "end",
      render: (invitation) => {
        const status = statusOf(invitation);
        return <Pill tone={tones[status]}>{t(`members.${status}`)}</Pill>;
      },
    },
  ];

  const itemsFor = (invitation: InvitationResponse): RowMenuItem[] => {
    const expired = statusOf(invitation) === InvitationResponseState.Expired;
    const inviteReason = reach(invitation.role.id, t("members.inviteOwner"));
    const revokeReason = reach(
      invitation.role.id,
      t("members.revokeOwnerHint"),
    );
    const items: RowMenuItem[] = [];
    if (canInvite)
      items.push({
        label: t("members.inviteAgain"),
        disabled: inviteReason !== null,
        reason: inviteReason ?? undefined,
        onSelect: () =>
          inviteAgain.mutate(
            {
              email: invitation.email,
              roleId: invitation.role.id,
              language: invitation.language,
            },
            {
              onSuccess: (sent) =>
                showToast({
                  title: t("members.sentAgain"),
                  description: t("members.sentText", {
                    email: sent.email,
                    date: formatDate(sent.expiresAt, { locale, timeZone }),
                  }),
                }),
            },
          ),
      });
    if (canRevoke)
      items.push({
        label: expired ? t("members.cancelInvitation") : t("members.revoke"),
        tone: "destructive",
        disabled: revokeReason !== null,
        reason: revokeReason ?? undefined,
        onSelect: () => setRevoking(invitation),
      });
    return items;
  };

  return (
    <>
      <Table
        columns={columns}
        rows={rows}
        rowKey="id"
        label={t("members.tabInvitations")}
        actions={
          canInvite || canRevoke
            ? (invitation) => (
                <RowMenu
                  label={t("members.actionsFor", { name: invitation.email })}
                  items={itemsFor(invitation)}
                />
              )
            : undefined
        }
        actionsLabel={t("members.actions")}
        // Every loaded row is on the one page: the list grows with Load more, so the pager never shows (F9, F10).
        rowCount={rows.length}
        page={1}
        pageSize={Math.max(1, rows.length)}
        sort={sortOf(sorts, sort)}
        onSortChange={(next) => onSortChange(sortFrom(sorts, next))}
        pager={{
          previous: t("common.previous"),
          next: t("common.next"),
          page: (current, count) => t("common.page", { page: current, count }),
        }}
      />
      {revoking && (
        <RevokeInvitationDialog
          invitation={revoking}
          expired={statusOf(revoking) === InvitationResponseState.Expired}
          onClose={() => setRevoking(null)}
        />
      )}
    </>
  );
}
