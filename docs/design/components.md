# Components

The parts every screen is built from. Each entry gives the variants, the properties and the rule written on the component in Figma. Code names follow packages/ui where the part exists there.

## Icons and logos

Each icon is a component on the Components page. Logos keep their brand colours and are never mirrored in Arabic.

`Icon/home`, `Icon/users`, `Icon/shield`, `Icon/activity`, `Icon/settings`, `Icon/bell`, `Icon/search`, `Icon/chevrons-up-down`, `Icon/ellipsis`, `Icon/plus`, `Icon/chevron-down`, `Icon/user`, `Icon/mail`, `Icon/calendar`, `Icon/check`, `Icon/list-filter`, `Icon/x`, `Icon/menu`, `Icon/lock`, `Icon/log-out`, `Icon/arrow-right-left`, `Icon/triangle-alert`, `Icon/info`, `Icon/circle-check`, `Icon/circle-x`, `Icon/refresh-cw`, `Icon/user-plus`, `Icon/wifi-off`, `Icon/external-link`, `Icon/trash`, `Icon/search-x`, `Icon/credit-card`, `Icon/arrow-left`, `Icon/download`, `Icon/shield-check`, `Icon/copy`, `Icon/flask-conical`, `Icon/building-2`, `Icon/flag`, `Icon/file-text`, `Icon/gauge`, `Icon/arrow-left · RTL`, `Icon/globe`, `Logo/Google`, `Logo/Microsoft`

## Core components

### Button

Buttons are a verb and the thing: “Invite member”, “Remove member”. Danger only for a destructive confirmation. Focus: 2 px ring (color/ring). Disabled: 45% and not focusable; say why nearby.  
  
Name a button by what it does. On plan and limit screens: “Request a plan change”: it opens the support email (supportUrl in packages/brand, today mailto:support@starter.example) with the subject “Plan change for &lt;organisation&gt;”. Elsewhere (errors, access problems): “Contact support”, the same address. With billing (step 36), “Request a plan change” becomes “Change plan” and opens the billing page. The icon is off by default. Turn "Icon" on, with Icon/plus, only for a button that creates or adds something.

| Property | Type | Values or default |
|---|---|---|
| Label | Text | Button |
| Icon | Boolean | False |
| Kind | Variant | Primary, Secondary, Ghost, Danger |
| State | Variant | Default, Hover, Focus, Disabled |
| Direction | Variant | LTR, RTL |

Figma: [45:302](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=45-302)

### Pill

Status pill. Colour means a status: Active (Success), Suspended (Warning), Closed (Neutral, filled), Being deleted (Danger), Scheduled (Info). Roles and other plain facts use the Label component, not the Pill.

| Property | Type | Values or default |
|---|---|---|
| Label | Text | Member |
| Dot | Boolean | True |
| Tone | Variant | Neutral, Warning, Success, Danger, Info |
| Direction | Variant | LTR, RTL |

Figma: [45:324](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=45-324)

### Avatar

Initials: first letter of the first and last name; Arabic names take Arabic letters.

| Property | Type | Values or default |
|---|---|---|
| Initials | Text | AD |
| Size | Variant | Small, Medium, Large |

Figma: [45:334](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=45-334)

### Checkbox

Off, On, Mixed (header row when some rows are selected).  
State=Disabled: the person can’t change this box. Say why next to it or in the row’s text.

| Property | Type | Values or default |
|---|---|---|
| Checked | Variant | Off, On, Mixed |
| State | Variant | Default, Focus, Disabled |

Figma: [45:366](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=45-366)

### Input

Placeholder in muted-foreground; typed text in foreground.  
Content=Placeholder shows a hint in the muted colour. Content=Value shows what the person typed or what is stored, in the foreground colour.

| Property | Type | Values or default |
|---|---|---|
| Text | Text | Search by name or email |
| Icon | Boolean | True |
| State | Variant | Default, Focus, Error, Disabled |
| Direction | Variant | LTR, RTL |
| Content | Variant | Value, Placeholder |

Figma: [45:394](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=45-394)

### Tab

| Property | Type | Values or default |
|---|---|---|
| Label | Text | Members |
| Count | Text | 12 |
| Show count | Boolean | True |
| State | Variant | Current, Default |
| Direction | Variant | LTR, RTL |

Figma: [45:456](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=45-456)

### Menu item

