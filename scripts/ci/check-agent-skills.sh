#!/usr/bin/env bash
# Checks that the agent skills in .claude/skills are real, complete and locked (docs/agent-skills.md).
# A symlink points at a folder outside the repository, so a fresh clone gets nothing.
# A skill is either an upstream skill, locked in skills-lock.json, or our own, marked in its SKILL.md with
# `origin: repository` under `metadata` (docs/agent-skills.md, section 2).
#
# Inputs: none; run it from the repository root. Exit codes: 0 when every check passes; 1 when
# .claude/skills holds a symlink, a folder without SKILL.md, or a set of upstream folders that differs from
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

# Our own skills carry the mark in the frontmatter of SKILL.md, between the first two `---` lines.
own() {
  awk 'NR == 1 { next } /^---$/ { exit } /^  origin: repository$/ { found = 1 } END { exit !found }' "$1/SKILL.md" 2>/dev/null
}

# A symlink to a folder also matches */ above, so list every entry, not only folders.
upstream=$(find "$dir" -mindepth 1 -maxdepth 1 | sort | while read -r entry; do
  own "$entry" || basename "$entry"
done)
if ! diff <(echo "$upstream") <(jq -r '.skills | keys[]' skills-lock.json | sort); then
  echo "::error::The upstream entries in $dir (<) and the skills in skills-lock.json (>) differ"
  failed=1
fi

exit "$failed"
