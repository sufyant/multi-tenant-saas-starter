# Patterns

Rules every screen follows. A screen in web/ or platform/ shows them; this file says why and how. Read it before you build a screen.


## Screen sizes

### Three sizes. Every component follows the rule of its size; only sign-in, accepting an invitation, notifications, My account and the members list get a phone design of their own. The platform app is designed for desktop and tablet; on a phone it must not break (no horizontal page scroll, wide tables scroll inside themselves), but it gets no phone design.

## Desktop
1280 px and wider  ·  breakpoint/desktop
•  The main design.
•  Sidebar open with labels (rail/expanded, 232 px).
•  Details panel open at the right (280 px), collapsible.
•  Tables show every column.
•  Click targets 24 px or more; row actions 28 px.

## Tablet and small laptop
768 to 1279 px  ·  breakpoint/tablet
•  The sidebar shrinks to icons (rail/width, 56 px); each label is the icon's tooltip and accessible name.
•  The details panel moves below the content.
•  Tables keep their columns; a table wider than the page scrolls inside itself.
•  Touch targets 48 px (target/touch-tablet) for icon buttons and rows.

## Phone
767 px and narrower  ·  below breakpoint/tablet
•  The sidebar hides behind a menu button and opens as a drawer.
•  Main lists (members) become cards.
•  Wide admin tables scroll sideways; the first column stays.
•  Dialogs fill the width with 16 px margins; their buttons stack.
•  Touch targets 44 px (target/touch).
•  No tooltips: touch has no hover.

## Each component at each size
| Component | Desktop · 1280 px + | Tablet · 768 to 1279 px | Phone · up to 767 px |
|---|---|---|---|
| Sidebar | Open with labels, 232 px. The organisation switcher on top, the account button at the foot. | Icons only, 56 px. A label shows as a tooltip on hover and on keyboard focus. | Hidden. A menu button (44 px) in the top bar opens it as a drawer over a scrim; Esc, the scrim or a link closes it. The account menu opens from the drawer's foot. |
| Top bar | Breadcrumb, then the page's own controls, then the bell. | The same. | Menu button, page title, bell. The breadcrumb hides; the page title is the last crumb. |
| Details panel | At the right, 280 px; Collapse details hides it and the choice is kept. | Below the content, full width; its fields in three columns. | Below the content, full width; its fields in one column. |
| Main lists (members) | Table: 40 px rows, every column. | Table: the Joined column hides first. | Cards: avatar, name, email, role pill, a 44 px actions button. A tap opens the person. |
| Wide admin tables (platform, plan table) | Table, every column. | Table; scrolls inside itself when wider than the page. | Scrolls sideways inside itself; the first column stays. The page never scrolls sideways. |
| Toolbar (search, filters, selection) | One row: search, filters, then the selection bar at the right. | One row; the search narrows first. | Search full width; filters behind one Filter button that opens a sheet; the selection bar sits at the foot of the screen. |
| Dialogs and confirmations | Centred, 440 px. | Centred, 440 px. | Full width with 16 px margins; buttons full width, stacked, the main action on top. |
| Menus (account, row actions) | A popover next to its button. | A popover; rows 48 px tall. | A popover anchored to its button; rows 44 px tall. |
| Bell | A popover under the bell. | A popover under the bell. | Full screen: Close or Esc closes it. |
| Command menu | Centred near the top, 460 px; opens with ⌘K or the search field. | The same. | Full screen from the search button; no key hints. |
| Tooltips | Tooltip on hover and focus; ? opens the list. | The same with a keyboard; a touch shows no tooltip. | None. Every icon button has a visible or accessible name. |

## Examples
Phone · members as cards
|  | Members | 3 |
|---|---|---|
People in Northwind Traders and what they can do.
Invite member
| Members 12 | Invitations 3 |
|---|---|
| Search | Filter |
AD
| Ayşe Demir | you |
|---|---|
ayse@northwind.example
Owner
OH
Omar Haddad
omar@northwind.example
Admin
LF
Lena Fischer
lena@northwind.example
Member
KS
Kenji Sato
kenji@northwind.example
Support lead
MS
Maria Santos
maria@northwind.example
Member
Phone · sidebar as a drawer
|  | Members | 3 |
|---|---|---|
People in Northwind Traders and what they can do.
Invite member
| Members 12 | Invitations 3 |
|---|---|
| Search | Filter |
AD
| Ayşe Demir | you |
|---|---|
ayse@northwind.example
Owner
OH
Omar Haddad
omar@northwind.example
Admin
LF
Lena Fischer
lena@northwind.example
Member
KS
Kenji Sato
kenji@northwind.example
Support lead
MS
Maria Santos
maria@northwind.example
Member
| N | Northwind Traders Owner |  |
|---|---|---|
Switch organisation
Northwind Traders
Overview
|  | Members | 12 |
|---|---|---|
|  | Roles | 6 |
Activity
Settings
| AD | Ayşe Demir |  |
|---|---|---|
Tablet · 1024 px · icon rail, details below
P
DK
| Organisations | › | Northwind Traders |
|---|---|---|

