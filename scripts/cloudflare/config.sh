#!/usr/bin/env bash
# Plans and applies the Cloudflare settings in .cloudflare/config.json (.cloudflare/README.md): the environment's private R2 bucket in
# the EU jurisdiction and its lifecycle rules. It calls Cloudflare's API with curl and one token, and never prints the token.
#
# Inputs: plan or apply, and an environment (staging or production); CLOUDFLARE_API_TOKEN and CLOUDFLARE_ACCOUNT_ID. Exit codes: 0 when
# the plan or the apply ends, an apply the user declines included; 1 when a variable is missing or an API call fails; 2 for a wrong
# argument.
set -euo pipefail

usage() {
  cat <<'EOF_USAGE'
Usage: scripts/cloudflare/config.sh plan staging|production
       scripts/cloudflare/config.sh apply staging|production

  plan   Change nothing. Print the difference between .cloudflare/config.json
         and the live Cloudflare account: the environment's R2 bucket and its
         lifecycle rules.
  apply  Print the same difference, ask, then apply it.

Requires: curl and jq, and these variables:
  CLOUDFLARE_API_TOKEN   an account API token with "Workers R2 Storage: Edit"
  CLOUDFLARE_ACCOUNT_ID  the id of the Cloudflare account
EOF_USAGE
}

mode=""
environment=""
for arg in "$@"; do
  case "$arg" in
    -h | --help) usage; exit 0 ;;
    # pnpm passes "--" to the script, so the script ignores it.
    --) ;;
    plan | apply) mode="$arg" ;;
    staging | production) environment="$arg" ;;
    *) echo "Unknown argument: $arg" >&2; usage >&2; exit 2 ;;
  esac
done
if [[ -z "$mode" || -z "$environment" ]]; then usage >&2; exit 2; fi

for var in CLOUDFLARE_API_TOKEN CLOUDFLARE_ACCOUNT_ID; do
  if [[ -z "${!var:-}" ]]; then
    echo "$var is not set. See .cloudflare/README.md." >&2
    exit 1
  fi
done

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
file="$root/.cloudflare/config.json"
api="https://api.cloudflare.com/client/v4/accounts/$CLOUDFLARE_ACCOUNT_ID/r2/buckets"
jurisdiction="$(jq -r '.r2.jurisdiction' "$file")"
bucket="$(jq -r '.r2.bucket_prefix' "$file")$environment"

# The token goes into a header file that only the user can read, so it is never on a command line. Every call names the jurisdiction:
# a bucket of the EU jurisdiction is reached only with it (Cloudflare R2 docs, "Data location").
work="$(mktemp -d)"
chmod 700 "$work"
trap 'rm -rf "$work"' EXIT
printf 'Authorization: Bearer %s\ncf-r2-jurisdiction: %s\n' "$CLOUDFLARE_API_TOKEN" "$jurisdiction" >"$work/header"

# Calls the API: a method, a URL and an optional JSON body. Prints the answer's body. On a status other than 2xx, or an answer whose
# success is false, it prints the method, the URL, the status and the errors, and exits 1. A 404 is printed as "missing".
call() {
  local method="$1" url="$2" body="${3:-}" out status
  local args=(--silent --show-error --request "$method" --header "@$work/header"
    --header 'Content-Type: application/json' --write-out '\n%{http_code}' --max-time 30)
  if [[ -n "$body" ]]; then
    printf '%s' "$body" >"$work/body.json"
    args+=(--data "@$work/body.json")
  fi
  if ! out="$(curl "${args[@]}" "$url")"; then
    echo "$method $url failed." >&2
    exit 1
  fi
  status="${out##*$'\n'}"
  if [[ "$status" == 404 ]]; then
    printf 'missing'
    return
  fi
  if [[ "$status" != 2?? ]]; then
    echo "$method $url answered $status: $(jq -c '.errors // empty' <<<"${out%$'\n'*}" 2>/dev/null || true)" >&2
    exit 1
  fi
  printf '%s' "${out%$'\n'*}"
}

# The bucket, and its lifecycle rules. A missing bucket has no rules.
bucket_answer="$(call GET "$api/$bucket")"
want_rules="$(jq -c '.r2.lifecycle_rules' "$file")"
if [[ "$bucket_answer" == missing ]]; then
  live_rules="[]"
else
  rules_answer="$(call GET "$api/$bucket/lifecycle")"
  if [[ "$rules_answer" == missing ]]; then live_rules="[]"; else live_rules="$(jq -c '.result.rules // []' <<<"$rules_answer")"; fi
fi

echo "== Cloudflare R2, $environment"
changes=0
if [[ "$bucket_answer" == missing ]]; then
  echo "+ create bucket \"$bucket\" in the $jurisdiction jurisdiction, private"
  changes=1
fi
if [[ "$(jq -c 'sort_by(.id)' <<<"$want_rules")" != "$(jq -c 'sort_by(.id)' <<<"$live_rules")" ]]; then
  echo "~ set the lifecycle rules of \"$bucket\":"
  jq -r '.[] | "    \(.id): prefix \"\(.conditions.prefix)\", " + (del(.id, .enabled, .conditions) | tojson)' <<<"$want_rules"
  echo "  live: $(jq -c '[.[].id]' <<<"$live_rules")"
  changes=1
fi
if [[ "$changes" == 0 ]]; then
  echo "No difference."
  exit 0
fi

if [[ "$mode" == "plan" ]]; then exit 0; fi
read -r -p "Apply this to Cloudflare, $environment? [y/N] " answer
if [[ "$answer" != "y" ]]; then echo "Not applied."; exit 0; fi

# A new bucket is private: R2 gives it no public access and no r2.dev address unless one is turned on.
if [[ "$bucket_answer" == missing ]]; then
  call POST "$api" "$(jq -c -n --arg name "$bucket" '{name: $name}')" >/dev/null
  echo "Applied: create bucket \"$bucket\"."
fi
call PUT "$api/$bucket/lifecycle" "$(jq -c -n --argjson rules "$want_rules" '{rules: $rules}')" >/dev/null
echo "Applied: the lifecycle rules of \"$bucket\"."
