#!/usr/bin/env bash
# Runs the web image `web` with fake, well-formed Clerk keys and checks that /sign-in answers below
# 500 within 30 seconds. The publishable key names clerk.example.invalid, which does not resolve.
# No real key is used.
#
# Inputs: none. Exit codes: 0 when /sign-in answers below 500; 1 otherwise.
# Leaves the container web; the workflow prints its logs on failure and removes it.
#
# Run it locally, with Docker:
#   docker build -f apps/web/Dockerfile --build-arg NODE_MAJOR="$(cat .nvmrc)" -t web .
#   scripts/ci/run-web-image.sh
#   docker rm -f web
set -euo pipefail

docker run -d --name web -p 3000:3000 \
  -e NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY="pk_test_$(printf 'clerk.example.invalid$' | base64)" \
  -e CLERK_SECRET_KEY=sk_test_fake_not_a_secret \
  -e CLERK_TELEMETRY_DISABLED=1 \
  web
# The sign-in page needs no sign-in. Any status below 500 passes.
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
