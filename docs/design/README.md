# Design

This folder is the design of both apps: the tenant web app (`apps/web`) and the platform app (`apps/platform`). A screen in code follows the part of this folder that describes it. Code steps read these files, not Figma.

## How to read it

1. Read [patterns.md](patterns.md). It holds the rules every screen follows: screen sizes, voice, dates, long text, dialogs, confirmations, billing bars.
2. Read [sample-data.md](sample-data.md). Every screen uses this one world: the same organisation, people, counts and amounts.
3. Open the section of the app you build, in [web/](web) or [platform/](platform). Its `README.md` has one part per screen and state, with the PNG beside it.
4. Look up a part in [components.md](components.md) when a screen names it.
5. Take colours, type, spacing and sizes from [tokens.json](tokens.json).

Each screen part says:

- **Who sees it**: the person, role or permission.
- **Device**: Desktop (1280 px and wider), Tablet (768 to 1279 px), Phone (up to 767 px) or Email.
- **This state shows**: when the screen looks like this (loading, error, empty, a role, dark, Arabic).
- **Values come from**: Clerk, our API, packages/brand or the build.
- **Components**: the parts on the screen, with their counts.
- **Actions**: what each button or link does, as the prototype links it.
- **Texts**: every text on the screen, word for word, in reading order.

Screen numbers (`8.01`) are the section and the screen. A number with a letter (`9.r16`) is a variant of a screen. Two web screens share the number 8.36: "Members · Arabic" and "Remove member · phone sheet". Their files have different names.

A screen not drawn on a phone follows the screen size rules in [patterns.md](patterns.md). Dark and Arabic screens show the rule once; every other screen works in both too.

## Tokens

[tokens.json](tokens.json) has the format of `packages/ui/tokens/tokens.json`, version 4. It holds the values of the approved design (Instrument Sans, JetBrains Mono, Noto Sans Arabic; the warm neutral palette). `packages/ui` does not change in this step. The code step that takes the design copies this file over `packages/ui/tokens/tokens.json` and runs `pnpm generate`.

Three values need that step's decision:

- `control-lg`: the design's large button is 36 px; the code's `control-lg` is the 44 px phone control. This file keeps 44 px.
- `platform` and `platform-foreground`: the design has no variable for them, so the values are the code's, unchanged.
- Shadows: the design has light values only. The dark values use the design's `shadow-soft` and `shadow-wide` colours.

## Sections in order

### Web app (308 screens)

| Section | Screens |
|---|---|
| [1 · Entry](web/01-entry/README.md) | 39 |
| [2 · Shell](web/02-shell/README.md) | 44 |
| [3 · Notifications](web/03-notifications/README.md) | 20 |
| [4 · Home](web/04-home/README.md) | 3 |
| [5 · Settings › Profile](web/05-settings-profile/README.md) | 7 |
| [6 · Settings › Account and security](web/06-settings-account-and-security/README.md) | 32 |
| [7 · Settings › General](web/07-settings-general/README.md) | 5 |
| [8 · Settings › Members](web/08-settings-members/README.md) | 46 |
| [9 · Settings › Roles](web/09-settings-roles/README.md) | 20 |
| [10 · Settings › Billing](web/10-settings-billing/README.md) | 24 |
| [11 · Settings › Usage](web/11-settings-usage/README.md) | 9 |
| [12 · Settings › Activity log](web/12-settings-activity-log/README.md) | 8 |
| [13 · Emails](web/13-emails/README.md) | 36 |
| [14 · Settings › Billing manager](web/14-settings-billing-manager/README.md) | 15 |

### Platform app (220 screens)

| Section | Screens |
|---|---|
| [1 · Entry](platform/01-entry/README.md) | 12 |
| [2 · Shell](platform/02-shell/README.md) | 26 |
| [3 · Notifications](platform/03-notifications/README.md) | 8 |
| [4 · Organisations](platform/04-organisations/README.md) | 46 |
| [5 · Users](platform/05-users/README.md) | 19 |
| [6 · Plans](platform/06-plans/README.md) | 18 |
| [7 · Activity log](platform/07-activity-log/README.md) | 8 |
| [8 · Settings › Profile](platform/08-settings-profile/README.md) | 7 |
| [9 · Settings › Account and security](platform/09-settings-account-and-security/README.md) | 13 |
| [10 · Settings › System admins](platform/10-settings-system-admins/README.md) | 15 |
| [11 · Settings › Terms of use](platform/11-settings-terms-of-use/README.md) | 15 |
| [12 · Settings › Feature flags](platform/12-settings-feature-flags/README.md) | 23 |
| [13 · Settings › Sign-up](platform/13-settings-sign-up/README.md) | 5 |
| [14 · Emails](platform/14-emails/README.md) | 5 |

## Prototype

The Figma prototype clicks through the screens. It is a help, not the source: these files are.

| Start | Opens at |
|---|---|
| Web · desktop | [1.01 Sign in · sign-up off](https://www.figma.com/proto/3Ev111okX8xCBwR9B3hyBM/?node-id=94-12379&starting-point-node-id=94%3A12379) |
| Web · phone | [1.21 Phone · sign in](https://www.figma.com/proto/3Ev111okX8xCBwR9B3hyBM/?node-id=94-12779&starting-point-node-id=94%3A12779) |
| Web · sign-up | [1.24 Sign in · sign-up on](https://www.figma.com/proto/3Ev111okX8xCBwR9B3hyBM/?node-id=242-22251&starting-point-node-id=242%3A22251) |
| Web · billing manager | [14.08 Home as a Billing manager](https://www.figma.com/proto/3Ev111okX8xCBwR9B3hyBM/?node-id=287-25313&starting-point-node-id=287%3A25313) |
| Platform · desktop | [1.01 Sign in](https://www.figma.com/proto/3Ev111okX8xCBwR9B3hyBM/?node-id=148-788&starting-point-node-id=148%3A788) |
| Platform · phone | [1.10 Phone · sign in](https://www.figma.com/proto/3Ev111okX8xCBwR9B3hyBM/?node-id=259-16845&starting-point-node-id=259%3A16845) |

Figma file: [SaaS Template · App Design](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/). Each screen part links its frame.

## How this folder was made

Exported from the Figma file on 3 Nov 2026 with the Figma REST API: the file's nodes for the texts, components and prototype links, and the images endpoint for the PNGs at scale 1, compressed with pngquant. The variables were read once with the Figma plugin API, because the variables REST API needs an Enterprise plan. Change the design in Figma first, then export again; do not edit a screen part by hand.
