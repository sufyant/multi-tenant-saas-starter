#!/usr/bin/env bash
# Sets IMAGE as the image of the services of one app in one Railway commit: both API services for `api`, one service for
# `web` or `platform`. Their names come from .railway/<app>.json, which .railway/railway.ts reads too. The project token selects
# the project and the environment, and the CLI finds the services by name. CI=true turns off the
# CLI's self-update. The CLI reads a JSON patch from stdin when stdin is not a terminal, and then
# ignores --service-config. So `environment edit` gets a terminal (-t), and the script checks that
# the CLI committed the change. Before the commit, it records the existing deployments of each
# service for wait-for-railway.sh.
#
# Inputs: APP (api, web or platform), RAILWAY_TOKEN (secret), RAILWAY_CLI_IMAGE, IMAGE (name@digest), GITHUB_SHA, RUNNER_TEMP.
# Writes: $RUNNER_TEMP/deployments-before-<service>.json.
# Exit codes: 0 when the CLI committed the new image; the CLI's exit code or 1 otherwise. The CLI's
# own message is printed.
set -euo pipefail

: "${APP:?}" "${RAILWAY_TOKEN:?}" "${RAILWAY_CLI_IMAGE:?}" "${IMAGE:?}" "${GITHUB_SHA:?}" "${RUNNER_TEMP:?}"

railway() { docker run --rm -e RAILWAY_TOKEN -e CI "$RAILWAY_CLI_IMAGE" railway "$@"; }

# api.json names two services, web.json and platform.json one each.
service_names=$(jq -r '.services[]? // .service' "$(dirname "${BASH_SOURCE[0]}")/../../.railway/$APP.json")
mapfile -t services <<<"$service_names"
service_configs=()
for service in "${services[@]}"; do
  service_configs+=(--service-config "$service" source.image "$IMAGE")
done

# The next step waits for a deployment that is not in these lists.
for service in "${services[@]}"; do
  railway deployment list --service "$service" --limit 50 --json | jq -c '[.[].id]' \
    > "$RUNNER_TEMP/deployments-before-$service.json"
done

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
