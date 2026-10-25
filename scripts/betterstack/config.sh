#!/usr/bin/env bash
# Plans and applies the Better Stack settings in .betterstack/config.json (.betterstack/README.md).
# The Errors applications serve every environment. The uptime monitors belong to one environment:
# the script names each one "<environment> <name>" and compares only the monitors with that prefix.
# It calls Better Stack's API with curl and two team-based tokens. It never prints a token, and
# never prints an Errors application's attributes other than its name and platform, because the
# API's answer holds the application's ingesting token.
#
# Inputs: plan or apply, and an environment (staging or production); BETTERSTACK_UPTIME_TOKEN,
# BETTERSTACK_TELEMETRY_TOKEN, and each URL variable that config.json names (API_URL, WEB_URL,
# PLATFORM_URL). Exit codes: 0 when the plan or the apply ends, an apply the user declines
# included; 1 when a token or a variable is missing or an API call fails; 2 for a wrong argument.
set -euo pipefail

usage() {
  cat <<'EOF_USAGE'
Usage: scripts/betterstack/config.sh plan staging|production
       scripts/betterstack/config.sh apply staging|production

  plan   Change nothing. Print the difference between .betterstack/config.json
         and the live Better Stack team: the Errors applications, and the
         uptime monitors of the environment.
  apply  Print the same difference, ask, then apply it.

Requires: curl and jq, and these variables:
  BETTERSTACK_UPTIME_TOKEN     the team's "Incidents API" token
  BETTERSTACK_TELEMETRY_TOKEN  the team's "Telemetry API" token
  API_URL, WEB_URL, PLATFORM_URL
                               the public URLs of the environment, the
                               GitHub environment variables of deploy.yml
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

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
file="$root/.betterstack/config.json"
uptime_api="https://incidents.betterstack.com/api/v2"
errors_api="https://errors.betterstack.com/api/v2"

while IFS= read -r var; do
  if [[ -z "${!var:-}" ]]; then
    echo "$var is not set. See docs/deployment.md, section 16." >&2
    exit 1
  fi
done < <(printf '%s\n' BETTERSTACK_UPTIME_TOKEN BETTERSTACK_TELEMETRY_TOKEN; jq -r '.uptime.monitors[].url_variable' "$file")

# Each token goes into a header file that only the user can read, so it is never on a command line.
work="$(mktemp -d)"
chmod 700 "$work"
trap 'rm -rf "$work"' EXIT
printf 'Authorization: Bearer %s\n' "$BETTERSTACK_UPTIME_TOKEN" >"$work/uptime.header"
printf 'Authorization: Bearer %s\n' "$BETTERSTACK_TELEMETRY_TOKEN" >"$work/errors.header"

# Calls the API: a method, a URL, the header file and an optional JSON body. Prints the answer's
# body. On a status other than 2xx it prints the method, the URL and the status, and exits 1.
call() {
  local method="$1" url="$2" header="$3" body="${4:-}" out status
  local args=(--silent --show-error --request "$method" --header "@$header"
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
  if [[ "$status" != 2?? ]]; then
    # Only the errors field: an answer can hold an application's token, an error never does.
    echo "$method $url answered $status: $(jq -c '.errors // empty' <<<"${out%$'\n'*}" 2>/dev/null || true)" >&2
    exit 1
  fi
  printf '%s' "${out%$'\n'*}"
}

# Reads every page of a list. Prints one JSON array of the items.
list() {
  local url="$1" header="$2" page items="[]"
  while [[ -n "$url" ]]; do
    # A command substitution does not inherit set -e, so a failed call must stop the loop here.
    page="$(call GET "$url" "$header")" || exit 1
    items="$(jq -c --argjson items "$items" '$items + .data' <<<"$page")"
    url="$(jq -r '.pagination.next // empty' <<<"$page")"
  done
  printf '%s' "$items"
}

# The live Errors applications, by name and platform only.
live_applications="$(list "$errors_api/applications?per_page=250" "$work/errors.header" \
  | jq -c 'map({id, name: .attributes.name, platform: .attributes.platform})')"
live_monitors="$(list "$uptime_api/monitors?per_page=250" "$work/uptime.header")"

# The monitors of the file, each with its name, the shared defaults, and its URL: the value of its
# variable and its path, or the health path of the file that it names.
desired_monitors="[]"
while IFS= read -r monitor; do
  variable="$(jq -r '.url_variable' <<<"$monitor")"
  path="$(jq -r '.path // empty' <<<"$monitor")"
  if [[ -z "$path" ]]; then
    path="$(jq -r '.healthPath' "$root/$(jq -r '.health_path_of' <<<"$monitor")")"
  fi
  desired_monitors="$(jq -c --argjson m "$monitor" --argjson defaults "$(jq -c '.uptime.defaults' "$file")" \
    --arg name "$environment $(jq -r '.name' <<<"$monitor")" --arg url "${!variable%/}$path" \
    '. + [$defaults + ($m | del(.name, .url_variable, .path, .health_path_of))
      + {pronounceable_name: $name, url: $url}]' <<<"$desired_monitors")"
done < <(jq -c '.uptime.monitors[]' "$file")

# The difference, as a list of actions. Only the keys that the file sets are compared.
actions="$(jq -c -n \
  --argjson want_apps "$(jq -c '.errors.applications' "$file")" \
  --argjson live_apps "$live_applications" \
  --argjson want_monitors "$desired_monitors" \
  --argjson live_monitors "$live_monitors" \
  --arg prefix "$environment " '
  ([ $want_apps[] as $w
     | ($live_apps | map(select(.name == $w.name)) | first) as $l
     | if $l == null then {kind: "application", op: "create", name: $w.name, body: $w}
       elif $l.platform != $w.platform then
         {kind: "application", op: "fixed", name: $w.name, from: $l.platform, to: $w.platform}
       else empty end ]
   + [ $live_apps[] | select(.name as $n | $want_apps | map(.name) | index($n) | not)
       | {kind: "application", op: "unmanaged", name} ]
   + [ $want_monitors[] as $w
       | ($live_monitors | map(select(.attributes.pronounceable_name == $w.pronounceable_name)) | first) as $l
       | if $l == null then {kind: "monitor", op: "create", name: $w.pronounceable_name, body: $w}
         else
           [ $w | to_entries[] | select(.value != $l.attributes[.key])
             | {key, from: $l.attributes[.key], to: .value} ] as $changes
           | if $changes == [] then empty
             else {kind: "monitor", op: "update", name: $w.pronounceable_name, id: $l.id,
                   changes: $changes, body: ($changes | map({(.key): .to}) | add)} end
         end ]
   + [ $live_monitors[]
       | select(.attributes.pronounceable_name | startswith($prefix))
       | select(.attributes.pronounceable_name as $n | $want_monitors | map(.pronounceable_name) | index($n) | not)
       | {kind: "monitor", op: "delete", name: .attributes.pronounceable_name, id} ])')"

echo "== Better Stack, $environment"
if [[ "$(jq 'length' <<<"$actions")" == 0 ]]; then
  echo "No difference."
  exit 0
fi
jq -r '.[] |
  if .op == "create" and .kind == "application" then
    "+ create Errors application \"\(.name)\": platform \(.body.platform)"
  elif .op == "create" then
    "+ create monitor \"\(.name)\": " + (.body | del(.pronounceable_name) | to_entries | map("\(.key) \(.value | tojson)") | join(", "))
  elif .op == "update" then
    "~ update monitor \"\(.name)\": " + (.changes | map("\(.key) \(.from | tojson) -> \(.to | tojson)") | join(", "))
  elif .op == "delete" then
    "- delete monitor \"\(.name)\": not in .betterstack/config.json"
  elif .op == "fixed" then
    "! Errors application \"\(.name)\" has platform \(.from), the file says \(.to). The API cannot change it: not applied. Change it by hand (docs/deployment.md, section 16)."
  else
    "! Errors application \"\(.name)\" is not in .betterstack/config.json. Left as is: deleting it deletes its errors."
  end' <<<"$actions"

if [[ "$mode" == "plan" ]]; then exit 0; fi
if [[ "$(jq '[.[] | select(.op == "create" or .op == "update" or .op == "delete")] | length' <<<"$actions")" == 0 ]]; then
  echo "Nothing to apply."
  exit 0
fi
read -r -p "Apply this to Better Stack, $environment? [y/N] " answer
if [[ "$answer" != "y" ]]; then echo "Not applied."; exit 0; fi

while IFS= read -r action; do
  op="$(jq -r '.op' <<<"$action")"
  kind="$(jq -r '.kind' <<<"$action")"
  name="$(jq -r '.name' <<<"$action")"
  id="$(jq -r '.id // empty' <<<"$action")"
  body="$(jq -c '.body // empty' <<<"$action")"
  case "$kind $op" in
    "application create") call POST "$errors_api/applications" "$work/errors.header" "$body" >/dev/null ;;
    "monitor create") call POST "$uptime_api/monitors" "$work/uptime.header" "$body" >/dev/null ;;
    "monitor update") call PATCH "$uptime_api/monitors/$id" "$work/uptime.header" "$body" >/dev/null ;;
    "monitor delete") call DELETE "$uptime_api/monitors/$id" "$work/uptime.header" >/dev/null ;;
    *) continue ;;
  esac
  echo "Applied: $op $kind \"$name\"."
done < <(jq -c '.[]' <<<"$actions")
