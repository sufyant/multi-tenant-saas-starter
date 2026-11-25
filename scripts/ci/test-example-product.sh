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
# Run it locally: git stash -u, then scripts/ci/test-example-product.sh, then git reset --hard && git clean -fd.
# In a product, whose lists are not empty, the script stops with exit code 0 and changes nothing: the example
# would overwrite the product's own lists, and the product's own tests prove them.
set -euo pipefail

lists=(apps/api/src/Modules/ControlPlane/ControlPlane.Domain/ProductPermissions.cs
  apps/api/src/Modules/ControlPlane/ControlPlane.Domain/Plans/ProductFeatureKeys.cs
  apps/api/src/Modules/ControlPlane/ControlPlane.Domain/Plans/ProductLimitKeys.cs
  apps/api/src/Modules/Audit/Audit.Domain/ProductAuditOperations.cs
  apps/api/src/Modules/Notifications/Notifications.Domain/ProductNotifications.cs)
if grep -q "const string" "${lists[@]}"; then
  echo "The product's lists are not empty, so this is a product, not the template: the example product does not run."
  exit 0
fi

cp -R apps/api/tests/ExampleProduct/files/. .
pnpm generate

cd apps/api
dotnet build Api.slnx
# A product commits what the build writes, such as openapi/v1.json, so the tests compare with these files.
git add --all
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
