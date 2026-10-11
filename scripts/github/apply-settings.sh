#!/usr/bin/env bash
# Applies and checks the GitHub settings in docs/github-settings.md.
# It uses only `gh api` and the user's own `gh auth login` session.
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: scripts/github/apply-settings.sh <owner/repo> [--template] [--check]

Applies the settings in docs/github-settings.md to a GitHub repository,
then checks them. A second run changes nothing.

  --template  Set is_template to true. Without it, the value is false.
  --check     Change nothing. Print one line per setting (OK or DIFF)
              and exit with 1 if any line is DIFF or FAIL.
  -h, --help  Print this help.

Requires: gh, logged in with `gh auth login` as a repository admin.
EOF
}

repo=""
template=false
check_only=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    -h | --help) usage; exit 0 ;;
    --template) template=true ;;
    --check) check_only=true ;;
    -*) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
    *)
      if [[ -n "$repo" ]]; then echo "Only one repository is allowed." >&2; exit 2; fi
      repo="$1"
      ;;
  esac
  shift
done

if [[ ! "$repo" =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]]; then
  usage >&2
  exit 2
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ruleset_file="$script_dir/../../.github/rulesets/main.json"
ruleset_json="$(<"$ruleset_file")"

failed=0
out=""
err=""
err_file="$(mktemp)"
trap 'rm -f "$err_file"' EXIT

# Runs `gh api` with the given arguments. Stores stdout in $out and stderr in $err.
api() {
  local rc=0
  out="$(gh api "$@" 2>"$err_file")" || rc=$?
  err="$(<"$err_file")"
  return "$rc"
}

# Prints a FAIL line that names the setting and the reason, and records the failure.
fail() {
  local reason="${2//$'\n'/ }"
  if [[ "$reason" == *"HTTP 403"* ]]; then
    reason="$reason (the plan may not include this feature; see Plan limits in docs/github-settings.md)"
  fi
  printf 'FAIL %s: %s\n' "$1" "$reason"
  failed=1
}

compare() {
  if [[ "$2" == "$3" ]]; then
    printf 'OK   %s\n' "$1"
  else
    printf 'DIFF %s expected=%s actual=%s\n' "$1" "$2" "$3"
    failed=1
  fi
}

# Reads one endpoint and compares fields. Each spec is "label|jq expression|expected value".
# A 404 response means the feature is off, so each field reads as "not-found".
check_fields() {
  local endpoint="$1"
  shift
  local labels=() exprs=() expected=() spec label expr value filter actual i
  for spec in "$@"; do
    IFS='|' read -r label expr value <<<"$spec"
    labels+=("$label")
    exprs+=("$expr")
    expected+=("$value")
  done
  filter="$(IFS=,; printf '%s' "${exprs[*]}")"
  if ! api "$endpoint" --jq "[$filter] | map(tostring) | .[]"; then
    if [[ "$err" == *"HTTP 404"* ]]; then
      for i in "${!labels[@]}"; do compare "${labels[i]}" "${expected[i]}" "not-found"; done
    else
      for label in "${labels[@]}"; do fail "$label" "$err"; done
    fi
    return 0
  fi
  i=0
  while IFS= read -r actual; do
    compare "${labels[i]}" "${expected[i]}" "$actual"
    i=$((i + 1))
  done <<<"$out"
}

# The endpoint returns 204 when the feature is on and 404 when it is off.
check_enabled_by_status() {
  local label="$1" endpoint="$2"
  if api "$endpoint" --silent; then
    compare "$label" true true
  elif [[ "$err" == *"HTTP 404"* ]]; then
    compare "$label" true false
  else
    fail "$label" "$err"
  fi
}

# Prints the id of the repository ruleset named "main", or nothing.
find_ruleset_id() {
  api --paginate "repos/$repo/rulesets?includes_parents=false" \
    --jq '.[] | select(.name == "main" and .source_type == "Repository") | .id'
}

# Compares .github/rulesets/main.json with the live ruleset. The JSON file is also a
# valid jq literal, so gh's built-in jq does the comparison. Arrays compare without order.
check_ruleset() {
  local id state key want have
  if ! find_ruleset_id; then fail "ruleset main" "$err"; return 0; fi
  id="$out"
  if [[ -z "$id" ]]; then
    compare "ruleset main" present absent
    return 0
  fi
  local filter="
    def sub(\$a; \$b):
      if (\$a | type) == \"object\" then
        (\$b | type) == \"object\" and all(\$a | keys[]; . as \$k | sub(\$a[\$k]; \$b[\$k]))
      elif (\$a | type) == \"array\" then
        (\$b | type) == \"array\" and (\$a | length) == (\$b | length)
        and all(\$a[]; . as \$x | any(\$b[]; sub(\$x; .)))
      else \$a == \$b end;
    ($ruleset_json) as \$want | . as \$have
    | \$want | keys[] as \$k
    | if sub(\$want[\$k]; \$have[\$k]) then \"OK|\(\$k)\"
      else \"DIFF|\(\$k)|\(\$want[\$k] | tojson)|\(\$have[\$k] | tojson)\" end"
  if ! api "repos/$repo/rulesets/$id" --jq "$filter"; then fail "ruleset main" "$err"; return 0; fi
  while IFS='|' read -r state key want have; do
    if [[ "$state" == OK ]]; then
      compare "ruleset main: $key" x x
    else
      compare "ruleset main: $key" "$want" "$have"
    fi
  done <<<"$out"
}

