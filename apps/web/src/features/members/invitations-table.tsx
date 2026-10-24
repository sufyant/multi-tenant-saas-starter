"use client";

import {
  type InvitationResponse,
  InvitationResponseState,
  type InviteMemberRequest,
  inviteMember,
  ListInvitationsSort,
  type ListPageOfInvitationResponse,
  Permission,
} from "@repo/api-client";
import { RowMenu, type RowMenuItem } from "@repo/ui/components/row-menu";
import { Status, type StatusTone } from "@repo/ui/components/status";
import { Table, type TableColumn } from "@repo/ui/components/table";
import { Time } from "@repo/ui/components/time";
import { showToast } from "@repo/ui/components/toast";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useCan, useTenant } from "@/shared/tenant-context";
import { useFailureText } from "@/shared/use-failure-text";
import { useRoleName } from "@/shared/use-role-name";
import {
  daysLeft,
  type InvitationStatus,
  invitationStatus,
} from "./invitation-status";
import { nameOf } from "./person";
import { afterRefusal, refreshInvitations } from "./refresh";
import { RevokeInvitationDialog } from "./revoke-invitation-dialog";
import { sortFrom, sortOf } from "./table-sort";
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

const tones: Record<InvitationStatus, StatusTone> = {
  invited: "info",
  expiresSoon: "warning",
  [InvitationResponseState.Expired]: "neutral",
};

/**
 * The Invitations tab's table (the Invitations board): Email, Role, Invited by, Expires, Status. An expired invitation stays and
 * can be cancelled (decision 3 of step 21c). "Invite again" is a new InviteMember with a new Idempotency-Key, which replaces the
 * pending one (K2, F48). "Invite again" needs `tenant.invitations.create`, revoking and cancelling `tenant.invitations.delete`,
 * and each only for an invitation whose role the viewer's role fully holds (F51); a disabled item says why.
 */
export function InvitationsTable({
  page,
  sort,
  timeZone,
  onSortChange,
  onPageChange,
}: {
  page: ListPageOfInvitationResponse;
  sort: string | null;
  timeZone: string | null | undefined;
  onSortChange: (sort: string | null) => void;
  onPageChange: (page: number) => void;
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
        description: failureText(error),
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
      render: (invitation) => (
        <bdi dir="ltr" className="font-medium">
          {invitation.email}
        </bdi>
      ),
    },
    { key: "role", header: t("members.role"), render: roleText },
    {
      key: "invitedBy",
      header: t("members.invitedBy"),
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
      secondary: true,
      sortable: true,
      card: "hide",
      render: (invitation) => (
        <Time
          at={invitation.expiresAt}
          show="relative"
          now={now}
          locale={locale}
          timeZone={timeZone}
        />
      ),
    },
    {
      key: "status",
      header: t("members.status"),
      card: "end",
      render: (invitation) => {
        const status = statusOf(invitation);
        return <Status tone={tones[status]}>{t(`members.${status}`)}</Status>;
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
                    days: daysLeft(sent.expiresAt, new Date()),
                  }),
                }),
            },
          ),
      });
    if (items.length > 0 && canRevoke) items.push({ separator: true });
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
        rows={page.items}
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
        rowCount={page.total}
        page={page.page}
        pageSize={page.pageSize}
        onPageChange={onPageChange}
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
