#!/usr/bin/env bash
# Plans and applies the Clerk instance settings in .clerk/config.json.
# The one file holds the settings of both instances.
# It uses only the `clerk` CLI and the user's own `clerk auth login` session
# (or CLERK_PLATFORM_API_KEY).
#
# Inputs: plan or apply, and an instance (development or production; plan without one handles both);
# CLERK_APP_ID (optional), and for apply to production CLERK_GOOGLE_CLIENT_ID, CLERK_GOOGLE_CLIENT_SECRET,
# CLERK_MICROSOFT_CLIENT_ID and CLERK_MICROSOFT_CLIENT_SECRET. Exit codes: 0 when the plan or the apply
# ends, an apply the user declines included; 1 when the repository is not linked, a variable is missing
# or the CLI fails; 2 for a wrong argument.
set -euo pipefail

usage() {
  cat <<'EOF_USAGE'
Usage: scripts/clerk/config.sh plan [development|production]
       scripts/clerk/config.sh apply development|production

  plan   Change nothing. Print, for each instance, the difference between
         .clerk/config.json and the live instance. Without an instance,
         plan handles both instances.
  apply  Print the same difference for one instance, ask, then apply it.

Requires: clerk, jq, and `clerk auth login` (or CLERK_PLATFORM_API_KEY).
The Clerk application is CLERK_APP_ID if it is set, else the application
that `clerk link` linked to this repository.
Apply to production also requires CLERK_GOOGLE_CLIENT_ID,
CLERK_GOOGLE_CLIENT_SECRET, CLERK_MICROSOFT_CLIENT_ID and
CLERK_MICROSOFT_CLIENT_SECRET. The development instance uses Clerk's shared
OAuth credentials.
EOF_USAGE
}

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
if [[ "$mode" == "apply" && -z "$only" ]]; then
  echo "apply needs an instance: development or production." >&2
  usage >&2
  exit 2
fi

# Without CLERK_APP_ID, the CLI uses its own link to this repository.
if [[ -z "${CLERK_APP_ID:-}" ]] \
  && ! clerk whoami --json 2>/dev/null | jq -e '.linked != null' >/dev/null; then
  echo "Run clerk link first, or set CLERK_APP_ID." >&2
  exit 1
fi

file="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.clerk" && pwd)/config.json"
body_file="$(mktemp)"
chmod 600 "$body_file"
trap 'rm -f "$body_file"' EXIT

# Prints the difference for one instance. The CLI writes the difference to
# stderr and the full before and after config to stdout. The script prints
# only the difference, because the full config can hold OAuth client secrets.
plan() {
  local name="$1" instance="$2" file="$3" err
  echo "== $name"
  if ! err="$(clerk config patch --mode agent ${CLERK_APP_ID:+--app "$CLERK_APP_ID"} --instance "$instance" \
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
  if ! err="$(clerk config patch --mode agent ${CLERK_APP_ID:+--app "$CLERK_APP_ID"} --instance "$instance" \
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
  plan "$name" "$instance" "$file" || continue
  if [[ "$mode" == "apply" ]]; then apply "$name" "$instance" "$file"; fi
done