check_all() {
  echo "== General"
  check_fields "repos/$repo" \
    "is_template|.is_template|$template" \
    "has_wiki|.has_wiki|false" \
    "has_projects|.has_projects|false" \
    "has_discussions|.has_discussions|false" \
    "has_issues|.has_issues|true" \
    "allow_merge_commit|.allow_merge_commit|true" \
    "allow_squash_merge|.allow_squash_merge|false" \
    "allow_rebase_merge|.allow_rebase_merge|false" \
    "allow_auto_merge|.allow_auto_merge|false" \
    "allow_update_branch|.allow_update_branch|true" \
    "delete_branch_on_merge|.delete_branch_on_merge|true"

  echo "== Security"
  check_fields "repos/$repo" \
    "secret scanning|.security_and_analysis.secret_scanning.status|enabled" \
    "secret scanning push protection|.security_and_analysis.secret_scanning_push_protection.status|enabled"
  check_enabled_by_status "Dependabot alerts" "repos/$repo/vulnerability-alerts"
  check_fields "repos/$repo/automated-security-fixes" \
    "Dependabot security updates|.enabled|true"
  check_fields "repos/$repo/private-vulnerability-reporting" \
    "private vulnerability reporting|.enabled|true"
  check_fields "repos/$repo/code-scanning/default-setup" \
    "CodeQL default setup|.state|configured"

  echo "== Actions"
  check_fields "repos/$repo/actions/permissions" \
    "actions enabled|.enabled|true" \
    "allowed actions|.allowed_actions|selected" \
    "SHA pinning required|.sha_pinning_required|true"
  check_fields "repos/$repo/actions/permissions/selected-actions" \
    "GitHub-owned actions allowed|.github_owned_allowed|true" \
    "verified creators allowed|.verified_allowed|true" \
    "extra allowed patterns|.patterns_allowed|[]"
  check_fields "repos/$repo/actions/permissions/workflow" \
    "default workflow permissions|.default_workflow_permissions|read" \
    "actions can approve pull requests|.can_approve_pull_request_reviews|false"
  check_fields "repos/$repo/actions/permissions/fork-pr-contributor-approval" \
    "fork pull request approval|.approval_policy|all_external_contributors"

  echo "== Ruleset"
  check_ruleset
}

# Sends one write request with a JSON body (or no body when the body is empty).
apply_step() {
  local label="$1" method="$2" endpoint="$3" body="${4:-}"
  if [[ -n "$body" ]]; then
    api --method "$method" "$endpoint" --input - --silent <<<"$body" || fail "$label" "$err"
  else
    api --method "$method" "$endpoint" --silent || fail "$label" "$err"
  fi
}

apply_code_scanning() {
  if ! api "repos/$repo/code-scanning/default-setup" --jq '.state'; then
    fail "CodeQL default setup" "$err"
    return 0
  fi
  if [[ "$out" != configured ]]; then
    apply_step "CodeQL default setup" PATCH "repos/$repo/code-scanning/default-setup" '{"state":"configured"}'
  fi
}

apply_ruleset() {
  if ! find_ruleset_id; then fail "ruleset main" "$err"; return 0; fi
  if [[ -n "$out" ]]; then
    apply_step "ruleset main" PUT "repos/$repo/rulesets/$out" "$ruleset_json"
  else
    apply_step "ruleset main" POST "repos/$repo/rulesets" "$ruleset_json"
  fi
}

apply_all() {
  echo "== Applying settings to $repo"
  apply_step "general settings" PATCH "repos/$repo" "{
    \"is_template\": $template,
    \"has_wiki\": false,
    \"has_projects\": false,
    \"has_issues\": true,
    \"allow_merge_commit\": true,
    \"allow_squash_merge\": false,
    \"allow_rebase_merge\": false,
    \"allow_auto_merge\": false,
    \"allow_update_branch\": true,
    \"delete_branch_on_merge\": true
  }"
  echo "MANUAL has_discussions: no REST endpoint sets it. See Manual steps in docs/github-settings.md."
  apply_step "secret scanning" PATCH "repos/$repo" \
    '{"security_and_analysis":{"secret_scanning":{"status":"enabled"},"secret_scanning_push_protection":{"status":"enabled"}}}'
  # Dependabot security updates need Dependabot alerts, so the order matters.
  apply_step "Dependabot alerts" PUT "repos/$repo/vulnerability-alerts"
  apply_step "Dependabot security updates" PUT "repos/$repo/automated-security-fixes"
  apply_step "private vulnerability reporting" PUT "repos/$repo/private-vulnerability-reporting"
  apply_code_scanning
  apply_step "actions permissions" PUT "repos/$repo/actions/permissions" \
    '{"enabled":true,"allowed_actions":"selected","sha_pinning_required":true}'
  apply_step "allowed actions" PUT "repos/$repo/actions/permissions/selected-actions" \
    '{"github_owned_allowed":true,"verified_allowed":true,"patterns_allowed":[]}'
  apply_step "workflow permissions" PUT "repos/$repo/actions/permissions/workflow" \
    '{"default_workflow_permissions":"read","can_approve_pull_request_reviews":false}'
  apply_step "fork pull request approval" PUT "repos/$repo/actions/permissions/fork-pr-contributor-approval" \
    '{"approval_policy":"all_external_contributors"}'
  apply_ruleset
}

if [[ "$check_only" == false ]]; then
  apply_all
fi
check_all

exit "$failed"
