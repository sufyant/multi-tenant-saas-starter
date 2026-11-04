---
name: starting-roadmap-steps
description: Prepares a step of docs/roadmap.md before its backend and frontend sessions start, so that every Figma frame has one pull request, every value has an API field and every open question is asked first. Use when a session is the first of a roadmap step, or when the maintainer asks to start, plan or split a step.
metadata:
  origin: repository
---

# Starting roadmap steps

A step goes wrong when a frame has no owner, a screen needs a field that the backend did not build, or a question comes up in the third pull request. This skill finds all three before the first line of code.

The plan is `docs/roadmap.md`. A step's pull request updates its own row and its own section (`AGENTS.md`, "The plan").

## Checklist

Copy this list into your work and tick each item.

```
Step start:
- [ ] 1. Read the step and its decided points
- [ ] 2. List every Figma frame of the step
- [ ] 3. Give each frame one pull request
- [ ] 4. Map the frames' values to API fields
- [ ] 5. Collect the open questions; ask them one at a time
- [ ] 6. Write the parts into the step's section
```

## 1. Read the step

Read the step's section of `docs/roadmap.md`. A decided point is closed: do not reopen it. Read the open pull requests too (`gh pr list`): another step's pull request can change this step's section. A point that only an open pull request writes is not decided yet: it is a question of step 5.

## 2. List every Figma frame of the step

Load `figma:figma-design-to-code` before the calls. The file's key is in `docs/roadmap.md`, step 34. Read each app's page with `get_metadata` once, to find the sections of the step; then read each of those sections with `get_metadata` on its node. List every frame that the step covers, in both apps: each state, size, theme and language.

## 3. Give each frame one pull request

Split the step into parts, one pull request each: by layer (backend, frontend), by app, or both, as the step's section says, and more when the frames are many. Then give each frame of step 2 one part. The rows below show the form; they are not real frames.

| Frame | Part | Note |
| --- | --- | --- |
| 8.08 · Export | 37a-frontend | waits for the export endpoint of 37a-backend |
| 8.09 · Export · phone | 37a-frontend | |
| 9.30 · Billing group | not this step | step 40 |

Each frame has exactly one row. A frame outside the step names its step. A frame that no step names is a question of step 5. The design is compared with the screens in the part that builds them, never later (the `building-screens-from-design` skill).

## 4. Map the frames' values to API fields

Write the value table of the `building-screens-from-design` skill (step 3) for every frame of the step, before the backend part starts. A state frame (loading, error, phone) lists only the values that differ from its main frame. The sources are `apps/api/openapi/v1.json`, `apps/api/docs/api-catalog.md` and `apps/api/docs/flows/what-the-screens-read.md`.

A value that no field gives becomes one of two things:

- Backend work: a line in the backend part, with the frame that needs it.
- An open question, when the value contradicts a rule or a closed decision.

## 5. Collect the open questions

Write every question of steps 1 to 4 into one list before you ask the first. Then ask them one at a time. Each question has two or three options, the cost of each, its source, and one recommendation ("Decisions" in the `AGENTS.md` of the area that the answer changes). Do not decide a question that is the maintainer's.

## 6. Write the parts into the step's section

After the answers, write the step's parts into its section of `docs/roadmap.md`, in a pull request of its own that the parts start from. Each part names its frames by number and its decided points, with "the maintainer's decision of" and the date. The frame table and the value table go into that pull request's body.

Each part then gets a session brief, one file for each part, which the maintainer starts a session with: the part's scope, its frames, the documents to read first, and what done means. A brief is not committed.
