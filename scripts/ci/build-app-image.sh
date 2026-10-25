#!/usr/bin/env bash
# Builds the image of a Next.js app, `web` or `platform`. The commit is the release that each error event names, the same in every
# environment (F55). With the token of Better Stack's source map upload, the build uploads the app's source maps there and then
# deletes them; the token goes in as a BuildKit secret, never as a build argument, so no layer holds it (apps/<app>/Dockerfile).
# Without the token (a fork's or Dependabot's pull request, a local build) the build uploads nothing and does not fail.
#
# Inputs: the app's name, which is also the image's tag. The environment: GITHUB_SHA, GITHUB_SERVER_URL and GITHUB_REPOSITORY, and
# for the upload SENTRY_AUTH_TOKEN, SENTRY_URL, SENTRY_ORG and SENTRY_PROJECT (docs/github-settings.md, section 3.8). With the token,
# a missing value stops the build: without SENTRY_URL the uploader would send the token to sentry.io. Exit codes: 0 when the image
# is built; 2 for a wrong argument or a missing value; Docker's exit code when the build fails.
#
# Run it locally, with Docker and without the token:
#   GITHUB_SHA="$(git rev-parse HEAD)" GITHUB_SERVER_URL=https://github.com GITHUB_REPOSITORY=owner/repo \
#     scripts/ci/build-app-image.sh web
set -euo pipefail

app="${1:-}"
if [[ "$app" != "web" && "$app" != "platform" ]]; then
  echo "Usage: $0 web|platform" >&2
  exit 2
fi

upload=()
if [[ -n "${SENTRY_AUTH_TOKEN:-}" ]]; then
  for name in SENTRY_URL SENTRY_ORG SENTRY_PROJECT; do
    if [[ -z "${!name:-}" ]]; then
      echo "The source map token is set, but $name is empty. Set the GitHub variable of docs/github-settings.md, section 3.8." >&2
      exit 2
    fi
  done
  upload=(--secret "id=sentry_auth_token,env=SENTRY_AUTH_TOKEN"
    --build-arg SENTRY_URL --build-arg SENTRY_ORG --build-arg SENTRY_PROJECT)
  echo "The build uploads the source maps of $app to $SENTRY_URL."
else
  echo "No source map token: the build of $app uploads no source maps."
fi

docker build -f "apps/$app/Dockerfile" --build-arg NODE_MAJOR="$(cat .nvmrc)" -t "$app" \
  --build-arg "SOURCE_REVISION=$GITHUB_SHA" \
  ${upload[@]+"${upload[@]}"} \
  --label "org.opencontainers.image.source=$GITHUB_SERVER_URL/$GITHUB_REPOSITORY" \
  --label "org.opencontainers.image.revision=$GITHUB_SHA" \
  .
