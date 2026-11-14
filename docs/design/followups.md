# Design follow-ups

This document lists the changes that the Figma file still needs, so that its frames draw what the code builds. The file's key is in `docs/roadmap.md`, section 2. A design thread takes items from this list, and its pull request removes each item it fixes. Step 34's handoff package comes into this folder later.

## 1. Open items

- The Activity frames still say "invited someone". After #280 they show the invitee's address, for a viewer who holds `tenant.invitations.read`; any other viewer reads "someone" (`docs/decisions.md`, section 2).
- 37c-design (#265) did not compare Tabs, EmptyState and the Combobox filter with the code.
- Frame 9.06 (a limit of 0) and frame 4.15 (the current plan chosen) do not draw the states that #279 built. #279's description lists the new texts.
