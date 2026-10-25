"use client";

import { Menu as MenuPrimitive } from "@base-ui/react";
import { type ElementType, useId } from "react";
import { Avatar, initialsOf, type Person } from "../avatar";
import {
  ChevronRightIcon,
  GlobeIcon,
  Icon,
  LogOutIcon,
  type LucideIcon,
  MonitorIcon,
  MoonIcon,
  SunIcon,
} from "../icon";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "../ui/dropdown-menu";
import { Tooltip, TooltipContent, TooltipTrigger } from "../ui/tooltip";

export type Choice = { id: string; label: string };

export type UserMenuProps = {
  name: string;
  email: string;
  /** The signed-in person: the avatar draws their initials. */
  person: Person;
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

/** Each theme's icon (the UserMenu README): sun, moon, monitor. */
const themeIcons: Record<string, LucideIcon> = {
  light: SunIcon,
  dark: MoonIcon,
  system: MonitorIcon,
};

/**
 * The menu behind the avatar (the UserMenu README): who is signed in, the language, the theme and sign out, last. It opens 6px
 * below the avatar at its end edge. It takes the user from its props; this package never imports the identity provider.
 */
export function UserMenu({
  name,
  email,
  person,
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
  const initials = initialsOf(person);
  return (
    <DropdownMenu defaultOpen={open}>
      <DropdownMenuTrigger className="flex size-control-md shrink-0 rounded-full outline-none transition-shadow hover:ring-2 hover:ring-input hover:ring-offset-2 hover:ring-offset-background focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-0 data-popup-open:ring-2 data-popup-open:ring-input data-popup-open:ring-offset-2 data-popup-open:ring-offset-background max-tablet:size-control-lg">
        <Avatar person={person} className="size-full" />
        {/* WCAG 2.5.3: the visible initials stay in the accessible name: "AY Account". */}
        <span className="sr-only">
          {initials} {label}
        </span>
      </DropdownMenuTrigger>
      <DropdownMenuContent
        align="end"
        sideOffset={6}
        className="w-70 max-w-[calc(100vw-var(--space-8))]"
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
        <DropdownMenuSeparator />
        <DropdownMenuItem
          render={<Link href={preferencesHref} />}
          className="min-h-9 gap-2.5 px-2 text-body"
        >
          <Icon icon={GlobeIcon} />
          <span className="flex-1">{languageLabel}</span>
          <bdi className="text-muted-foreground">{languageName}</bdi>
          <Icon
            icon={ChevronRightIcon}
            size={14}
            directional
            className="text-muted-foreground"
          />
        </DropdownMenuItem>
        {/* The theme is a radio group of the menu (WAI-ARIA APG, "Menu Button"): a menu holds only menu items and their groups. */}
        <div className="flex min-h-9 items-center gap-2.5 py-0.5 ps-2 pe-1 text-body">
          <span id={themeId} className="flex-1">
            {themeLabel}
          </span>
          <DropdownMenuRadioGroup
            aria-labelledby={themeId}
            value={theme}
            onValueChange={(value) => onThemeChange(String(value))}
            className="flex gap-0.5 rounded-full p-0.5 inset-ring inset-ring-border"
          >
            {themes.map((choice) => (
              <Tooltip key={choice.id}>
                <TooltipTrigger
                  render={
                    <MenuPrimitive.RadioItem
                      value={choice.id}
                      closeOnClick={false}
                      aria-label={choice.label}
                    />
                  }
                  className="flex size-7 items-center justify-center rounded-full text-muted-foreground outline-none transition-colors hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring data-checked:bg-primary-subtle data-checked:text-primary-subtle-foreground pointer-coarse:size-touch-target"
                >
                  <Icon icon={themeIcons[choice.id] ?? MonitorIcon} />
                </TooltipTrigger>
                <TooltipContent side="bottom" sideOffset={6}>
                  {choice.label}
                </TooltipContent>
              </Tooltip>
            ))}
          </DropdownMenuRadioGroup>
        </div>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          onClick={onSignOut}
          className="min-h-9 gap-2.5 px-2 text-body"
        >
          <Icon icon={LogOutIcon} directional />
          {signOutLabel}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
