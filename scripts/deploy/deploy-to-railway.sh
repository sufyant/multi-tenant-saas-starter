#!/usr/bin/env bash
# Sets IMAGE as the image of api-web and api-worker in one Railway commit. The project token selects
# the project and the environment, and the CLI finds the services by name. CI=true turns off the
# CLI's self-update. The CLI reads a JSON patch from stdin when stdin is not a terminal, and then
# ignores --service-config. So `environment edit` gets a terminal (-t), and the script checks that
# the CLI committed the change. Before the commit, it records the existing deployments of each
# service for wait-for-railway.sh.
#
# Inputs: RAILWAY_TOKEN (secret), RAILWAY_CLI_IMAGE, IMAGE (name@digest), GITHUB_SHA, RUNNER_TEMP.
# Writes: $RUNNER_TEMP/deployments-before-<service>.json.
# Exit codes: 0 when the CLI committed the new image; the CLI's exit code or 1 otherwise. The CLI's
# own message is printed.
set -euo pipefail

: "${RAILWAY_TOKEN:?}" "${RAILWAY_CLI_IMAGE:?}" "${IMAGE:?}" "${GITHUB_SHA:?}" "${RUNNER_TEMP:?}"

railway() { docker run --rm -e RAILWAY_TOKEN -e CI "$RAILWAY_CLI_IMAGE" railway "$@"; }

# The next step waits for a deployment that is not in these lists.
for service in api-web api-worker; do
  railway deployment list --service "$service" --limit 50 --json | jq -c '[.[].id]' \
    > "$RUNNER_TEMP/deployments-before-$service.json"
done

# With a terminal, Docker sends the CLI's errors to stdout too, so the step prints the output first.
status=0
output=$(docker run --rm -t -e RAILWAY_TOKEN -e CI "$RAILWAY_CLI_IMAGE" railway environment edit \
  --service-config api-web source.image "$IMAGE" \
  --service-config api-worker source.image "$IMAGE" \
  --message "Deploy ${GITHUB_SHA::7}") || status=$?
echo "$output"
if (( status != 0 )); then
  exit "$status"
fi
if ! grep -q "Configuration committed" <<<"$output"; then
  echo "::error::The Railway CLI did not commit the new image."
  exit 1
fi