| Property | Type | Values or default |
|---|---|---|
| Label | Text | Menu item |
| State | Variant | Default, Hover |
| External | Variant | No, Yes |

Figma: [45:494](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=45-494)

### Kbd

| Property | Type | Values or default |
|---|---|---|
| Key | Text | G |

Figma: [45:498](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=45-498)

### Tooltip

| Property | Type | Values or default |
|---|---|---|
| Label | Text | Go to Members |
| Keys | Text | G then M |

Figma: [45:500](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=45-500)

### Toast

| Property | Type | Values or default |
|---|---|---|
| Title | Text | Invitation sent |
| Text | Text | lena@northwind.example can join until 14 Nov 2026. |
| Tone | Variant | Success, Error |

Figma: [45:523](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=45-523)

### Banner

| Property | Type | Values or default |
|---|---|---|
| Title | Text | Title |
| Text | Text | Text |
| Action | Boolean | True |
| Second action | Boolean | False |
| Dismissible | Boolean | False |
| Tone | Variant | Info, Warning, Danger |

Figma: [45:572](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=45-572)

### Empty state

| Property | Type | Values or default |
|---|---|---|
| Title | Text | No pending invitations |
| Text | Text | People you invite appear here until they join or the link expires. |
| Action | Boolean | True |

Figma: [45:576](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=45-576)

### Skeleton

Figma: [45:595](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=45-595)

### Environment strip

Every environment except Production, both apps, at the very top of the window; it pushes the layout down and never covers content. Labels: “Staging”, “Preview · #182”, “Local”. Desktop 24 px; phone 28 px, below the safe area, above the top bar. Light: pale amber with dark amber text; dark: deep amber tint with light text (variables warning-subtle and warning-text). Nothing in Production. The tab icon is amber and the title starts with “[Staging]”.

| Property | Type | Values or default |
|---|---|---|
| Label | Text | Staging |
| Size | Variant | Desktop, Phone |

Figma: [142:1008](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=142-1008)

### Label

Label for roles and other plain facts: Owner, Admin, Member, a custom role, the platform's System admin, a flag type, "This device". No fill, a thin border, dark text. Colour is for statuses only; use the Pill for a status. Model: GitHub Primer, Label (default variant).

| Property | Type | Values or default |
|---|---|---|
| Label | Text | Member |
| Direction | Variant | LTR, RTL |

Figma: [217:29749](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=217-29749)

### Account menu · Version

Last row of the account menu, in the web app and the platform app, in Staging and Production. Shows "Version" and the first 7 characters of the commit the image was built from (SOURCE_REVISION), the same value Sentry names as the release. It is not the v* release tag (docs/deployment.md, section 5). A click copies the 7 characters and shows "Copied" for 2 seconds. "Contact support" opens an email to support@starter.example with "Version: a1b2c3d" in the body.

| Property | Type | Values or default |
|---|---|---|
| State | Variant | Default, Copied |

Figma: [218:1105](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=218-1105)

### Connection bar

At the top of the main panel; it pushes the content down and the page stays usable with what it shows. Offline: retries after 5, 10, then 30 s; “Try now” retries at once. Server not answering (503): the same bar. When the connection is back: “Back online” for 2 s, then the bar goes. A section that fails shows its own “Something went wrong · Try again”. A full-page error only when the app cannot start. “Check the status page” (property Status link, State=Server) shows only when the brand’s optional statusUrl is set; it opens that page. The same link sits beside “Contact support” when statusUrl is set.

| Property | Type | Values or default |
|---|---|---|
| Status link | Boolean | False |
| State | Variant | Offline, Server, Back online |

Figma: [164:16541](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=164-16541)

### Billing bar

Billing bar. Sits where the Connection bar sits, under the top bar. Only people who hold tenant.billing.manage (Manage billing) see it. One bar at a time; the Connection bar always wins. Priority: Payment grace, Payment failed, Cap reached, Usage 100 · Free, Usage 100, Trial ends, Usage 80. During a trial the Trial ends bar shows the end date (“Your 14-day Business trial ends on 16 Nov.”); 3 days before the end it says “ends in 3 days”. The action opens Billing or Usage.

| Property | Type | Values or default |
|---|---|---|
| State | Variant | Trial ends, Payment failed, Payment grace, Usage 80, Usage 100, Usage 100 · Free, Cap reached |

Figma: [229:1133](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=229-1133)

### Meter

