#!/usr/bin/env bash
# Checks the public route after a deploy (Continuous Delivery, chapter 5: "Smoke-test your
# deployments"). GET /health/live must answer 200. GET /v1/me/tenants needs a signed-in user and
# changes no data; without a token it answers 401. A worker answers 404 for it (HostRoleTests), so a
# 401 shows that the route reaches the web process and its authentication. Retries for at most 60
# seconds. Prints the status codes only.
#
# Inputs: ENVIRONMENT (for the message), API_URL (the public URL of api-web).
# Exit codes: 0 when both answer as expected; 1 otherwise, or when API_URL is empty.
#
# Run it locally:
#   ENVIRONMENT=staging API_URL=https://<api-web domain> scripts/deploy/smoke-test.sh
set -euo pipefail

: "${ENVIRONMENT:?}"

if [[ -z "${API_URL:-}" ]]; then
  echo "::error::The variable API_URL of the environment $ENVIRONMENT is empty. See docs/deployment.md."
  exit 1
fi

deadline=$((SECONDS + 60))
expect() {
  local path="$1" expected="$2" status
  while true; do
    status=$(curl --silent --output /dev/null --write-out '%{http_code}' --max-time 5 \
      "${API_URL%/}$path" || true)
    echo "GET $path: $status"
    if [[ "$status" == "$expected" ]]; then
      return 0
    fi
    if (( SECONDS >= deadline )); then
      echo "::error::GET $path answered $status, not $expected, within 60 seconds."
      return 1
    fi
    sleep 5
  done
}
expect /health/live 200
expect /v1/me/tenants 401
