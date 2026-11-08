#!/usr/bin/env bash
# Fails when an exception of the image scans has outlived its end date, so an exception cannot stay by accident.
#
# Input: the Grype configuration of the web and platform scans, .github/grype-app-images.yaml. Its "# expires: YYYY-MM-DD"
# line holds the last day of its ignore rules. Exit codes: 0 when the file has no ignore rule, or today (UTC) is on or before
# that day; 1 when the day has passed, or the file has ignore rules and no valid end date.
set -euo pipefail

config="$(dirname "${BASH_SOURCE[0]}")/../../.github/grype-app-images.yaml"

if ! grep -q '^ignore:' "$config"; then
  echo "No scan exception."
  exit 0
fi

expires=$(sed -n 's/^# expires: \([0-9]\{4\}-[0-9]\{2\}-[0-9]\{2\}\)$/\1/p' "$config")
if [[ -z "$expires" ]]; then
  echo "$config has ignore rules but no line \"# expires: YYYY-MM-DD\"." >&2
  exit 1
fi

today=$(date -u +%F)
if [[ "$today" > "$expires" ]]; then
  echo "The scan exceptions of $config ended on $expires. Remove them when the base image has the fix, or decide a new end date." >&2
  exit 1
fi
echo "The scan exceptions of $config end on $expires."
