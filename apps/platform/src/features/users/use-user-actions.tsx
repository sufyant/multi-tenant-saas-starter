"use client";

import type { RowMenuItem } from "@repo/ui/components/row-menu";
import { useTranslations } from "next-intl";
import { type ReactNode, useState } from "react";
import { DeleteDialog } from "./delete-dialog";
import { ReactivateDialog } from "./reactivate-dialog";
import { RenameDialog } from "./rename-dialog";
import { SuspendDialog } from "./suspend-dialog";
import { type Action, actionsOf, type User } from "./user";

const labels = {
  rename: "users.rename",
  suspend: "users.suspend",
  reactivate: "users.reactivate",
  delete: "users.delete",
} as const satisfies Record<Action, string>;

const dialogs: Record<
  Action,
  (props: { user: User; onClose: () => void }) => ReactNode
> = {
  rename: RenameDialog,
  suspend: SuspendDialog,
  reactivate: ReactivateDialog,
  delete: DeleteDialog,
};

/**
 * The actions of the list's rows and of the user's page (the Users and UserDetail boards): the menu items by status, and the one
 * dialog that is open.
 */
export function useUserActions() {
  const t = useTranslations();
  const [open, setOpen] = useState<{ action: Action; user: User } | null>(null);

  const itemsFor = (user: User): RowMenuItem[] =>
    actionsOf(user).map((action) =>
      action === null
        ? { separator: true }
        : {
            label: t(labels[action]),
            tone: action === "delete" ? "destructive" : "default",
            onSelect: () => setOpen({ action, user }),
          },
    );

  const Open = open && dialogs[open.action];
  const dialog = open && Open && (
    <Open user={open.user} onClose={() => setOpen(null)} />
  );
  return { itemsFor, dialog };
}
