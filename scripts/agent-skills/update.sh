#!/usr/bin/env bash
# Updates every agent skill in skills-lock.json to its latest upstream version (docs/agent-skills.md, section 4).
# skills-lock.json holds the list once: for each skill, the script reads its source and runs `skills add --copy`.
# `skills update` is not used: it runs `skills add` without --copy, so it writes a store in .agents/ and symlinks.
#
# Inputs: none; run it from the repository root, with Node, npx and jq. Exit codes: 0 when every skill is installed and
# check-agent-skills.sh passes; the exit code of `skills add` when an install fails; 1 when check-agent-skills.sh fails.
#
# Run it locally: pnpm skills:update
set -euo pipefail

jq -r '.skills | to_entries[] | "\(.value.source) \(.key)"' skills-lock.json |
  while read -r source skill; do
    npx -y skills@1.7.2 add "$source" --copy -a claude-code -y -s "$skill" </dev/null
  done

# The tool can still create them (.gitignore, the comment above .agents/).
rm -rf .agents agent

scripts/ci/check-agent-skills.sh
