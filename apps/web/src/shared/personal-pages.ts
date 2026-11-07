import type { NavItem } from "@repo/ui/components/app-shell";
import { LockIcon, UserIcon } from "@repo/ui/components/icon";

/**
 * The person's own pages of Settings, the same in every organisation: Profile and Account and security open without an
 * organisation, at the platform's paths (decision 69). The old addresses under the slug lead there.
 */
export const personalPages = {
  profile: "/settings/profile",
  account: "/settings/account",
} as const;

/** My account in a settings menu and in the command menu: both shells list it. */
export const myAccountPages = (
  t: (key: "appShell.profile" | "appShell.accountAndSecurity") => string,
): NavItem[] => [
  {
    id: "profile",
    label: t("appShell.profile"),
    icon: UserIcon,
    href: personalPages.profile,
  },
  {
    id: "account",
    label: t("appShell.accountAndSecurity"),
    icon: LockIcon,
    href: personalPages.account,
  },
];
