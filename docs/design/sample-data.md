# Sample data

The one world every screen uses. The names, counts and amounts below appear on the screens word for word. Code tests and mocks can use the same data.


## Sample data set · one source for every screen

### Every screen, in both apps, uses these values. A screen that shows another moment (a new organisation, a limit reached) says so in its label and changes only what that moment changes.

## Organisation
| Name, slug | Northwind Traders · northwind |
|---|---|
| Created | 12 Mar 2026 (by the platform; Owner invited the same day) |
| Status | Active |
| Plan | Team |
| Seats | 15 per the plan (members and pending invitations count); used 12 of 15 = 10 members + 2 pending invitations; tooltip: “10 members and 2 pending invitations”; 1 billing manager (Grace Okafor) is free and uses no seat. Seat summary: “12 of 15 seats: 10 members and 2 pending invitations. 1 billing manager, free.” |
| Custom roles | Team plan gives 5; override 10 until 31 Dec 2026 (platform, reason: pilot); used 4 of 10 |
| Roles | 8 = Owner, Admin, Billing manager, Member + 4 custom (Support lead, Auditor, Role manager, Onboarding); built-in permissions (BuiltInRoles.cs): Owner 14, Admin 9, Billing manager 3, Member 2; custom: Support lead 5 (details, members, invitations, invite, roles), Auditor 2 (see members, see the Activity log: tenant.members.read, tenant.audit.read; not in use), Role manager 7 (details, members, invitations, roles, create, edit and delete roles; held by Liam O’Connor), Onboarding 6 (details, members, invitations, invite, revoke, roles) |
| Other organisations | Pacific Coast Freight & Warehousing Cooperative Ltd. (Admin) · شركة الأفق للتجارة (Member, the Arabic journey) · Blue Harbour Logistics (Member, suspended) |
| Platform: organisations | 6 live and 1 deleted, on the Team plan except Pacific Coast (Business): Northwind Traders · northwind · Active · 10 members + 2 pending invitations · created 12 Mar 2026 \| Pacific Coast Freight & Warehousing Cooperative Ltd. · pacific-coast · Active · 24 members · 3 Feb 2026 \| شركة الأفق للتجارة · al-ufuq · Active · 8 members · 18 Apr 2026 \| Blue Harbour Logistics · blue-harbour · Suspended (reason: unpaid invoices) · 5 members · 9 Jan 2026 \| Fernwood Studio · fernwood · Closed, can be reopened until 1 Dec 2026 · 3 members · 2 Jun 2026 \| Old Mill Bakery · old-mill · Being deleted · 2 members · 15 May 2026 \| Greyfield Books · greyfield · Deleted on 12 Oct 2026 · created 4 Feb 2026 \| Owners: Pacific Coast: Rosa Delgado; al-ufuq: يوسف بن خالد; Blue Harbour: Aiko Tanaka; Fernwood: Jonah Reyes; Old Mill: Ruth Okoye \| New in 4.a07: Harbourside Dental · harbourside-dental · Owner invited: maya.chen@harbourside.example |
| Platform: plans | Free (default, preselected when creating an organisation): €0 base, €0 per person, 3 seats, no custom roles · Team: €0 base, €10 per person, 15 seats, custom roles on, 5 custom roles · Business: €99 base, €8 per person, 50 seats, custom roles on, 20 custom roles · Plans list: Free 1 organisation (Harbourside Dental), Team 5, Business 1; deleted organisations are not counted; all created 1 Jan 2026 · Moment screens: Team edited to 6 seats puts Northwind Traders (12) and al-ufuq (8) over; new plan “Nonprofit”. |
| Platform: users | 53 in all: 51 active, 2 suspended. Ayşe Demir · Owner of Northwind Traders, Admin of Pacific Coast, Member of al-ufuq and Blue Harbour · signed up 12 Mar 2026 \| Omar Haddad · Admin of Northwind \| Rosa Delgado · Owner of Pacific Coast \| Aiko Tanaka · Owner of Blue Harbour \| Mark Ellis · mark@pacificcoast.example · Member of Pacific Coast · suspended on 25 Oct 2026 (reason: shared account) \| Nadia Petrov · nadia@fernwood.example · Member of Fernwood · suspended \| Deniz Kaya · tag “System admin”, in no organisation (0) · System admins: Selin Arslan (selin@platform.example, Root, from the configuration), Deniz Kaya (added 1 Jan 2026), Hannah Weber (hannah@platform.example, added 4 Sep 2026) · Terms of use versions: 12 Jan 2026 (notice only, replaced), 1 Mar 2026 (current, everyone accepted), 1 Dec 2026 (scheduled, requires acceptance, 18 of 53 accepted). |

