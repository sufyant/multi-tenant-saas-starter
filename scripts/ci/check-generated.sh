#!/usr/bin/env bash
# Runs every generator and fails when a committed generated file differs from what its generator writes now, or when a generator
# writes a file that is not committed (rule D1, case 2, and "Generated files" in apps/api/docs/ARCHITECTURE.md). pnpm generate runs
# the generators in their order: the CSS of the design tokens, the emails, the C# code, the OpenAPI document, then the API client.
#
# Inputs: none; run it from the repository root, with pnpm install done and the .NET SDK of apps/api/global.json. Exit codes: 0 when
# every generated file is current; 1 otherwise; the exit code of pnpm generate when a generator fails.
#
# Run it locally: scripts/ci/check-generated.sh
set -euo pipefail

pnpm generate

if [[ -n "$(git status --porcelain)" ]]; then
  git status --short
  git --no-pager diff --stat
  echo "A generated file is out of date. Run \`pnpm generate\` and add the changes to the commit." >&2
  exit 1
fi