### N
| Northwind Traders | Active |
|---|---|
Organisation · Team plan
| Rename | Actions |
|---|---|
| Overview | Members 12 | Activity |
| Plan | Add override Change plan |
| Feature or limit | Team plan | Override | Used |
| Custom roles | On | — | — |
| Custom roles limit | 5 | 10 until 31 Dec 2026 | 4 of 10 |
| Members limit | 15 | — | 12 of 15 |
Details
| Status Active | Slug northwind | Owner Ayşe Demir | Plan Team | Created 12 Mar 2026 | Last active Today, 09:41 |
|---|---|---|---|---|---|
[Environment strip] Staging



## Voice and tone

## 1. Say what happened, then what to do.

### “Couldn’t load members.” + Try again · “Daily limit reached. You have reached today’s invitation limit.”

## 2. Buttons are a verb and the thing, in sentence case.

### “Invite member”, “Send invitation”, “Remove member”, “Close organisation”. No “OK”, no “Submit”, no exclamation marks, no “please” or “oops”.

## 3. Speak to “you”; name people and organisations; a refusal names who can help.

### “Only an Owner can remove an Owner. Ask an Owner of Northwind Traders.”

## 4. State the consequence exactly, and say what is kept.

### “They lose access at once. Their past activity stays in the log.” · “Nobody loses access.” · “Your data is safe.”

## 5. Short sentences, one idea each; empty states say what will appear and how.

### “People you invite appear here until they join or the link expires.” British spelling (organisation), dates as 12 Mar 2026, counts as 12 of 15.

## Data for every drawing
| Case | Value | Where it tests |
|---|---|---|
| Usual names | Ayşe Demir, Omar Haddad, Lena Fischer, Kenji Sato, Maria Santos, Liam O’Connor, Sofia Rossi | Every list and avatar |
| Long name | Maximiliane Alexandra von Hohenzollern-Sigmaringen | Truncation with an ellipsis and a tooltip; never wraps a table row |
| Long email | maximiliane.von-hohenzollern@internationale-handelsgesellschaft.example | Email column, invite dialog, cards on a phone |
| Long organisation | Internationale Handelsgesellschaft Nordwind GmbH & Co. KG | Organisation switcher, breadcrumb, record header, dialogs |
| Arabic names | عبد الرحمن الهاشمي · فاطمة الزهراء القحطاني · يوسف بن خالد | Mixed with Latin in one list; avatars take the first letters |
| Arabic organisation | شركة الأفق للتجارة | The right-to-left journey (Members, fully mirrored) |
| Slugs | northwind · internationale-handelsgesellschaft-nordwind · ufuk | Slug field, details panel, typing to confirm |
| 0 records | No members but you; no invitations; no activity | Empty states |
| 12 records | 12 members, 3 invitations, 15-member limit | The ideal state |
| 500 records | 500 members, 48 invitations, no limit | Paging, search, the partial state (first 50 shown, more on scroll) |
| Plan edge | 15 of 15 (limit reached) · 17 of 15 (over the limit) | Invite disabled; the over-limit banner |
Arabic type: Noto Sans Arabic · page-title-ar 22/34 · body-ar 14/24 · status-ar 12/18

## الأعضاء
الأشخاص في شركة الأفق للتجارة وما يمكنهم فعله.
مالك · مسؤول · عضو

### Brand settings · packages/brand/brand.json
name: “Starter”. The name a person sees.
supportUrl: mailto:support@starter.example. Where a person asks for help: “Contact support”, “Contact us”.
privacyUrl (new): https://starter.example/privacy (an example). The privacy policy on the marketing site; “Privacy” in the Auth footer.
statusUrl (new, optional): for example https://status.starter.example. When set, the 503 screen and every “Contact support” show “Check the status page”. When not set, nothing shows.
The privacy policy lives on the marketing site, at privacyUrl. There is no termsUrl: “Terms” opens the link of the terms version in force, which the app reads from the API. No cookie banner: only strictly necessary cookies.



## Patterns · every screen follows these

### Dates and times
In the person’s time zone (set in Profile). On screen: the date and the local time. On hover and on keyboard focus: the full date, the zone and its UTC offset.
1 Nov 2026, 14:32
[Tooltip] 1 Nov 2026, 14:32 · Irish time (UTC+0)

