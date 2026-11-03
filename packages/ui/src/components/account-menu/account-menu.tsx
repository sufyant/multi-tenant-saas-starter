"use client";

import { useTranslations } from "next-intl";
import { type ElementType, useEffect, useState } from "react";
import { cn } from "../../lib/utils";
import { Avatar, initialsOf, type Person } from "../avatar";
import { copiedMs } from "../copy-button";
import {
  ChevronsUpDownIcon,
  Icon,
  LockIcon,
  LogOutIcon,
  UserIcon,
} from "../icon";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "../ui/dropdown-menu";

export type AccountMenuProps = {
  name: string;
  email: string;
  /** The signed-in person: the avatar draws their initials. */
  person: Person;
  /** The person's Profile page. */
  profileHref: string;
  /** Account and security; without it the item does not show, as a page a person cannot open is not in their menu. */
  accountAndSecurityHref?: string;
  onSignOut: () => void;
  /** The image's commit, seven characters (`shortRevision`); without it, as in a local run, the version row does not show. */
  version?: string;
  /** The link element: Next.js's Link in the apps. */
  linkComponent?: ElementType;
  /** rail: the avatar alone, as on a tablet's icon rail; row: the avatar and the name. */
  variant?: "row" | "rail";
  /** Previews only: render the menu open. */
  open?: boolean;
};

/**
 * The account menu at the foot of the sidebar and of the phone drawer (the Account menu screens): who is signed in, Profile,
 * Account and security, Sign out, and last "Version a1b2c3d" in small grey text. A click on the version copies it and says
 * "Copied" for two seconds, without closing the menu. It takes the person from its props; this package never imports the identity
 * provider.
 */
export function AccountMenu({
  name,
  email,
  person,
  profileHref,
  accountAndSecurityHref,
  onSignOut,
  version,
  linkComponent: Link = "a",
  variant = "row",
  open,
}: AccountMenuProps) {
  const t = useTranslations();
  const [copied, setCopied] = useState(false);
  useEffect(() => {
    if (!copied) return;
    const timer = setTimeout(() => setCopied(false), copiedMs);
    return () => clearTimeout(timer);
  }, [copied]);
  const item = "min-h-9 gap-2.5 px-2 text-body max-tablet:min-h-touch-target";
  return (
    <>
      {/* A menu holds only its items (WAI-ARIA APG, "Menu Button"), so the live region of "Copied" sits beside the menu. */}
      <span role="status" className="sr-only">
        {copied ? t("patterns.copied") : ""}
      </span>
      <DropdownMenu defaultOpen={open}>
        <DropdownMenuTrigger
          className={cn(
            "flex h-control-md min-w-0 items-center gap-2.5 rounded-md text-small font-medium outline-none hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring data-popup-open:bg-accent max-tablet:h-touch-target",
            variant === "row" ? "w-full ps-1.5 pe-2" : "size-10 justify-center",
          )}
        >
          <Avatar person={person} className="size-5.5 shrink-0" />
          {/* WCAG 2.5.3: the visible name stays in the accessible name; on the rail the initials and "Account" give it. */}
          {variant === "row" ? (
            <>
              <bdi className="min-w-0 flex-1 truncate text-start">{name}</bdi>
              <Icon
                icon={ChevronsUpDownIcon}
                size={14}
                className="text-muted-foreground"
              />
            </>
          ) : (
            <span className="sr-only">
              {initialsOf(person)} {t("common.account")}
            </span>
          )}
        </DropdownMenuTrigger>
        <DropdownMenuContent
          side="top"
          align="start"
          sideOffset={6}
          className="w-60 max-w-[calc(100vw-var(--space-8))]"
        >
          <DropdownMenuGroup>
            <DropdownMenuLabel className="flex flex-col gap-0.5 p-2 text-foreground">
              <bdi className="text-body font-medium wrap-anywhere">{name}</bdi>
              <bdi
                dir="ltr"
                className="text-small font-normal text-muted-foreground wrap-anywhere"
              >
                {email}
              </bdi>
            </DropdownMenuLabel>
          </DropdownMenuGroup>
          <DropdownMenuItem
            render={<Link href={profileHref} />}
            className={item}
          >
            <Icon icon={UserIcon} />
            {t("appShell.profile")}
          </DropdownMenuItem>
          {accountAndSecurityHref && (
            <DropdownMenuItem
              render={<Link href={accountAndSecurityHref} />}
              className={item}
            >
              <Icon icon={LockIcon} />
              {t("appShell.accountAndSecurity")}
            </DropdownMenuItem>
          )}
          <DropdownMenuSeparator />
          <DropdownMenuItem onClick={onSignOut} className={item}>
            <Icon icon={LogOutIcon} directional />
            {t("common.signOut")}
          </DropdownMenuItem>
          {version && (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuItem
                closeOnClick={false}
                aria-label={t("appShell.copyVersion", { version })}
                onClick={() =>
                  void navigator.clipboard
                    .writeText(version)
                    .then(() => setCopied(true))
                }
                className="min-h-7 px-2 text-small text-muted-foreground max-tablet:min-h-touch-target"
              >
                <span aria-hidden="true">
                  {copied
                    ? t("patterns.copied")
                    : t("appShell.version", { version })}
                </span>
              </DropdownMenuItem>
            </>
          )}
        </DropdownMenuContent>
      </DropdownMenu>
    </>
  );
}
