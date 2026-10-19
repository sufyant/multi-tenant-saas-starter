#!/usr/bin/env bash
# Pushes the local image of an app, `api`, `web` or `platform`, to GHCR as ghcr.io/<owner>/<repo>/<app>, with the tags
# <commit SHA> and main, and reads the digest back from the registry. A registry name is lower case, and an owner name can have
# capitals. The deploy uses the digest, never a tag.
#
# Inputs: APP (api, web or platform; also the local image's name), GITHUB_REPOSITORY, GITHUB_SHA, GITHUB_OUTPUT (the step's
# output file). Needs a Docker login to ghcr.io with permission to push, so it runs in CI only.
# Outputs: name, digest and image (name@digest) in GITHUB_OUTPUT.
# Exit codes: 0 when the push succeeded and the registry returned a digest; Docker's exit code when a docker command fails; 1 when
# the registry returned no digest; 2 for a wrong APP.
set -euo pipefail

: "${APP:?}" "${GITHUB_REPOSITORY:?}" "${GITHUB_SHA:?}" "${GITHUB_OUTPUT:?}"
if [[ "$APP" != api && "$APP" != web && "$APP" != platform ]]; then
  echo "APP must be api, web or platform, not '$APP'." >&2
  exit 2
fi

name="ghcr.io/${GITHUB_REPOSITORY,,}/$APP"
for tag in "$GITHUB_SHA" main; do
  docker tag "$APP" "$name:$tag"
  docker push "$name:$tag"
done
digest=$(docker buildx imagetools inspect "$name:$GITHUB_SHA" --format '{{json .Manifest}}' | jq -r .digest) || true
if [[ ! "$digest" =~ ^sha256:[0-9a-f]{64}$ ]]; then
  echo "The registry returned no digest for $name:$GITHUB_SHA." >&2
  exit 1
fi
echo "Image: $name@$digest"
{
  echo "name=$name"
  echo "digest=$digest"
  echo "image=$name@$digest"
} >> "$GITHUB_OUTPUT"
