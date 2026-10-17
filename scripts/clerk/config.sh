#!/usr/bin/env bash
# Plans and applies the Clerk instance settings in .clerk/.
# It uses only the `clerk` CLI and the user's own `clerk auth login` session
# (or CLERK_PLATFORM_API_KEY).
set -euo pipefail

usage() {
  cat <<'EOF_USAGE'
Usage: scripts/clerk/config.sh plan|apply [development|production]

  plan   Change nothing. Print, for each instance, the difference between
         its file in .clerk/ and the live instance.
  apply  Print the same difference, ask for each instance, then apply it.

Without an instance, the script handles every file in .clerk/.

Requires: clerk, jq, and `clerk auth login` (or CLERK_PLATFORM_API_KEY).
Apply to production also requires CLERK_GOOGLE_CLIENT_ID,
CLERK_GOOGLE_CLIENT_SECRET, CLERK_MICROSOFT_CLIENT_ID and
CLERK_MICROSOFT_CLIENT_SECRET. The development instance uses Clerk's shared
OAuth credentials.
EOF_USAGE
}

# The Clerk application "AerCura". An application ID is not a secret.
app_id="app_3IeGaXTDFceiWP2j10maEBm2HD7"

mode=""
only=""
for arg in "$@"; do
  case "$arg" in
    -h | --help) usage; exit 0 ;;
    # pnpm passes "--" to the script, so the script ignores it.
    --) ;;
    plan | apply) mode="$arg" ;;
    development | production) only="$arg" ;;
    *) echo "Unknown argument: $arg" >&2; usage >&2; exit 2 ;;
  esac
done
if [[ -z "$mode" ]]; then usage >&2; exit 2; fi

config_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.clerk" && pwd)"
body_file="$(mktemp)"
chmod 600 "$body_file"
trap 'rm -f "$body_file"' EXIT

# Prints the difference for one instance. The CLI writes the difference to
# stderr and the full before and after config to stdout. The script prints
# only the difference, because the full config can hold OAuth client secrets.
plan() {
  local name="$1" instance="$2" file="$3" err
  echo "== $name"
  if ! err="$(clerk config patch --mode agent --app "$app_id" --instance "$instance" \
    --file "$file" --dry-run 2>&1 >/dev/null)"; then
    if [[ "$err" == *instance_not_found* ]]; then
      echo "No $name instance exists. Skipped."
      return 1
    fi
    echo "$err" >&2
    exit 1
  fi
  echo "$err"
}

# Adds the production OAuth credentials to the body and applies it. The body
# file is readable by the user only and is removed on exit. The CLI output is
# not printed on success; on failure each secret value is masked.
apply() {
  local name="$1" instance="$2" file="$3" err answer var
  read -r -p "Apply this to the $name instance? [y/N] " answer
  if [[ "$answer" != "y" ]]; then echo "Not applied."; return; fi
  if [[ "$name" == "production" ]]; then
    for var in CLERK_GOOGLE_CLIENT_ID CLERK_GOOGLE_CLIENT_SECRET \
      CLERK_MICROSOFT_CLIENT_ID CLERK_MICROSOFT_CLIENT_SECRET; do
      if [[ -z "${!var:-}" ]]; then echo "$var is not set." >&2; exit 1; fi
    done
    jq '.connection_oauth_google += {client_id: env.CLERK_GOOGLE_CLIENT_ID, client_secret: env.CLERK_GOOGLE_CLIENT_SECRET}
      | .connection_oauth_microsoft += {client_id: env.CLERK_MICROSOFT_CLIENT_ID, client_secret: env.CLERK_MICROSOFT_CLIENT_SECRET}' \
      "$file" >"$body_file"
  else
    cp "$file" "$body_file"
  fi
  if ! err="$(clerk config patch --mode agent --app "$app_id" --instance "$instance" \
    --file "$body_file" --yes 2>&1 >/dev/null)"; then
    err="${err//"${CLERK_GOOGLE_CLIENT_SECRET:-<none>}"/***}"
    err="${err//"${CLERK_MICROSOFT_CLIENT_SECRET:-<none>}"/***}"
    echo "$err" >&2
    exit 1
  fi
  echo "Applied to $name."
}

for name in development production; do
  if [[ -n "$only" && "$name" != "$only" ]]; then continue; fi
  case "$name" in
    development) instance="dev" ;;
    production) instance="prod" ;;
  esac
  file="$config_dir/$name.json"
  plan "$name" "$instance" "$file" || continue
  if [[ "$mode" == "apply" ]]; then apply "$name" "$instance" "$file"; fi
done
