#!/usr/bin/env bash
# Runs the image of a Next.js app, `web` or `platform`, with fake, well-formed settings, all given at run time. The image is
# built without any variable, so it proves that one image serves every environment (F7). The script starts it twice, with two
# different publishable keys. Each time, the health path of .railway/<app>.json must answer 200, and /sign-in must answer 200
# with the key of that run in its page and without the key of the other run. The keys name clerk-a.example.invalid and
# clerk-b.example.invalid, and API_URL names api.example.invalid; none resolves. No real key is used. The app reads its settings
# at start (src/env.ts), so a missing one stops the container here.
#
# Inputs: the app's name, `web` or `platform`, which is also the image's and the container's name. Exit codes: 0 when both runs
# pass; 2 for a wrong argument; Docker's exit code when a container does not start; 1 when a check fails. Leaves the container of
# the second run; the workflow prints its logs on failure and removes it.
#
# Run it locally, with Docker:
#   docker build -f apps/web/Dockerfile --build-arg NODE_MAJOR="$(cat .nvmrc)" -t web .
#   scripts/ci/run-app-image.sh web
#   docker rm -f web
set -euo pipefail

app="${1:-}"
if [[ "$app" != "web" && "$app" != "platform" ]]; then
  echo "Usage: $0 web|platform" >&2
  exit 2
fi
infrastructure="$(dirname "${BASH_SOURCE[0]}")/../../.railway/$app.json"
port=$(jq -r '.port' "$infrastructure")
health_path=$(jq -r '.healthPath' "$infrastructure")

# A publishable key is pk_test_ and the base64 of the Frontend API's host with a trailing $.
key_a="pk_test_$(printf 'clerk-a.example.invalid$' | base64)"
key_b="pk_test_$(printf 'clerk-b.example.invalid$' | base64)"

# Starts the image with the key and checks the health path and the sign-in page.
run() {
  local key="$1" other="$2" status page
  docker run -d --name "$app" -p "$port:$port" \
    -e PORT="$port" \
    -e API_URL=https://api.example.invalid \
    -e CLERK_PUBLISHABLE_KEY="$key" \
    -e CLERK_SECRET_KEY=sk_test_fake_not_a_secret \
    -e CLERK_TELEMETRY_DISABLED=1 \
    "$app"
  status=$(curl --silent --output /dev/null --write-out '%{http_code}' --max-time 5 \
    --retry 30 --retry-delay 1 --retry-all-errors --retry-max-time 30 \
    "http://localhost:$port$health_path" || true)
  echo "GET $health_path: $status"
  if [[ "$status" != 200 ]]; then
    echo "GET $health_path answered $status, not 200, within 30 seconds." >&2
    return 1
  fi
  page=$(curl --silent --show-error --fail --max-time 10 "http://localhost:$port/sign-in")
  if ! grep -qF "$key" <<<"$page"; then
    echo "The sign-in page does not carry the publishable key that the container got." >&2
    return 1
  fi
  if grep -qF "$other" <<<"$page"; then
    echo "The sign-in page carries the publishable key of another run." >&2
    return 1
  fi
  echo "GET /sign-in: 200, with the publishable key of this run"
}

run "$key_a" "$key_b"
docker rm -f "$app" > /dev/null
run "$key_b" "$key_a"
