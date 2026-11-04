# Agent skill evaluations

This document holds the evaluation of each own agent skill (`docs/agent-skills.md`, section 5), and of each candidate that was not written. An evaluation is a scenario from a real step of this repository, a rubric, and the result of a subagent without the skill and with it. A skill is kept only when it changed the result. A change to a skill runs its scenario again, and its result replaces the one here.

## 1. How a scenario runs

Each scenario runs in a fresh subagent on the repository's worktree. The subagent reads the repository's `AGENTS.md` files as a new session does. The rules of a run:

- It changes no file of the repository and makes no commit, push, comment, re-run or pull request.
- It runs only commands that read. A command that would change something, or that touches Railway, a database, Better Stack or Clerk, is written as "Would run" in its report.
- It makes at most 10 Figma calls.
- It writes a report: its steps with their commands, every artifact it would post, and where it would stop and ask.

Without the skill, the subagent gets the task only. With the skill, it also gets the name and the description of each own skill, as a session sees them, and reads a `SKILL.md` only when it decides that the skill fits. The rubric of each scenario was written before the first run was read.

A rehearsal plans the work; it does not do it. A failure that happens only while an agent writes code, such as a test that changes in the code commit, does not show in a rehearsal (section 4.1).

## 2. `building-screens-from-design`

The source: a step built its screens first and compared them with the frames in a later pull request, and that comparison found real faults at once. The comparison of another pull request had "Not shot" rows for states that Playwright reaches.

The task: "35-frontend-fix-3: the frontend adds the Unread and All tabs, the area and the actor to the notification bells, in both apps. The design is section 3 · Notifications. Do the task up to the first code edit, then describe the rest to the end of the pull request."

| Rubric | Without the skill | With the skill |
| --- | --- | --- |
| a. Lists every frame of the task before building | Read 6 frames of 28; listed the rest by name | Every frame of both apps, one row each, with its part or its step |
| b. A table of each value and its source, before code | None; a table of what the design draws | A value table: API field, message key or token for each value |
| c. Stops on a value without a source | Partly: noted types outside the catalog, used the frames' area words | Stopped: which tab opens, the platform's organisation name, the web's actor; asked one question at a time |
| d. Builds with `packages/ui` and the tokens | Yes | Yes |
| e. Shot at the frame's size with its data, compare, fix, compare again, in this pull request | No: one download of the CI shots at the end | Yes: a shot per frame, the frame's size and data, a draft pull request for CI, a loop |
| f. A frame table on the pull request with three forms | A placeholder | Yes; after the edit of step 6, "no shot" only for frames of other steps |

The first run with the skill still gave "no shot" to loading, error and toast frames. Step 6 now says how to reach those states, and the second run shot them. The second run also listed 13 unclear sentences. The real ones are fixed: the file key (now in `docs/roadmap.md`, step 34), `get_metadata` on a section, the call limit, a missing language frame, the shot's name and size, a draft pull request before the review, and the stop before the dependent tests.

The final run, with the skill as it is now, kept every point of the rubric. Its frame table has a shot for each state, "no shot" only for the frames of steps 39 and 40, and four questions before the first test. It found one more contradiction, which is fixed: the skill opened the draft pull request before the `architecture-reviewer` run.

Verdict: kept.

## 3. `starting-roadmap-steps`

The source: a step's screens were built without reading every frame, and a frame showed a value that no API field gives, which a fix pull request found.

The task: "Start step 37, Organisation pages. You are the first session of the step. Prepare the step so that its backend and frontend sessions can begin."

| Rubric | Without the skill | With the skill |
| --- | --- | --- |
| a. Lists the step's frames | Yes, by section and range | Yes, every frame of both pages |
| b. Each frame has one pull request | No: ranges per part; the platform's frames by section only | A frame table: each frame has one part, or its step |
| c. Values mapped to API fields before the backend | Partly: export, leave and sender found; no table | A value table per part; found values without a field (the two seat counts, "last used", a closed decision against a frame) and turned them into backend lines or questions |
| d. Questions up front, one at a time, with options, costs, a recommendation and a source | Yes | Yes |

The two runs with the skill listed unclear sentences. The real ones are fixed: the file key, `get_metadata` on a section, a point that only an open pull request writes, the example rows, the value table of a state frame, where the briefs go, how to split a step, and a frame that no step names. The final run made a frame table of 531 rows, 186 of them in the step, and a value table that found the CSV export without an endpoint.

Verdict: kept.

## 4. Candidates not written

### 4.1 Writing tests

The source: in four pull requests of step 35, a test came in the same commit as its code, or a changed contract test went into the code commit, without a red run.

The task: 35-backend-2's first item, from the commit before it. Run once with a question that named the broken tests, and once without it.

Result without a skill: both runs put the test commit first, planned the red run with its expected output, and put the changed `ListContractTests` into the test commit. The fixtures of the frontend went into the code commit, with a reason (they break only when the client is generated).

Verdict: not written. The documents already give the rule ("Test first", "Small commits: the test, then the change"), and the rehearsal followed it. The real failures happened while an agent wrote code, which a rehearsal does not show. A check that CI runs would hold the rule better than a skill (the maintainer's decision).

### 4.2 Adding an endpoint

The task: the public endpoint of step 38 that gives the terms version in force.

Result without a skill: 5 of 5. It cited the rules by id, added the `api-catalog.md` row, planned `pnpm generate` for `v1.json` and the client, ran the `architecture-reviewer` and wrote the tests first.

Verdict: not written. Each step is already in `apps/api/AGENTS.md`.

### 4.3 Opening and watching a pull request

The source: a pull request's CI failed twice on a test that the change did not touch, and the job was re-run; the cause was a real fault of that test.

The task: finish a branch whose `main` moved, open the pull request, then handle attempt 1 of that CI run.

Result without a skill: 9 of 10. It rebased on `origin/main` and pushed with `--force-with-lease`, ran `pnpm generate`, the `architecture-reviewer` and the roadmap row, and ended the body with what it skipped, did not check and the risks. It read the failed job's log, showed that the diff does not touch the test, found the root cause (section 9, rule 10 of `apps/api/docs/ARCHITECTURE.md`), did not re-run, and reported. Its body had no heading "Maintainer's tasks". The rebase came from the maintainer's local memory, which a cloud thread does not have.

Verdict: not written. The two missing rules went into `AGENTS.md`, section "Pull requests". The run with those lines kept every point above, and its body outline has the heading "Maintainer's tasks".

### 4.4 Reading logs

The tasks: a Delivery run whose staging deploy failed; a dead letter after a role change.

Result without a skill: both found the cause from `gh run view --log-failed` and `docs/deployment.md`, read Railway only through its logs and its deployments, and left the database queries and the replay to the maintainer.

Verdict: not written. No document held the two exact rules, so they went into `AGENTS.md`, "Repository settings": never read the Railway variables, never connect to a deployed database.