Meter. One per usage meter (AI requests is a sample; a product defines its own meters in code). Stack several in one card. Near = 80% or more. Over = past the quota on a paid plan, the extra part in amber. Stopped = quota reached on Free, or the spending cap reached.

| Property | Type | Values or default |
|---|---|---|
| State | Variant | Normal, Near, Over, Stopped |

Figma: [229:1163](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=229-1163)

## Shell components

### Select

| Property | Type | Values or default |
|---|---|---|
| Value | Text | Member |
| State | Variant | Default, Focus, Error, Disabled |
| Direction | Variant | LTR, RTL |

Figma: [57:176](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=57-176)

### Switch

| Property | Type | Values or default |
|---|---|---|
| State | Variant | On, Off, On focus, Locked |

Figma: [57:233](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=57-233)

### Notification item

| Property | Type | Values or default |
|---|---|---|
| Title | Text | Omar Haddad accepted your invitation |
| Meta | Text | Northwind Traders · Members · 2 min ago |
| State | Variant | Unread, Read, New, Hover |

Figma: [57:261](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=57-261)

### Notifications popover

Item text comes from the API; the screens write no notification sentence (F57).

| Property | Type | Values or default |
|---|---|---|
| View | Variant | Unread, All, Empty, Loading, Error, Offline, New, End, All read |

Figma: [57:733](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=57-733)

### Sidebar

The main menu is the product’s: Home first, then the product’s own sections (the example module shows one as a real row right after Home), then Settings at the bottom with the account under it. Administration lives in the Settings layout (Layout=Settings): My account, Organisation. A page a person cannot open is not in their menu.

| Property | Type | Values or default |
|---|---|---|
| Organisation | Text | Northwind Traders |
| Role | Text | Owner |
| Members count | Text | 12 |
| Roles count | Text | 6 |
| Notifications count | Text | 3 |
| Account name | Text | Ayşe Demir |
| Initials | Text | AD |
| Switcher | Boolean | True |
| Unread | Boolean | True |
| Roles | Boolean | True |
| Activity | Boolean | True |
| Billing | Boolean | True |
| Page | Variant | Members, Roles, First load, None, Profile, Account and security, Home, General, Billing, Activity log, Usage |
| Layout | Variant | Expanded, Settings, Settings rail, Settings RTL |

Figma: [57:1309](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=57-1309)

### Top bar

| Property | Type | Values or default |
|---|---|---|
| Crumb 1 | Text | Northwind Traders |
| Crumb 2 | Text | Members |
| Direction | Variant | LTR, RTL, Loading |

Figma: [57:1359](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=57-1359)

### Phone top bar

| Property | Type | Values or default |
|---|---|---|
| Title | Text | Members |
| Notifications count | Text | 3 |
| Unread | Boolean | True |

Figma: [57:1363](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=57-1363)

### Phone drawer

Phone drawer: the same main menu as the desktop. The main menu is the product’s: Home first, then the product’s own sections (the example module shows one as a real row right after Home), then Settings at the bottom with the account under it. Administration lives in the Settings layout (Layout=Settings): My account, Organisation. A page a person cannot open is not in their menu.

| Property | Type | Values or default |
|---|---|---|
| Organisation | Text | Northwind Traders |
| Role | Text | Owner |
| Members count | Text | 12 |
| Roles count | Text | 6 |
| Account name | Text | Ayşe Demir |
| Initials | Text | AD |
| Switcher | Boolean | True |
| Roles | Boolean | True |
| Activity | Boolean | True |

Figma: [57:1381](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=57-1381)

### Member card

| Property | Type | Values or default |
|---|---|---|
| Name | Text | Ayşe Demir |
| Email | Text | ayse@northwind.example |
| Initials | Text | AD |
| You | Boolean | True |

Figma: [57:1443](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=57-1443)

### Phone drawer · Settings

Phone drawer inside Settings: “← Back” at the top, then My account and Organisation, as the desktop Settings layout.

| Property | Type | Values or default |
|---|---|---|
| Organisation | Text | Northwind Traders |
| Role | Text | Owner |
| Members count | Text | 12 |
| Roles count | Text | 6 |
| Account name | Text | Ayşe Demir |
| Initials | Text | AD |
| Switcher | Boolean | True |
| Roles | Boolean | True |
| Activity | Boolean | True |

Figma: [142:901](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=142-901)

### Platform sidebar

