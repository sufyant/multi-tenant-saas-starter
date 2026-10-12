#!/usr/bin/env bash
# Pushes the local image `api` to GHCR with the tags <commit SHA> and main, and reads the digest back
# from the registry. A registry name is lower case, and an owner name can have capitals. The deploy
# uses the digest, never a tag.
#
# Inputs: GITHUB_REPOSITORY, GITHUB_SHA, GITHUB_OUTPUT (the step's output file). Needs a Docker login
# to ghcr.io with permission to push, so it runs in CI only.
# Outputs: name, digest and image (name@digest) in GITHUB_OUTPUT.
# Exit codes: 0 when the push succeeded and the registry returned a digest; Docker's exit code when
# a docker command fails; 1 when the registry returned no digest.
set -euo pipefail

: "${GITHUB_REPOSITORY:?}" "${GITHUB_SHA:?}" "${GITHUB_OUTPUT:?}"

name="ghcr.io/${GITHUB_REPOSITORY,,}/api"
for tag in "$GITHUB_SHA" main; do
  docker tag api "$name:$tag"
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
