#!/usr/bin/env bash
# Builds the example product "Projects" on the template and runs its tests with the template's (X4 of
# apps/api/docs/ARCHITECTURE.md). The template ships its product lists empty (X1). This script copies
# apps/api/tests/ExampleProduct/files over the checkout, so the example fills each list, as a product would.
# Then it writes every generated file and runs the API's unit and architecture tests: the example's own tests
# prove each extension point, and the template's tests prove that a product's additions break none of them.
#
# Inputs: none; run it from the root of a checkout that you can throw away, because it changes the files.
# Exit codes: the exit code of the first command that fails.
#
# Run it locally: git stash -u, then scripts/ci/test-example-product.sh, then git checkout . && git clean -fd.
set -euo pipefail

cp -R apps/api/tests/ExampleProduct/files/. .
pnpm generate

cd apps/api
dotnet build Api.slnx
for project in \
  src/Modules/ControlPlane/ControlPlane.UnitTests \
  src/Modules/Notifications/Notifications.UnitTests \
  src/Modules/Billing/Billing.UnitTests \
  src/Shared/SharedKernel.UnitTests \
  src/Shared/Tenancy.UnitTests \
  tests/Api.UnitTests \
  tests/Api.ArchitectureTests; do
  dotnet test --no-build --project "$project"
done
