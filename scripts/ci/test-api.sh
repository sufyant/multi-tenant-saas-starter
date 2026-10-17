#!/usr/bin/env bash
# Runs the API's tests and watches the resident memory of every test process (section 9, rule 11 of
# apps/api/docs/ARCHITECTURE.md). All of Api.IntegrationTests in one process peaked at 8.9 GB in one run and
# at 11.0 GB in the next, on a runner with 16 GB. Most of it comes from the hosts that generate their handler
# code at runtime. So the tests run in two parts, one after the other, until a stopped test host leaves no
# memory behind. The first part runs every test that is not marked [RuntimeHandlerCode], in every project; the
# second part runs the marked classes. A process that passes 11 GB is stopped, so a regression fails this step
# with its reason, not a runner that runs out of memory and leaves no log.
#
# Inputs: none; run it from the repository root. Exit codes: the exit code of the first part of dotnet test
# that fails; 1 when a test process passed the limit. It prints the peak resident memory of each test process.
#
# Run it locally: scripts/ci/test-api.sh
set -euo pipefail

limit_gb=11
limit_kb=$((limit_gb * 1024 * 1024))
runtime_code=HandlerCode=Runtime

descendants() {
  local child
  for child in $(pgrep -P "$1" || true); do
    echo "$child"
    descendants "$child"
  done
}

# Runs dotnet test with the arguments and, once a second, reads the peak resident memory (VmHWM) of each
# process below it.
run_watched() {
  local peaks over=0 code=0 tests pid peak name tree
  peaks=$(mktemp)
  dotnet test --no-build --results-directory TestResults "$@" &
  tests=$!

  while kill -0 "$tests" 2>/dev/null; do
    for pid in $(descendants "$tests"); do
      peak=$(awk '/^VmHWM:/ { print $2 }' "/proc/$pid/status" 2>/dev/null || true)
      name=$(basename "$(tr '\0' '\n' 2>/dev/null <"/proc/$pid/cmdline" | head -n 1)" 2>/dev/null || true)
      [[ -n "$peak" && -n "$name" ]] || continue
      echo "$name $peak" >>"$peaks"
      if ((peak > limit_kb && over == 0)); then
        echo "::error::$name passed $limit_gb GB of resident memory ($((peak / 1024)) MB). The tests are stopped." \
          "A test host keeps more memory than before (HostMemoryTests, section 9, rule 11 of apps/api/docs/ARCHITECTURE.md)."
        over=1
        mapfile -t tree < <(descendants "$tests")
        kill "${tree[@]}" "$tests" 2>/dev/null || true
      fi
    done
    sleep 1
  done

  wait "$tests" || code=$?

  echo "Peak resident memory of each test process:"
  sort -k2,2nr "$peaks" | awk '!seen[$1]++ { printf "  %s: %d MB\n", $1, $2 / 1024 }'

  if ((over == 1)); then
    return 1
  fi
  return "$code"
}

cd apps/api
run_watched --solution Api.slnx -- --filter-not-trait "$runtime_code"
run_watched --project tests/Api.IntegrationTests -- --filter-trait "$runtime_code"
