#!/usr/bin/env bash
# Builds apps/web and apps/platform and runs their Playwright tests (F35, F37 of docs/frontend/ARCHITECTURE.md). Each app's
# playwright.config.ts starts the production build with `pnpm start`, because CI is set. The tests answer the API in the browser
# (e2e/api.ts), so no API and no database run here; API_URL names api.example.invalid, which does not resolve.
#
# Inputs, from the environment: NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY and CLERK_SECRET_KEY of a Clerk development instance, and
# E2E_CLERK_USER_EMAIL, a user of that instance with +clerk_test in the address. Exit codes: 0 when both apps pass; 1 when a
# variable is missing or an app's tests fail; the build's exit code when a build fails. Both apps run, also when the first fails.
# Each app writes its HTML report to apps/<app>/playwright-report.
#
# Run it locally, with the keys of the development instance:
#   CI=1 NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY=pk_test_... CLERK_SECRET_KEY=sk_test_... \
#     E2E_CLERK_USER_EMAIL=someone+clerk_test@example.com scripts/ci/test-e2e.sh
set -euo pipefail

for name in NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY CLERK_SECRET_KEY E2E_CLERK_USER_EMAIL; do
  if [[ -z "${!name:-}" ]]; then
    echo "$name is not set." >&2
    exit 1
  fi
done

export API_URL=https://api.example.invalid
export NEXT_PUBLIC_CLERK_SIGN_IN_URL=/sign-in
export CLERK_TELEMETRY_DISABLED=1
export PLAYWRIGHT_HTML_OPEN=never

pnpm turbo run build --filter=@repo/web --filter=@repo/platform
pnpm --filter @repo/web exec playwright install --with-deps chromium

failed=0
for app in web platform; do
  echo "::group::Playwright: $app"
  if ! pnpm --filter "@repo/$app" test:e2e --reporter=list,html; then
    failed=1
  fi
  echo "::endgroup::"
done
exit "$failed"
