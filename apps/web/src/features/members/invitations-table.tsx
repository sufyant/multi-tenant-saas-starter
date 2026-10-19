"use client";

import {
  type InvitationResponse,
  type InviteMemberRequest,
  inviteMember,
  ListInvitationsSort,
  type ListPageOfInvitationResponse,
  MemberRole,
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
import {
  daysLeft,
  type InvitationStatus,
  invitationStatus,
} from "./invitation-status";
import { nameOf } from "./person";
import { refreshInvitations, refreshWhenTenantGone } from "./refresh";
import { RevokeInvitationDialog } from "./revoke-invitation-dialog";
import { sortFrom, sortOf } from "./table-sort";
import { useFailureText } from "./use-failure-text";

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
  expired: "neutral",
};

/**
 * The Invitations tab's table (the Invitations board): Email, Role, Invited by, Expires, Status. An expired invitation stays and
 * can be cancelled (decision 3 of step 21c). "Invite again" is a new InviteMember with a new Idempotency-Key, which replaces the
 * pending one (K2, F48).
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
  const can = useCan();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const { tenant, detail } = useTenant();
  const [revoking, setRevoking] = useState<InvitationResponse | null>(null);
  const now = new Date();
  const locale = t("locale.format");
  const viewerIsOwner = detail.role.builtIn === MemberRole.Owner;
  const canInvite = can(Permission.TenantInvitationsCreate);
  const canRevoke = can(Permission.TenantInvitationsDelete);

  const inviteAgain = useMutation({
    // The email's language is left out: the API then uses the inviter's own (K8). InvitationResponse does not carry it.
    mutationFn: (request: InviteMemberRequest) =>
      inviteMember(tenant.id, request, {
        "Idempotency-Key": crypto.randomUUID(),
      }),
    onSuccess: () => refreshInvitations(queryClient, tenant.id),
    onError: (error) => {
      refreshWhenTenantGone(queryClient, tenant.id, error);
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
    invitation.role.builtIn
      ? t(`shell.roles.${invitation.role.builtIn}`)
      : invitation.role.name;

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
    const expired = statusOf(invitation) === "expired";
    const role = invitation.role.builtIn;
    const items: RowMenuItem[] = [];
    if (canInvite && role)
      items.push({
        label: t("members.inviteAgain"),
        disabled: role === MemberRole.Owner && !viewerIsOwner,
        reason: t("members.inviteRoleHintAdmin"),
        onSelect: () =>
          inviteAgain.mutate(
            { email: invitation.email, role },
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
          expired={statusOf(revoking) === "expired"}
          onClose={() => setRevoking(null)}
        />
      )}
    </>
  );
}
