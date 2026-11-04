---
name: building-screens-from-design
description: Builds a screen of apps/web or apps/platform from its Figma frames, and proves it with a screenshot of each frame. Use when a task builds or changes a screen, a state or a shared component that the Figma design draws, before the first code edit.
metadata:
  origin: repository
---

# Building screens from the design

A screen is done when its screenshot matches its frame, not when the code compiles. The comparison runs in the first pull request of the screen, before the report, never in a later one.

The rules of the frontend stay in their documents. This skill gives the order of the work. Read `docs/frontend/AGENTS.md` first.

## Checklist

Copy this list into your work and tick each item.

```
Screen progress:
- [ ] 1. Load the Figma plugin's skills
- [ ] 2. List every frame of the task
- [ ] 3. Write the value table; stop on a value without a source
- [ ] 4. Write the tests first
- [ ] 5. Build with packages/ui and the tokens
- [ ] 6. Add one shot for each frame
- [ ] 7. Compare each shot with its frame; fix; compare again
- [ ] 8. Post the frame table on the pull request
```

## 1. Load the Figma plugin's skills

Load `figma:figma-design-to-code` before the first `get_design_context` call, as the Figma MCP server asks. Follow it for the calls; this skill does not repeat it. Without the plugin, read the skills that the Figma MCP server serves.

The design is one Figma file for both apps. Its key is in `docs/roadmap.md`, step 34. The file has a page for each app ("Web app", "Platform app"). Each page has numbered sections ("3 · Notifications"), and each section has numbered frames (3.01).

Each call counts against the Figma seat's daily limit. One `get_screenshot` of a section shows all its frames. Use `get_design_context` for the frames whose exact sizes you build.

## 2. List every frame of the task

Read the app's page with `get_metadata` once, to find the node of each section that the task touches. Then read each of those sections with `get_metadata` on its node. List every frame of those sections: each state, size, theme and language (empty, loading, error, offline, phone, tablet, dark, Arabic). A frame that belongs to another step gets its step number, not silence. When the design has no frame for a language or a theme that the screen has (Arabic, dark), the shot of step 6 is compared with the frame of the other language or theme, mirrored or in its theme, and the row says so.

Read each frame of the list before you build. A list of frames that you did not read is not a list.

## 3. Write the value table

For each frame, write every value it shows and where it comes from.

| Frame | Value | Source |
| --- | --- | --- |
| 3.01 | "Northwind Traders" | `NotificationResponse.tenantName` |
| 3.01 | "Members" | `NotificationResponse.area` |
| 3.01 | "Mark all as read" | message key `notifications.markAll` |
| 3.01 | unread row fill | token `primary-subtle` |

A source is an API field of `apps/api/openapi/v1.json`, a message key, or a token of `packages/ui/tokens/tokens.json`. A size or a mark that has no token follows the component of `packages/ui` that already draws it. A screen writes no sentence that the API owns (`docs/frontend/AGENTS.md`, "Source of truth").

When no source gives a value, stop before the tests that depend on it. Do not invent the value, do not hard-code it, and do not leave it out silently. Report it to the maintainer as one question with options, a recommendation and a source (`docs/frontend/AGENTS.md`, "Decisions"). Typical cases:

- The frame shows a field that the API does not answer. Option: a backend change first.
- The frame shows a notification type, a role or a state that the catalog does not have.
- The frame contradicts a rule of `docs/frontend/ARCHITECTURE.md`. The rule wins until the maintainer decides.
- The frame contradicts the task or its line in `docs/roadmap.md`.

The sample text of a frame is sample data, not a value to copy. The API's title stays the API's title.

## 4. Write the tests first

The test rules are in `docs/frontend/ARCHITECTURE.md`, section 6, and in "How we test" of `docs/frontend/AGENTS.md`.

## 5. Build

Use the components of `packages/ui`. Use the tokens; never a raw colour, size or breakpoint (F20). A component that the frame needs and `packages/ui` lacks is built there, not inside the app.

## 6. Add one shot for each frame

Each frame of the list gets a test in the app's `e2e/screenshots.spec.ts`:

- Name the shot after the frame, as the spec names its other shots (`shell-3.01-notifications-en-light-desktop`).
- Set the viewport to the frame's width and height. Use a size of the spec when it matches; otherwise add the frame's size.
- Give the screen the frame's data: the names, the counts and the states that the frame draws. Set them in the mocks of `e2e/api.ts`. Other data hides real differences.
- Bring the screen into the frame's state with the mocks and with Playwright: a slow or failed answer for loading and error, `context.setOffline` for offline, a key press for focus, a failed command for its toast.
- Wait until the screen is still: an open popover or a sheet that slides in must reach its end before the shot.

A frame gets "no shot" only when Playwright cannot reach its state, for example a frame of another step. A state that takes work to reach is not a reason.

How to take the shots and where they go is in "How we work" of `docs/frontend/AGENTS.md`. Never commit a shot.

If the shots cannot run locally (they need the e2e secrets), run the `architecture-reviewer` first, as `docs/frontend/AGENTS.md` asks before a pull request opens. Then push the branch and open the pull request as a draft, so CI takes the shots, and download the artifact `screenshots` of that run. Mark the pull request ready after the frame table.

## 7. Compare, fix, compare again

Put each shot beside its frame: the section's `get_screenshot`, and `get_design_context` of a frame that differs. Check the layout, the spacing, the type, the colours, the icons and the texts. For each difference, decide:

- It is a fault of the code. Fix it, take the shot again and compare again.
- It comes from a rule or a decided deviation. Name the rule or the deviation row.
- It is a value without a source. It is in the questions of step 3.

Repeat until each frame is "matches" or has a reason.

## 8. Post the frame table on the pull request

Post the table as a comment on the pull request after the last comparison. Name the CI run and the head commit of the shots.

| Frame | Shot | Result |
| --- | --- | --- |
| 3.01 · Popover · unread | `shell-3.01-notifications-en-light-desktop` | matches |
| 3.05 · Popover · error | `shell-3.05-notifications-error-en-light-desktop` | differs, because the error also shows the API code's message and the reference (F4) |
| 3.17 · Billing | none | no shot, because billing comes with step 40 |

Each frame of step 2 has one row. The result is one of three forms: "matches", "differs, because ..." or "no shot, because ...". A difference that the pull request does not fix also goes into "Skipped, not checked, risks" of the body.
