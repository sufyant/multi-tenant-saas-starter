"use client";

import { useTranslations } from "next-intl";
import { cn } from "../../lib/utils";
import { Icon, UserIcon } from "../icon";

/**
 * A person whose account was deleted (the A deleted person pattern): "Deleted user" in the muted colour, with the person icon in
 * place of the initials. The same in lists, the Activity log and notifications.
 */
export function DeletedUser({ className }: { className?: string }) {
  const t = useTranslations("patterns");
  return (
    <span
      className={cn(
        "inline-flex items-center gap-2 text-muted-foreground",
        className,
      )}
    >
      <span
        aria-hidden="true"
        className="flex size-6 shrink-0 items-center justify-center rounded-full bg-muted"
      >
        <Icon icon={UserIcon} size={14} />
      </span>
      {t("deletedUser")}
    </span>
  );
}