The platform’s menu. Main: Starter · Platform (no switcher), search, Notifications, Organisations, Users, Plans, Activity log; Settings at the bottom; the account under it. Settings layout: “← Back to the platform”, My account (Profile, Account and security), Platform (System admins, Terms of use, Feature flags).

| Property | Type | Values or default |
|---|---|---|
| Page | Variant | Organisations, Users, Plans, Activity log, None, Profile, Account and security, System admins, Terms of use, Feature flags, Sign-up |
| Layout | Variant | Main, Settings |

Figma: [166:1660](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=166-1660)

### Platform phone drawer

Platform phone drawer. Main menu of the platform on a phone: Organisations, Users, Plans, Activity log; Settings at the bottom; the account and "Version a1b2c3d" last. Opens from the menu button in the Phone top bar.

| Property | Type | Values or default |
|---|---|---|
| Organisation | Text | Starter Platform |
| Role | Text | Owner |
| Members count | Text | 12 |
| Roles count | Text | 6 |
| Account name | Text | Deniz Kaya |
| Initials | Text | DK |
| Switcher | Boolean | True |
| Roles | Boolean | True |
| Activity | Boolean | True |

Figma: [258:1197](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=258-1197)

### Platform phone drawer · Settings

Platform phone drawer · Settings. Inside Settings: "Back to the platform", My account (Profile, Account and security), Platform (System admins, Terms of use, Feature flags, Sign-up), the account and the version.

| Property | Type | Values or default |
|---|---|---|
| Organisation | Text | Starter Platform |
| Role | Text | Owner |
| Members count | Text | 12 |
| Roles count | Text | 6 |
| Account name | Text | Deniz Kaya |
| Initials | Text | DK |
| Switcher | Boolean | True |
| Roles | Boolean | True |
| Activity | Boolean | True |

Figma: [258:1265](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=258-1265)

### Phone list row

Phone list row. On a phone each table row becomes one list row: the name, the one most useful value, and the status label. A tap opens the detail page. No horizontal scrolling (Nielsen Norman Group, "Mobile Tables"). The Status instance takes any Pill tone or can be swapped for a Label.

| Property | Type | Values or default |
|---|---|---|
| Title | Text | Northwind Traders |
| Value | Text | 10 members · 2 invited |
| Status | Boolean | True |

Figma: [258:1389](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=258-1389)

### Language list

The list of the language switcher (Combobox, variant compact): a search field, then each language in its own name and script, the current one checked. No match: “No language matches.” Arabic reads right to left in its row.

| Property | Type | Values or default |
|---|---|---|
| State | Variant | Open, No match |

Figma: [295:1208](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=295-1208)

### Auth footer

The footer of every screen before sign-in, in both apps: sign-in, the code, two-step verification, sign-up and invitation acceptance. Left: the language switcher of packages/ui (sign-in/language-switcher.tsx), a compact combobox with a globe icon and a search; each language shows in its own name and script; a choice reloads the page in that language. Right: “Terms” opens the link of the terms version in force. The app reads it from the API (a public endpoint: the terms in force), the same version that the platform’s Terms of use manages, so the text a person reads is always the version they accept. There is no termsUrl setting. “Privacy” opens the brand’s privacyUrl (packages/brand), a fixed setting, because the privacy policy has no versions. No cookie banner: the apps set only strictly necessary cookies. Direction=RTL mirrors the row: the links on the left, the language switcher on the right, the globe after the name; the texts are Arabic (العربية, الشروط · الخصوصية).

| Property | Type | Values or default |
|---|---|---|
| Language | Text | English |
| Direction | Variant | LTR, RTL |

Figma: [299:25025](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=299-25025)

### List more

The one paging pattern of every long list in both apps: Members, Invitations, Roles, Organisations, Users, Feature flags, System admins, Terms of use versions, Notifications and the Activity logs. The list shows its first page; the count in the tab is the total. Idle: “Show more” under the last row adds the next page below it. Loading: the button is disabled and reads “Loading more…”; the rows already shown stay. Failed: “Couldn’t load more” and “Try again”; the rows already shown stay. When everything is shown, there is no button. The Activity logs label the button “Load older activity”.

| Property | Type | Values or default |
|---|---|---|
| State | Variant | Idle, Loading, Failed |

Figma: [306:42712](https://www.figma.com/design/3Ev111okX8xCBwR9B3hyBM/?node-id=306-42712)
