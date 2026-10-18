#!/usr/bin/env bash
# Runs the image of a Next.js app, `web` or `platform`, with fake, well-formed settings and checks that /sign-in answers below 500
# within 30 seconds. The publishable key names clerk.example.invalid and API_URL names api.example.invalid; neither resolves. No
# real key is used. The app reads its settings at start (src/env.ts), so a missing one stops the container here.
#
# Inputs: the app's name, `web` or `platform`, which is also the image's and the container's name. Exit codes: 0 when /sign-in
# answers below 500; 2 for a wrong argument; Docker's exit code when the container does not start; 1 when /sign-in does not
# answer below 500 in 30 seconds. Leaves the container; the workflow prints its logs on failure and removes it.
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

docker run -d --name "$app" -p 3000:3000 \
  -e API_URL=https://api.example.invalid \
  -e NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY="pk_test_$(printf 'clerk.example.invalid$' | base64)" \
  -e CLERK_SECRET_KEY=sk_test_fake_not_a_secret \
  -e NEXT_PUBLIC_CLERK_SIGN_IN_URL=/sign-in \
  -e CLERK_TELEMETRY_DISABLED=1 \
  "$app"
# The sign-in page needs no sign-in. Any status below 500 passes.
status=""
for _ in $(seq 30); do
  status=$(curl --silent --output /dev/null --write-out '%{http_code}' --max-time 5 \
    http://localhost:3000/sign-in || true)
  if [[ "$status" =~ ^[1-4][0-9][0-9]$ ]]; then
    echo "GET /sign-in: $status"
    exit 0
  fi
  sleep 1
done
echo "GET /sign-in did not answer below 500 in 30 seconds. Last status: $status" >&2
exit 1