### Relative time
“2 min ago”, “1 h ago”, “Yesterday” in notifications and recent items; after 7 days, the date. The hover always gives the full date. The Activity log always shows the full date and time: a record must be exact.
2 min ago
1 h ago
Yesterday
28 Oct
[Tooltip] 31 Oct 2026, 16:40 · Istanbul time (UTC+3)
| Activity log | 1 Nov 2026, 10:24 |
|---|---|

### A deleted person
Shown as “Deleted user”, in the muted colour, with the person icon instead of initials. The same look in lists, the Activity log and notifications.
[Icon/user]
Deleted user

### An empty value
A value that is not set shows “—” in the muted colour. Never “N/A”, “null” or a blank.
| Last sign-in | — |
|---|---|

### Long text
A translation is often longer than the English, and short text grows the most: a label under 10 characters can double or triple (W3C, “Text size in translation”). The design follows rules, so every language fits, not only the ones drawn.
| Where | Rule |
|---|---|
| Every screen | Designed for text 40% longer than the English. |
| Buttons | Never cut. The button grows. On a phone, buttons stack at full width. |
| Menus and tabs | One line. Cut with “…”, the full text in a tooltip. |
| Table headers | At most two lines. Cells cut with “…”, the full text in a tooltip. |
| Titles | At most two lines, then “…”. |
| Help text, form text, descriptions | Wrap freely. No box has a fixed height. |
| Pill and Label | One line, never cut. Keep their words short. |
| Names and emails | Cut at the end with “…”, never in the middle; the full text on hover and on keyboard focus. |
Maximiliane Alexandra von Hohenzollern-Sigmaringen
[Tooltip] Maximiliane Alexandra von Hohenzollern-Sigmaringen

### Numbers
The language’s separators. Western digits in every language, Arabic too.
| English | 1,234 · 12 of 15 |
|---|---|
| Türkçe | 1.234 · 15 üzerinden 12 |
| العربية | 12 من 15 · 1,234 |

### Copy
A small copy button beside an address or an email. After a click, the button says “Copied” for 2 seconds; a screen reader hears it too.
app.starter.example/northwind
[Button]
| app.starter.example/northwind | Copied |
|---|---|

### Confirmation and success
Confirm only what can’t be undone. The dialog title asks the question with the name (“Remove Omar Haddad?”), the text states the consequence, the button repeats the action (“Remove member”, never “OK”). Closing an organisation or deleting an account asks to type its address or email. After success: a short toast at the bottom right, the change already visible on the page.

### Remove Omar Haddad?
Omar Haddad loses access to Northwind Traders at once. Their past activity stays in the Activity log.
| Cancel | Remove member |
|---|---|
[Toast] Member removed · Omar Haddad no longer has access.

### The page menu (⋯)
Secondary actions of a page sit in a ⋯ menu, always right beside the page title. The main action keeps its button at the right. A page without a secondary action shows no ⋯; a menu is never empty. A secondary action that can’t run stays in the menu, disabled, with its reason. Today: Members and Activity log (Export CSV).

### Organisation status
One colour per status, the same in both apps: Active green, Suspended amber, Closed grey, Being deleted red. Deleted stays grey in lists and the Activity log. The colour never stands alone: the word is always there.
| Active | Suspended | Closed | Being deleted | Deleted |
|---|---|---|---|---|

### Pill tones
Colour means a status. Roles and other plain facts use the neutral outlined label. Blue (Info): scheduled, or new information. Amber (Warning): needs attention, or suspended. Green (Success): active, current. Red (Danger): being deleted, or failed. Grey, filled (Neutral): ended, closed, replaced. Outlined label, no colour: Owner, Admin, Member, a custom role, System admin, a flag type.
| Scheduled | Suspended | Current | Being deleted | Replaced | Owner | Support lead |
|---|---|---|---|---|---|---|

### Button icons
A “+” only on a button that creates or adds something: “New role”, “Invite member”, “Add override”. Save, confirm and send buttons have no icon: “Save changes”, “Confirm”, “Send invitation”.

### App version
The version is the commit the image was built from (SOURCE_REVISION), the same value Sentry names as the release. It is not the v* release tag (docs/deployment.md, section 5). The last row of the account menu, in both apps, shows small grey text "Version a1b2c3d" (7 characters). A click copies it and shows "Copied". Staging and Production look the same. "Contact support" opens an email with "Version: a1b2c3d" in the body.
[Account menu · Version] Version a1b2c3d

