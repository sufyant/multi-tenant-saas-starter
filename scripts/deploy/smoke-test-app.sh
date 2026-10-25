#!/usr/bin/env bash
# Checks the public route of a web app after a deploy (Continuous Delivery, chapter 5: "Smoke-test your deployments"). The
# health path of .railway/<app>.json must answer 200. GET / without a session must end at the sign-in page, as a browser's
# first visit does: the request asks for HTML, keeps the cookies and follows the redirects. With a key of a Clerk development
# instance, Clerk first sends the browser through its Frontend API and back (its "handshake"), so this step also shows that the
# instance accepts the app's domain. Retries for at most 60 seconds. Prints the status codes and the final path only, never a
# cookie or a query. For GET /, each attempt and the final error also name the host where the chain stopped and curl's exit
# code (28 is a timeout): "stopped at the app", or "stopped at a third party: <host>", such as Clerk's Frontend API.
# curl's %{url_effective} is the URL in progress when --max-time stops a redirect chain.
#
# Inputs: APP (web or platform), APP_URL (the public URL of the app), ENVIRONMENT (for the message), URL_VARIABLE (the name of
# the GitHub variable that holds APP_URL, for the message).
# Exit codes: 0 when both answer as expected; 1 otherwise, or when APP_URL is empty.
#
# Run it locally:
#   APP=web APP_URL=https://<web domain> ENVIRONMENT=staging URL_VARIABLE=WEB_URL scripts/deploy/smoke-test-app.sh
set -euo pipefail

: "${APP:?}" "${ENVIRONMENT:?}" "${URL_VARIABLE:?}"

if [[ -z "${APP_URL:-}" ]]; then
  echo "::error::The variable $URL_VARIABLE of the environment $ENVIRONMENT is empty. See docs/deployment.md."
  exit 1
fi
url="${APP_URL%/}"
app_host=$(sed -E 's#^https?://([^/?#]+).*$#\1#' <<<"$url")
health_path=$(jq -r '.healthPath' "$(dirname "${BASH_SOURCE[0]}")/../../.railway/$APP.json")
jar=$(mktemp)
trap 'rm -f "$jar"' EXIT

deadline=$((SECONDS + 60))
until_deadline() {
  if (( SECONDS >= deadline )); then
    echo "::error::$APP: $1 within 60 seconds."
    exit 1
  fi
  sleep 5
}

while true; do
  status=$(curl --silent --output /dev/null --write-out '%{http_code}' --max-time 5 "$url$health_path" || true)
  echo "$APP: GET $health_path: $status"
  [[ "$status" == 200 ]] && break
  until_deadline "GET $health_path answered $status, not 200,"
done

while true; do
  : > "$jar"
  code=0
  result=$(curl --silent --output /dev/null --location --max-redirs 10 --cookie "$jar" --cookie-jar "$jar" \
    --header 'Accept: text/html' --write-out '%{http_code} %{url_effective}' --max-time 15 "$url/") || code=$?
  status="${result%% *}"
  final="${result#* }"
  # The path and the host only: the query can hold Clerk's redirect parameters.
  path=$(sed -E 's#^https?://[^/]+##; s#[?#].*$##' <<<"$final")
  host=$(sed -E 's#^https?://([^/?#]+).*$#\1#' <<<"$final")
  if [[ "$host" == "$app_host" ]]; then
    stopped="stopped at the app"
  else
    stopped="stopped at a third party: $host"
  fi
  echo "$APP: GET / without a session ended at ${path:-?} with $status, curl exit code $code, $stopped"
  [[ "$status" == 200 && "$path" == /sign-in && "$final" == "$url"/* ]] && break
  until_deadline "GET / without a session did not end at $url/sign-in with 200 (last attempt: $stopped, curl exit code $code)"
done
