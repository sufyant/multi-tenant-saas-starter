#!/usr/bin/env bash
# Checks that the agent skills in .claude/skills are real, complete and locked (docs/agent-skills.md).
# A symlink points at a folder outside the repository, so a fresh clone gets nothing.
#
# Inputs: none; run it from the repository root. Exit codes: 0 when every check passes; 1 when
# .claude/skills holds a symlink, a folder without SKILL.md, or a set of folders that differs from
# the skills in skills-lock.json. It prints every failure before it exits.
#
# Run it locally: scripts/ci/check-agent-skills.sh
set -euo pipefail

dir=.claude/skills
failed=0

links=$(find "$dir" -type l)
if [[ -n "$links" ]]; then
  echo "::error::$dir must hold real folders, not symlinks:"
  echo "$links"
  failed=1
fi

for skill in "$dir"/*/; do
  if [[ ! -f "$skill/SKILL.md" ]]; then
    echo "::error::${skill%/} has no SKILL.md"
    failed=1
  fi
done

# A symlink to a folder also matches */ above, so list every entry, not only folders.
if ! diff <(find "$dir" -mindepth 1 -maxdepth 1 -exec basename {} \; | sort) \
  <(jq -r '.skills | keys[]' skills-lock.json | sort); then
  echo "::error::The entries in $dir (<) and the skills in skills-lock.json (>) differ"
  failed=1
fi

exit "$failed"