### Dialogs on a phone
A form you type into opens as a full-screen page: a top bar with × on the left and the title, the primary button stays visible above the keyboard (Material 3, Full-screen dialogs). A short choice or a confirmation opens as a bottom sheet with a drag handle (Apple HIG, Sheets). Behind a sheet the page stays visible and dimmed by the overlay.
Usage stopped at the action
When a meter stops (the quota on Free, or the spending cap), the action that uses it stays visible but disabled. A short message sits under it and says when the meter resets. A person who manages billing also gets a link to change the plan, or the cap. Everyone else reads who can change it. Nothing is hidden and nothing is lost.
Manages billing
[Button] Summarise with AI
| You’ve used all 100 AI requests this month. They reset on 1 Dec. | Change plan |
|---|---|
Everyone else
[Button] Summarise with AI
You’ve used all 100 AI requests this month. They reset on 1 Dec. Someone who manages billing can change the plan.
Billing bar priority
One bar at a time, under the top bar. A connection problem always comes first. Then, for people who manage billing: 1. Payment grace. 2. Payment failed. 3. Spending cap reached. 4. Quota used up on Free. 5. Quota used up on a paid plan. 6. Trial ends in 3 days. 7. 80% of a meter. Losing access comes before paying more; paying more comes before a reminder. A bar closes only when its cause goes away.
Billing is for billing managers
Billing, Usage, the billing bars and billing emails go only to people with “Manage billing” (tenant.billing.manage). The Owner and the Billing manager have it; a custom role can have it; the Admin does not. A page a person can’t open is not in their menu. At a limit, they read “Someone who manages billing can change the plan.” A Contract organisation shows plan and limits only, and “Request a plan change”.
Amounts
Prices and amounts to pay: excl. VAT. This covers prices, previews, overage and the spending cap. Invoices: the total paid, tax included, for example “€100.00”; the PDF shows the breakdown. Stripe adds the tax at payment and on the invoice. Prices: show the parts (base, per person × people), then the total, excl. VAT: “€99 base + €8 × 10 people = €179 a month, excl. VAT”. A billing manager is free and uses no seat. The dialog that adds, removes or changes a person says what it does to the bill. Euro with the sign first and a comma for thousands: €1,490. Yearly plans show the saving as “2 months free”.
Long lists
Every long list in both apps pages with the “List more” component: the first page, then “Show more” under the last row. Loading more keeps the rows shown and disables the button; a failure says “Couldn’t load more” with “Try again”. The tab count is the total, not the rows shown. No numbered pages and no endless scroll.
[List more] Show more
[Top bar] You · › · Notifications

## Notifications
Choose how you hear about activity.
| Activity log | Bell | Email |
|---|---|---|
A maintenance task is assigned to you
When someone assigns a maintenance task to you.
[Switch]
[Switch]
Security notices and notifications about your role, such as plan changes, always reach you.

## Long text check · every text 40% longer
A test, not a screen. The texts are German-length stand-ins for any long language. Each block shows one rule of “Long text” at work.
Sidebar
Menus: one line, cut with “…”, the full text in a tooltip.
[Sidebar] Zurück zu Northwind Traders GmbH & Co. KG · Mein Benutzerkonto · Persönliches Profil · Konto und Sicherheitseinstellungen · Organisation und Team · Allgemeine Einstellungen · Mitgliederverwaltung · 11 · Rollen und Berechtigungen · 8 · Abrechnung und Zahlungen · Nutzungsübersicht · Aktivitätsprotokoll der Organisation · AD · Ayşe Demir
Tabs
Tabs: one line, cut with “…”, the full text in a tooltip.
| Mitgliederverwaltung der Organisation 11 | Ausstehende Einladungen 2 |
|---|---|
Table header
Headers: at most two lines. Cells: one line, cut with “…”, the full text in a tooltip.
| Name der Person | E-Mail-Adresse des Benutzerkontos | Rolle in der Organisation | Beigetreten am |
|---|---|---|---|
| Maximiliane Alexandra von Hohenzollern-Sigmaringen | maximiliane.hohenzollern@northwind.example | Abrechnungsverwaltung | 12. März 2026 |
Title
Titles: at most two lines, then “…”.

## Abrechnungsübersicht, Zahlungsdetails und Rechnungen von Northwind Traders für das laufende Geschäftsjahr
Dialog · desktop
Buttons never cut: they grow. Title two lines, then “…”. Text wraps. The Pill stays on one line, never cut.

### Maximiliane Alexandra von Hohenzollern-Sigmaringen aus Northwind Traders entfernen?
[Pill] Ausstehende Einladung
Die Person verliert sofort den Zugriff. Ihre bisherigen Aktivitäten bleiben im Aktivitätsprotokoll. Sie können die Person später erneut einladen.
| Abbrechen und zurückkehren | Mitglied endgültig entfernen |
|---|---|
Dialog · phone
On a phone, the buttons stack at full width, the main action on top.

### Maximiliane Alexandra von Hohenzollern-Sigmaringen aus Northwind Traders entfernen?
[Pill] Ausstehende Einladung
Die Person verliert sofort den Zugriff. Ihre bisherigen Aktivitäten bleiben im Aktivitätsprotokoll. Sie können die Person später erneut einladen.
[Button] Mitglied endgültig entfernen
[Button] Abbrechen und zurückkehren

