# Design follow-ups

This document lists the changes that the Figma file still needs, so that its frames draw what the code builds. The file's key is in `docs/roadmap.md`, section 2. A design thread takes items from this list, and its pull request removes each item it fixes. Step 34's handoff package comes into this folder later.

## 1. Open items

- The Activity frames still say "invited someone". After #280 they show the invitee's address, for a viewer who holds `tenant.invitations.read`; any other viewer reads "someone" (`docs/decisions.md`, section 2).
- 37c-design (#265) did not compare Tabs, EmptyState and the Combobox filter with the code.
- Platform 12.15 and 12.30 (couldn't load) show the user-plus icon in the empty state. An error state needs the error icon of the other "couldn't load" frames.
- Frame 9.06 (a limit of 0) and frame 4.15 (the current plan chosen) do not draw the states that #279 built. #279's description lists the new texts.
- 14.02 draws the invite dialog's roles as a dropdown with short descriptions ("Free. Uses no seat."); the built dialog keeps the radio list of 8.26 with each role's description, and 14.03 draws the Billing manager chosen in a dropdown too.
- 14.13 (the phone's settings menu of a Billing manager) lists Roles and Activity log, which the role cannot open (F59); the built menu shows General and Members, and Billing and Usage from 40b and 40c.
- 14.01 counts Members "1 of 4" with Manage Owners; a role that is not the Owner's has no Manage Owners row (9.10), so the built page says "1 of 3".
- 14.08 gives Members and Settings their own sentences for a Billing manager ("See who is in Northwind Traders.", "Name and address. Read only."); Home's cards keep one sentence each for every role (2.x).
- 9.14 and 9.18 draw no banner on Roles at the limit; F56 shows the limit's banner there, as Members' 8.42 does.
- 6.13 says "2 months free." beside €100 a year while the monthly fee is edited to €12; the built text counts against the edited fee, 3 months.