## People (10 members and 1 billing manager)
| Owner | Ayşe Demir · ayse@northwind.example (the signed-in person on most screens) |
|---|---|
| Admins | Omar Haddad · omar@ · Jonas Berg · jonas@ |
| Custom role | Kenji Sato · kenji@ (Support lead) · Liam O’Connor · liam@ (Role manager) |
| Billing manager | Grace Okafor · grace@northwind.example · joined 20 Oct 2026 · free, uses no seat |
| Members | Lena Fischer · Maria Santos · Sofia Rossi · Priya Nair · Tomás García (all @northwind.example) |
| Pending invitations (2) | david.okafor@harbour.example (Member, by Ayşe Demir, expires 15 Nov 2026) · ines.moreau@northwind.example (Admin, by Omar Haddad, expires 3 Nov 2026, “Expires soon”) |
| Edge people | Maximiliane Alexandra von Hohenzollern-Sigmaringen (long name, the 500 screen) · عبد الرحمن الهاشمي, فاطمة الزهراء القحطاني, يوسف بن خالد (Arabic) |
| Platform | Deniz Kaya · deniz@platform.example (system admin) |

## Counts and moments
| Sidebar | Members 11 · Roles 8 · Notifications 2 unread |
|---|---|
| Only you | 1 member, 0 invitations, 1 of 15 seats |
| Limit reached | 13 members + 2 invitations = 15 of 15 seats |
| Over the limit | 15 members + 2 invitations = 17 of 15 seats (after a plan change) |
| 500 | 500 members + 48 invitations, no seat limit |
| Notifications | Omar Haddad accepted your invitation (2 min) · Your role in Northwind Traders changed to Admin (1 h) · The plan of Northwind Traders changed to Team (yesterday) · New sign-in to your account (3 Oct) |
| Dates | Today 1 Nov 2026; format 12 Mar 2026 |

### Billing
Northwind Traders: Team, monthly, card (Visa ending in 4242). Next payment 1 Dec 2026, €100 excl. VAT (€10 per person × 10 people). Invoices: €100.00. Billing manager: Grace Okafor, grace@northwind.example, free. Billing email accounts@northwind.example. AI requests 7,200 of 10,000.
شركة الأفق للتجارة: Business trial, 22 Oct to 5 Nov 2026 (3 days left). Owner: Yusuf Al-Amin, yusuf@al-ufuq.example. 8 members.
Pacific Coast Freight & Warehousing Cooperative Ltd.: Contract, Business, set by a system admin.
Kestrel Analytics (Elif Yıldız, self-service sign-up on 2 Nov): Business trial until 16 Nov 2026.
Web: each billing state is drawn on Northwind Traders; a person sees only their own organisation. Platform: Lindqvist Design Studio (Team, payment retrying since 1 Nov, 6 members), Marrow & Vine Catering (Team, grace until 20 Nov, 4 members), Copperleaf Tutoring (Free, 100 of 100 AI requests, 3 members), Harbourside Dental (Free, waiting for its Owner). 10 organisations, 66 users (64 active).
Plans (sample prices, excl. VAT). Each plan has a base fee and a per-person fee, each monthly and yearly; either can be 0. Free: €0 base, €0 per person, 3 seats, no custom roles, 100 AI requests then stop. Team: €0 base, €10 a month or €100 a year per person, 15 seats, 5 custom roles, 10,000 then €1 per 1,000. Business: €99 a month or €990 a year base, plus €8 a month or €80 a year per person, 50 seats, 20 custom roles, 50,000 then €1 per 1,000. For 10 people: Team €100 a month or €1,000 a year; Business €179 a month or €1,790 a year. Upgrade Team to Business on 1 Nov: €53.25 for the rest of the month. A join on a yearly Team plan with 10 months left: €83.33. Price change sample: Team per-person fee €10 → €12 from 1 Feb 2027 (Lindqvist 6 people: €60 a month; Marrow & Vine 4 people: €40 a month).
Built-in roles: Owner 14 permissions, Admin 9, Billing manager 3 (organisation details, see members, manage billing; free, uses no seat, can’t use the product), Member 2. Manage billing (tenant.billing.manage): Owner and Billing manager. No custom role in the sample has it; a custom role that holds it uses a seat.

