"use client";

import { type ElementType, useId } from "react";
import { Icon, LogOutIcon } from "../icon";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "../ui/dropdown-menu";

export type Choice = { id: string; label: string };

export type UserMenuProps = {
  name: string;
  email: string;
  initials: string;
  /** The accessible name of the avatar button: "Account". */
  label: string;
  /** The current language in its own language: "Türkçe". The row opens Settings > Preferences. */
  languageName: string;
  languageLabel: string;
  preferencesHref: string;
  /** light, dark or system. Saved on the device. */
  theme: string;
  themes: Choice[];
  themeLabel: string;
  onThemeChange: (theme: string) => void;
  signOutLabel: string;
  onSignOut: () => void;
  /** The link element: Next.js's Link in the apps. */
  linkComponent?: ElementType;
  /** Previews only: render the menu open. */
  open?: boolean;
};

/**
 * The menu behind the avatar (the UserMenu README): who is signed in, the language, the theme and sign out, last. It takes the
 * user from its props; this package never imports the identity provider.
 */
export function UserMenu({
  name,
  email,
  initials,
  label,
  languageName,
  languageLabel,
  preferencesHref,
  theme,
  themes,
  themeLabel,
  onThemeChange,
  signOutLabel,
  onSignOut,
  linkComponent: Link = "a",
  open,
}: UserMenuProps) {
  const themeId = useId();
  return (
    <DropdownMenu defaultOpen={open}>
      <DropdownMenuTrigger className="flex size-control-md items-center justify-center rounded-full bg-muted text-small font-semibold outline-none focus-visible:ring-2 focus-visible:ring-ring pointer-coarse:size-control-lg">
        {/* WCAG 2.5.3: the visible initials stay in the accessible name: "AY Account". */}
        {initials}
        <span className="sr-only"> {label}</span>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-72">
        <DropdownMenuGroup>
          <DropdownMenuLabel className="flex flex-col px-2 py-1.5 text-body font-normal text-foreground">
            <span className="font-medium">{name}</span>
            <bdi dir="ltr" className="text-small text-muted-foreground">
              {email}
            </bdi>
          </DropdownMenuLabel>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          render={<Link href={preferencesHref} />}
          className="justify-between"
        >
          <span>{languageLabel}</span>
          <span className="text-muted-foreground">{languageName}</span>
        </DropdownMenuItem>
        {/* The theme is a radio group of the menu (WAI-ARIA APG, "Menu Button"): a menu holds only menu items and their groups. */}
        <div className="flex items-center justify-between gap-2 px-1.5 py-1 text-sm">
          <span id={themeId}>{themeLabel}</span>
          <DropdownMenuRadioGroup
            aria-labelledby={themeId}
            value={theme}
            onValueChange={(value) => onThemeChange(String(value))}
            className="flex"
          >
            {themes.map((choice) => (
              <DropdownMenuRadioItem
                key={choice.id}
                value={choice.id}
                closeOnClick={false}
                className="h-7 rounded-none border border-input border-s-0 px-2 text-small font-medium first:rounded-s-md first:border-s last:rounded-e-md data-checked:bg-muted *:data-[slot=dropdown-menu-radio-item-indicator]:hidden"
              >
                {choice.label}
              </DropdownMenuRadioItem>
            ))}
          </DropdownMenuRadioGroup>
        </div>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={onSignOut}>
          <Icon icon={LogOutIcon} directional />
          {signOutLabel}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
