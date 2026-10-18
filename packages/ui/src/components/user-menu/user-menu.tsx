"use client";

import { type ElementType, useId } from "react";
import { Icon, LogOutIcon } from "../icon";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "../ui/dropdown-menu";
import { ToggleGroup, ToggleGroupItem } from "../ui/toggle-group";

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
      <DropdownMenuTrigger
        aria-label={label}
        className="flex size-control-md items-center justify-center rounded-full bg-muted text-small font-semibold outline-none focus-visible:ring-2 focus-visible:ring-ring pointer-coarse:size-control-lg"
      >
        {initials}
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
        <div className="flex items-center justify-between gap-2 px-1.5 py-1 text-sm">
          <span id={themeId}>{themeLabel}</span>
          <ToggleGroup
            aria-labelledby={themeId}
            value={[theme]}
            onValueChange={(value) =>
              value[0] && onThemeChange(String(value[0]))
            }
            spacing={0}
            variant="outline"
            size="sm"
          >
            {themes.map((choice) => (
              <ToggleGroupItem
                key={choice.id}
                value={choice.id}
                className="px-2 text-small"
              >
                {choice.label}
              </ToggleGroupItem>
            ))}
          </ToggleGroup>
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
