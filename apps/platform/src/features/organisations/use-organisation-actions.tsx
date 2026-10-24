"use client";

import type { RowMenuItem } from "@repo/ui/components/row-menu";
import { useTranslations } from "next-intl";
import { type ReactNode, useState } from "react";
import { CloseDialog } from "./close-dialog";
import { InviteOwnerDialog } from "./invite-owner-dialog";
import { type Action, actionsOf, type Organisation } from "./organisation";
import { ReactivateDialog } from "./reactivate-dialog";
import { RenameDialog } from "./rename-dialog";
import { ReopenDialog } from "./reopen-dialog";
import { SuspendDialog } from "./suspend-dialog";

const labels = {
  inviteOwner: "organisations.inviteOwner",
  rename: "organisations.rename",
  suspend: "organisations.suspend",
  reactivate: "organisations.reactivate",
  close: "organisations.close",
  reopen: "organisations.reopen",
} as const satisfies Record<Action, string>;

const dialogs: Record<
  Action,
  (props: { organisation: Organisation; onClose: () => void }) => ReactNode
> = {
  inviteOwner: InviteOwnerDialog,
  rename: RenameDialog,
  suspend: SuspendDialog,
  reactivate: ReactivateDialog,
  close: CloseDialog,
  reopen: ReopenDialog,
};

/**
 * The lifecycle actions of the list's rows and of the organisation's page (the Main and OrgDetail boards): the menu items by
 * status, and the one dialog that is open.
 */
export function useOrganisationActions() {
  const t = useTranslations();
  const [open, setOpen] = useState<{
    action: Action;
    organisation: Organisation;
  } | null>(null);
  const start = (action: Action, organisation: Organisation) =>
    setOpen({ action, organisation });

  const itemsFor = (organisation: Organisation): RowMenuItem[] =>
    actionsOf(organisation).map((action) =>
      action === null
        ? { separator: true }
        : {
            label: t(labels[action]),
            tone: action === "close" ? "destructive" : "default",
            onSelect: () => start(action, organisation),
          },
    );

  const Open = open && dialogs[open.action];
  const dialog = open && Open && (
    <Open organisation={open.organisation} onClose={() => setOpen(null)} />
  );
  return { itemsFor, start, dialog };
}
