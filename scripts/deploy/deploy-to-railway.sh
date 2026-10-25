#!/usr/bin/env bash
# Sets IMAGE as the image of the services of one app in one Railway commit: both API services for `api`, one service for
# `web` or `platform`. Their names come from .railway/<app>.json, which .railway/railway.ts reads too. The project token selects
# the project and the environment, and the CLI finds the services by name. CI=true turns off the
# CLI's self-update. The CLI reads a JSON patch from stdin when stdin is not a terminal, and then
# ignores --service-config. So `environment edit` gets a terminal (-t), and the script checks that
# the CLI committed the change. Before the commit, it records the existing deployments of each
# service for wait-for-railway.sh.
#
# A re-run of a Delivery run deploys digests that may be deployed already. When the newest deployment of a service (by
# createdAt, any status) already has IMAGE in meta.image, the edit would change nothing, and Railway would make no new
# deployment. So the script leaves that service out of the edit, and leaves that deployment out of
# deployments-before-<service>.json: wait-for-railway.sh then waits for it as the new deployment. When no service needs
# the edit, the script skips `environment edit`. Only the newest deployment counts: after X, then Y, a deploy of X edits.
#
# Inputs: APP (api, web or platform), RAILWAY_TOKEN (secret), RAILWAY_CLI_IMAGE, IMAGE (name@digest), GITHUB_SHA, RUNNER_TEMP.
# Writes: $RUNNER_TEMP/deployments-before-<service>.json.
# Exit codes: 0 when the CLI committed the new image, or when every service already has it; the CLI's exit code or 1
# otherwise. The CLI's own message is printed.
set -euo pipefail

: "${APP:?}" "${RAILWAY_TOKEN:?}" "${RAILWAY_CLI_IMAGE:?}" "${IMAGE:?}" "${GITHUB_SHA:?}" "${RUNNER_TEMP:?}"

railway() { docker run --rm -e RAILWAY_TOKEN -e CI "$RAILWAY_CLI_IMAGE" railway "$@"; }

# api.json names two services, web.json and platform.json one each.
service_names=$(jq -r '.services[]? // .service' "$(dirname "${BASH_SOURCE[0]}")/../../.railway/$APP.json")
mapfile -t services <<<"$service_names"

# The next step waits for a deployment that is not in these lists. A newest deployment that already has IMAGE stays out.
service_configs=()
for service in "${services[@]}"; do
  deployments=$(railway deployment list --service "$service" --limit 50 --json)
  current=$(jq -r --arg image "$IMAGE" 'sort_by(.createdAt) | last | select(.meta.image == $image) | .id' <<<"$deployments")
  if [[ -n "$current" ]]; then
    echo "$service: deployment $current already has this image"
  else
    service_configs+=(--service-config "$service" source.image "$IMAGE")
  fi
  jq -c --arg current "$current" '[.[].id | select(. != $current)]' <<<"$deployments" \
    > "$RUNNER_TEMP/deployments-before-$service.json"
done
if (( ${#service_configs[@]} == 0 )); then
  echo "$APP: every service already has this image, so the script skips environment edit."
  exit 0
fi

# With a terminal, Docker sends the CLI's errors to stdout too, so the step prints the output first.
status=0
output=$(docker run --rm -t -e RAILWAY_TOKEN -e CI "$RAILWAY_CLI_IMAGE" railway environment edit \
  "${service_configs[@]}" --message "Deploy $APP ${GITHUB_SHA::7}") || status=$?
echo "$output"
if (( status != 0 )); then
  exit "$status"
fi
if ! grep -q "Configuration committed" <<<"$output"; then
  echo "::error::The Railway CLI did not commit the new image."
  exit 1
fi
