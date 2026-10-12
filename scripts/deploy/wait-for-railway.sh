#!/usr/bin/env bash
# Waits until the new deployment of api-web and api-worker is SUCCESS. A new deployment is one whose
# ID was not in $RUNNER_TEMP/deployments-before-<service>.json. Railway keeps the old deployment
# serving when the new one fails its health check, so the script does not roll back. It prints
# deployment IDs and statuses only, never a variable. A failed list call counts as "no new
# deployment yet", and the script asks again.
#
# Inputs: RAILWAY_TOKEN (secret), RAILWAY_CLI_IMAGE, RUNNER_TEMP. WAIT_TIMEOUT_SECONDS (default 600)
# and WAIT_POLL_SECONDS (default 15) exist for tests.
# Exit codes: 0 when both are SUCCESS; 1 when one is FAILED, CRASHED or REMOVED, or after the timeout.
set -euo pipefail

: "${RAILWAY_TOKEN:?}" "${RAILWAY_CLI_IMAGE:?}" "${RUNNER_TEMP:?}"
timeout_seconds="${WAIT_TIMEOUT_SECONDS:-600}"
poll_seconds="${WAIT_POLL_SECONDS:-15}"

railway() { docker run --rm -e RAILWAY_TOKEN -e CI "$RAILWAY_CLI_IMAGE" railway "$@"; }

declare -A state
pending=(api-web api-worker)
deadline=$((SECONDS + timeout_seconds))
while (( ${#pending[@]} > 0 )); do
  waiting=()
  for service in "${pending[@]}"; do
    deployment=$(railway deployment list --service "$service" --limit 50 --json |
      jq -r --slurpfile before "$RUNNER_TEMP/deployments-before-$service.json" \
        '[.[] | select(.id | IN($before[0][]) | not)] | sort_by(.createdAt) | last
         | if . == null then empty else "\(.id) \(.status)" end') || deployment=""
    state[$service]="${deployment:-no new deployment yet}"
    echo "$service: ${state[$service]}"
    case "${deployment#* }" in
      SUCCESS) ;;
      FAILED | CRASHED | REMOVED)
        echo "::error::$service: deployment ${deployment% *} ended as ${deployment#* }. Its Railway logs give the reason."
        exit 1
        ;;
      *) waiting+=("$service") ;;
    esac
  done
  pending=("${waiting[@]}")
  if (( ${#pending[@]} > 0 )); then
    if (( SECONDS >= deadline )); then
      for service in "${pending[@]}"; do
        echo "::error::$service: not healthy after $((timeout_seconds / 60)) minutes: ${state[$service]}"
      done
      exit 1
    fi
    sleep "$poll_seconds"
  fi
done
