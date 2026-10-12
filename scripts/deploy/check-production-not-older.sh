#!/usr/bin/env bash
# Fails when this run would take production back to an older commit: when GITHUB_SHA is an ancestor
# of the commit of the latest successful production deployment. Each push to main makes a run whose
# production job waits for an approval, and approving an older run must not deploy older code.
# A rollback skips the check on purpose. Staging is not checked.
#
# Inputs: ENVIRONMENT (staging or production), ROLLBACK (true or false), GITHUB_REPOSITORY,
# GITHUB_SHA, and GH_TOKEN (a token that may read deployments). Needs the git history of main.
# Exit codes: 0 when the deploy may go on; 1 when the commit is older or the API or git fails.
#
# Run it locally from a clone with the full history, logged in with gh:
#   ENVIRONMENT=production ROLLBACK=false GITHUB_REPOSITORY=<owner>/<repo> \
#     GITHUB_SHA="$(git rev-parse HEAD)" scripts/deploy/check-production-not-older.sh
set -euo pipefail

: "${ENVIRONMENT:?}" "${ROLLBACK:?}" "${GITHUB_REPOSITORY:?}" "${GITHUB_SHA:?}"

if [[ "$ENVIRONMENT" != production ]]; then
  echo "The check is for production only."
  exit 0
fi
if [[ "$ROLLBACK" == true ]]; then
  echo "Rollback: the check is skipped."
  exit 0
fi

# The newest production deployment that reached success. This job's own deployment has
# no success yet. An API error stops the script: each call is an assignment, which fails.
deployments=$(gh api --paginate --slurp "repos/$GITHUB_REPOSITORY/deployments?environment=production&per_page=100" |
  jq -r 'add // [] | sort_by(.id) | reverse | .[] | "\(.id):\(.sha)"')
latest=""
for deployment in $deployments; do
  statuses=$(gh api --paginate --slurp "repos/$GITHUB_REPOSITORY/deployments/${deployment%%:*}/statuses")
  if jq -e 'add // [] | any(.state == "success")' > /dev/null <<<"$statuses"; then
    latest="${deployment#*:}"
    break
  fi
done

if [[ -z "$latest" ]]; then
  echo "Production has no successful deployment yet."
  exit 0
fi
echo "Production runs $latest. This run deploys $GITHUB_SHA."
if [[ "$latest" == "$GITHUB_SHA" ]]; then
  exit 0
fi
status=0
git merge-base --is-ancestor "$GITHUB_SHA" "$latest" || status=$?
if (( status == 0 )); then
  echo "::error::Production already runs a newer commit. Approve the newest waiting run instead, or use the rollback workflow (docs/deployment.md)."
  exit 1
fi
if (( status != 1 )); then
  echo "::error::git could not compare $GITHUB_SHA with $latest."
  exit 1
fi
