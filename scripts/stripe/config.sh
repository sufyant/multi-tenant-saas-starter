#!/usr/bin/env bash
# Plans and applies the Stripe settings in .stripe/config.json (.stripe/README.md): the default customer
# portal's features and the webhook endpoint of the API. It calls Stripe's API with curl and the account's
# secret key, in the mode the key names (sk_test_ or sk_live_). It never prints the key. It prints the
# webhook endpoint's signing secret once, when it creates the endpoint, because Stripe shows it only then.
#
# Inputs: plan or apply; STRIPE_SECRET_KEY and API_URL (the API's public root, as deploy.yml names it).
# Exit codes: 0 when the plan or the apply ends, an apply the user declines included; 1 when a variable is
# missing or an API call fails; 2 for a wrong argument.
set -euo pipefail

usage() {
  cat <<'EOF_USAGE'
Usage: scripts/stripe/config.sh plan
       scripts/stripe/config.sh apply

  plan   Change nothing. Print the difference between .stripe/config.json and
         the Stripe account: the default customer portal's features and the
         webhook endpoint at API_URL.
  apply  Print the same difference, ask, then apply it. A new webhook endpoint
         prints its signing secret once: put it in Billing:Stripe:WebhookSecret.

Requires: curl and jq, and these variables:
  STRIPE_SECRET_KEY  the account's secret key: sk_test_... for staging,
                     sk_live_... for production
  API_URL            the API's public root, such as https://api.example.com
EOF_USAGE
}

mode=""
for arg in "$@"; do
  case "$arg" in
    -h | --help) usage; exit 0 ;;
    # pnpm passes "--" to the script, so the script ignores it.
    --) ;;
    plan | apply) mode="$arg" ;;
    *) echo "Unknown argument: $arg" >&2; usage >&2; exit 2 ;;
  esac
done
if [[ -z "$mode" ]]; then usage >&2; exit 2; fi
for var in STRIPE_SECRET_KEY API_URL; do
  if [[ -z "${!var:-}" ]]; then echo "$var is not set. See .stripe/README.md." >&2; exit 1; fi
done

file="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.stripe" && pwd)/config.json"
api="https://api.stripe.com/v1"
url="${API_URL%/}$(jq -r '.webhook.path' "$file")"

# One call to Stripe's API. The key goes in a header file readable by the user only, so it never shows in
# the process list. A failed call prints Stripe's error message and stops.
headers="$(mktemp)"
chmod 600 "$headers"
trap 'rm -f "$headers"' EXIT
printf 'Authorization: Bearer %s\n' "$STRIPE_SECRET_KEY" >"$headers"
stripe() {
  local method="$1" path="$2" answer
  shift 2
  if ! answer="$(curl --silent --show-error --fail-with-body --max-time 20 -H "@$headers" -X "$method" "$api$path" "$@")"; then
    echo "Stripe answered: $(jq -r '.error.message // .' <<<"$answer" 2>/dev/null || echo "$answer")" >&2
    exit 1
  fi
  echo "$answer"
}

# The portal's features as form fields, from the JSON of the file: features[a][b]=c, and a list as
# features[a][b][]=c.
form_of_features() {
  jq -r '.portal.features | paths(type != "object" and type != "array") as $p | [$p, getpath($p)] |
    "features" + ($p | map(if type == "number" then "[]" else "[" + . + "]" end) | join("")) + "=" + (.[1] | tostring)' "$file" |
    while IFS= read -r field; do printf -- '--data-urlencode\n%s\n' "$field"; done
}

# The features that the file names, as the live configuration holds them, in the file's shape.
wanted_features="$(jq -S '.portal.features | walk(if type == "array" then sort else . end)' "$file")"
portal="$(stripe GET '/billing_portal/configurations?is_default=true&limit=1' | jq '.data[0] // empty')"
live_features="$(jq -S --argjson wanted "$wanted_features" \
  '.features as $f | $wanted | with_entries(.key as $k | .value = ($f[$k] | with_entries(select(.key as $x | $wanted[$k] | has($x))))) | walk(if type == "array" then sort else . end)' <<<"${portal:-{\}}")"

wanted_events="$(jq -S '.webhook.enabled_events | sort' "$file")"
endpoint="$(stripe GET '/webhook_endpoints?limit=100' | jq --arg url "$url" '[.data[] | select(.url == $url)][0] // empty')"
live_events="$(jq -S '.enabled_events // [] | sort' <<<"${endpoint:-{\}}")"

changes=0
echo "== Customer portal (the default configuration)"
if [[ -z "$portal" ]]; then
  echo "None yet: apply creates it."
  changes=1
elif [[ "$wanted_features" == "$live_features" ]]; then
  echo "Up to date."
else
  diff <(echo "$live_features") <(echo "$wanted_features") || true
  changes=1
fi
echo "== Webhook endpoint $url"
if [[ -z "$endpoint" ]]; then
  echo "None yet: apply creates it and prints its signing secret once."
  changes=1
elif [[ "$wanted_events" == "$live_events" && "$(jq -r '.status' <<<"$endpoint")" == "enabled" ]]; then
  echo "Up to date."
else
  diff <(echo "$live_events") <(echo "$wanted_events") || true
  changes=1
fi

if [[ "$mode" == "plan" || "$changes" == 0 ]]; then exit 0; fi
read -r -p "Apply this to the Stripe account of this key? [y/N] " answer
if [[ "$answer" != "y" ]]; then echo "Not applied."; exit 0; fi

mapfile -t feature_fields < <(form_of_features)
if [[ -z "$portal" ]]; then
  stripe POST /billing_portal/configurations "${feature_fields[@]}" --data-urlencode "default_return_url=${API_URL%/}" >/dev/null
elif [[ "$wanted_features" != "$live_features" ]]; then
  stripe POST "/billing_portal/configurations/$(jq -r '.id' <<<"$portal")" "${feature_fields[@]}" >/dev/null
fi

mapfile -t event_fields < <(jq -r '.webhook.enabled_events[] | "--data-urlencode\nenabled_events[]=" + .' "$file")
if [[ -z "$endpoint" ]]; then
  created="$(stripe POST /webhook_endpoints --data-urlencode "url=$url" "${event_fields[@]}")"
  echo "The webhook endpoint's signing secret, shown once: $(jq -r '.secret' <<<"$created")"
  echo "Put it in Billing:Stripe:WebhookSecret (Railway, the API service), then keep it in the password manager."
else
  stripe POST "/webhook_endpoints/$(jq -r '.id' <<<"$endpoint")" "${event_fields[@]}" --data-urlencode "disabled=false" >/dev/null
fi
echo "Applied."
