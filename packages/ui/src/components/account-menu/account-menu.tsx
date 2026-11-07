"use client";

import { useTranslations } from "next-intl";
import { type ElementType, useEffect, useState } from "react";
import { cn } from "../../lib/utils";
import { Avatar, initialsOf, type Person } from "../avatar";
import { copiedMs } from "../copy-button";
import {
  ArrowRightLeftIcon,
  ChevronsUpDownIcon,
  ExternalLinkIcon,
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
  /** The person's Profile page; without it the item does not show, as on "No access yet", which has no organisation (1.19). */
  profileHref?: string;
  /** Account and security; without it the item does not show, as a page a person cannot open is not in their menu. */
  accountAndSecurityHref?: string;
  /** The platform's own item, "Open the web app": the web app's origin, opened in a new tab. Without it the item does not show. */
  webAppHref?: string;
  onSignOut: () => void;
  /** The image's commit, seven characters (`shortRevision`); without it, as in a local run, the version row does not show. */
  version?: string;
  /** The link element: Next.js's Link in the apps. */
  linkComponent?: ElementType;
  /**
   * rail: the sidebar's place, the avatar alone on a tablet's icon rail and the avatar and the name from bp-desktop; row: always
   * both; chip: the round button at the top of a page without the shell, with the menu under it (1.19).
   */
  variant?: "row" | "rail" | "chip";
  /** Previews only: render the menu open. */
  open?: boolean;
};

/**
 * The account menu at the foot of the sidebar and of the phone drawer (the Account menu screens): who is signed in, Profile,
 * Account and security, on the platform "Open the web app" in a new tab, Sign out, and last "Version a1b2c3d" in small grey text. A click on the version copies it and says
 * "Copied" for two seconds, without closing the menu. It takes the person from its props; this package never imports the identity
 * provider.
 */
export function AccountMenu({
  name,
  email,
  person,
  profileHref,
  accountAndSecurityHref,
  webAppHref,
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
  const rail = variant === "rail";
  const chip = variant === "chip";
  const item = "max-tablet:min-h-touch-target";
  return (
    <>
      {/* A menu holds only its items (WAI-ARIA APG, "Menu Button"), so the live region of "Copied" sits beside the menu. */}
      <span role="status" className="sr-only">
        {copied ? t("patterns.copied") : ""}
      </span>
      <DropdownMenu defaultOpen={open}>
        <DropdownMenuTrigger
          className={cn(
            "flex min-w-0 items-center text-small font-medium outline-none hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring data-popup-open:bg-accent",
            chip
              ? "h-9 max-w-60 gap-2 rounded-full border border-border bg-card ps-1 pe-2.5"
              : "w-full gap-2.5 rounded-md py-1 ps-1.5 pe-2 max-tablet:h-12 max-tablet:px-2",
            rail &&
              "max-desktop:size-12 max-desktop:justify-center max-desktop:p-0",
          )}
        >
          <Avatar
            person={person}
            className={cn(
              "shrink-0",
              chip ? "size-7" : "size-5.5 max-tablet:size-7",
              rail && "max-desktop:size-8",
            )}
          />
          {/* WCAG 2.5.3: the visible name stays in the accessible name; on the icon rail the initials and "Account" give it. */}
          <bdi
            className={cn(
              "min-w-0 flex-1 truncate text-start",
              rail && "max-desktop:hidden",
            )}
          >
            {name}
          </bdi>
          {!chip && (
            <Icon
              icon={ChevronsUpDownIcon}
              size={14}
              className={cn(
                "text-muted-foreground",
                rail && "max-desktop:hidden",
              )}
            />
          )}
          {rail && (
            <span className="sr-only desktop:hidden">
              {initialsOf(person)} {t("common.account")}
            </span>
          )}
        </DropdownMenuTrigger>
        <DropdownMenuContent
          side={chip ? "bottom" : "top"}
          align={chip ? "end" : "start"}
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
          {profileHref && (
            <DropdownMenuItem
              render={<Link href={profileHref} />}
              className={item}
            >
              <Icon icon={UserIcon} />
              {t("appShell.profile")}
            </DropdownMenuItem>
          )}
          {accountAndSecurityHref && (
            <DropdownMenuItem
              render={<Link href={accountAndSecurityHref} />}
              className={item}
            >
              <Icon icon={LockIcon} />
              {t("appShell.accountAndSecurity")}
            </DropdownMenuItem>
          )}
          {webAppHref && (
            <DropdownMenuItem
              render={<a href={webAppHref} target="_blank" rel="noopener" />}
              className={item}
            >
              <Icon icon={ArrowRightLeftIcon} />
              <span className="flex-1">
                {t("appShell.openWebApp")}{" "}
                <span className="sr-only">{t("appShell.opensInNewTab")}</span>
              </span>
              <Icon icon={ExternalLinkIcon} className="text-muted-foreground" />
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
