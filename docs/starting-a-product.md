# Starting a product

This guide starts a product from the template and keeps the two connected. It has five parts:

1. Start the product's repository, with the template as its upstream.
2. Add the product's parts at the extension points, with the example product "Projects".
3. Bring the template's changes into the product.
4. Move a general feature from the product into the template.
5. What can conflict, and what to do.

The rules behind this guide are in `docs/roadmap.md`, step 41, and in X1 to X8 of `apps/api/docs/ARCHITECTURE.md`.

## The idea in one paragraph

Each extension point has two lists: the template's list and the product's list. The product's list is a file of its own, such as `ProductPermissions.cs`. The template ships each product file empty and never changes it. A product changes only its product files and its own folders. So the template's changes and the product's changes never touch the same lines, and `git merge upstream/main` brings the template's changes in without a conflict (the maintainer's decision of 2026-11-25).

## 1. Start the product's repository

The product's history must start from the template's history. Otherwise git cannot merge the template's later changes.

Do not use GitHub's "Use this template". It makes a new history without the template's commits, so each later merge needs `--allow-unrelated-histories` and conflicts on every file. Do not fork either: a fork of a public repository cannot be private.

1. On GitHub, create an empty repository for the product. Add no README, no license and no `.gitignore`.
2. Clone the template, with the remote named `upstream`, and push it to the product's repository:

   ```sh
   git clone --origin upstream https://github.com/sufyant/multi-tenant-saas-starter.git my-product
   cd my-product
   git remote add origin https://github.com/<owner>/my-product.git
   git push -u origin main
   ```

3. Install and generate, as in the template:

   ```sh
   pnpm install
   pnpm generate
   ```

4. Set the product's identity in `packages/brand/brand.json`: the name, the support address, the privacy policy and the status page (`packages/brand/README.md`).
5. Set up the services and the environments: `docs/deployment.md`, section 4. A private repository needs a paid GitHub plan for some steps: `docs/deployment.md`, section 12.

The CI job "Example product: build and test" runs in the product too. Once a product list is not empty, the job stops at its first step and passes (`scripts/ci/test-example-product.sh`).

## 2. Add the product's parts

The example product "Projects" uses every extension point. Its files are in `apps/api/tests/ExampleProduct/files`, at the same paths as in a product. Copy the shape of a file from there. The template never builds these files; the example job copies them over the template and runs the tests (X4).

After a change to a list of the API, run `pnpm generate` and commit what it writes. The CI job "Generated files: current" fails otherwise.

### 2.1 The API

| What you add | Where | The example's file | Rule |
| --- | --- | --- | --- |
| A module | A folder `apps/api/src/Modules/<Name>/` with the projects of a template module (`Api`, `Application`, `Contracts`, `Domain`, `Infrastructure`) | `Modules/Projects/` | X8; `apps/api/docs/ARCHITECTURE.md`, section 2 |
| The module in the solution | Run `dotnet sln apps/api/Api.slnx add <project>` for each project of the module | `scripts/ci/test-example-product.sh` | X8 |
| The module in the host | `apps/api/src/Api/ProductModules.props` (the `ProjectReference` of the module's `Api` and `Infrastructure`) and `apps/api/src/Api/Hosting/ProductModules.cs` (its handler assembly, its services, its tenant purge participant and its endpoints) | the same two files | X8 |
| A tenant table | The module's migration. Add one row for the table to `apps/api/tests/Api.IntegrationTests/Fixtures/ProductTenantTables.cs`, so the isolation tests check it | `ProductTenantTables.cs` | X8; R9 |
| Permissions | `ControlPlane.Domain/ProductPermissions.cs`: the constants, the `Tenant` and `Platform` sets, and the sets of the Admin and the Member | `ProductPermissions.cs` | X1; X2 |
| Features and limits | `ControlPlane.Domain/Plans/ProductFeatureKeys.cs` and `ProductLimitKeys.cs`. A module checks its own limit with `IEntitlementDirectory.ReadLimitAsync` | the same two files; `GetProjectLimitHandler.cs` | X1; X3 |
| Usage meters | `ControlPlane.Domain/Plans/ProductMeters.cs` | `ProductMeters.cs` | X5 |
| Audit operations | `Audit.Domain/ProductAuditOperations.cs`. The module records its entry with `RecordAuditEntryCommand` | `ProductAuditOperations.cs`; `CreateProjectHandler.cs` | X6; X8 |
| Notification types | `Notifications.Domain/ProductNotifications.cs`. The module sends `SendNotificationCommand` | `ProductNotifications.cs`; `NotifyProjectCreatedHandler.cs` | X7 |
| The texts of the emails and the notifications | `packages/emails/messages/product/{en,tr,ar}.json`: a word for each feature and limit key, and the title and the body of each notification type | `packages/emails/messages/product/` | X1; F54 |

A product's permission, key, meter, operation or notification type needs no migration of the template's tables: no constraint lists them.

### 2.2 The tenant web and the platform

| What you add | Where | Rule |
| --- | --- | --- |
| A section in the tenant web's sidebar | `apps/web/src/features/shell/product-sections.ts`: its id, its icon and the permission that shows it. Its pages go under `apps/web/src/app/(app)/[tenantSlug]/(shell)/<id>/`, and its code under `apps/web/src/features/<id>/` | F59 |
| A group of permissions on the roles screen | `apps/web/src/features/roles/product-permissions.ts`: the group's id and its permissions | F14 |
| The words of the screens | `apps/web/messages/product/{en,tr,ar}.json` and `apps/platform/messages/product/{en,tr,ar}.json` | F14 |

The words of the screens, by key:

- A section's name: `shell.<id>`.
- A group's name: `roles.groups.<id>`. A permission's label: `permissions.<permission>`, and the line under it: `roles.hints.<permission>`.
- A meter's name: `usage.meters.<key>` in the web, and `plans.meters.<key>` in the platform.

A key with dots nests. Write the meter `projects.exports` as `{ "plans": { "meters": { "projects": { "exports": "Exports" } } } }`. Without its word, a screen shows the key, so a missing word shows on the screen and breaks nothing. Give each word in all three languages.

The example product shows no screen, so these files have no example. The tests `tenant-shell.test.tsx`, `product-permissions.test.tsx` and `usage-section.test.tsx` show each one with a product's entry.

### 2.3 Tests

Write the tests of the module as the template writes its own (`apps/api/AGENTS.md`). The template's tests check every module: the isolation of every tenant table, every route, the terms gate and the tenant purge. Run them all in the product, so they check the product's module too.

## 3. Bring the template's changes in

Merge. Never rebase: a rebase rewrites the product's published history (Git documentation, "The Perils of Rebasing").

```sh
git fetch upstream
git switch -c template-update origin/main
git merge upstream/main
pnpm install
pnpm generate
git add --all
git commit -m "chore: the generated files after the template's changes"
git push -u origin template-update
```

Open a pull request of `template-update` in the product's repository. Merge it when CI is green.

- Read the template's `docs/roadmap.md` and `docs/decisions.md` in the merge's diff. A new rule can ask the product for a change.
- A migration of the template comes with the merge. It runs at the next deploy, as the product's own migrations do (`docs/deployment.md`, section 8).
- Merge often. A small merge is easy to read and to test.

## 4. Move a general feature into the template

A feature that the product built first can be useful for every product. It moves into the template in a pull request of the template. The product then gives up its copy.

1. In the template, copy the module's folder `apps/api/src/Modules/<Name>/` unchanged, with its migrations.
2. Move each line from the product's lists to the template's lists: `ProductPermissions` to `PermissionPools`, `ProductFeatureKeys` to `FeatureKeys`, `ProductLimitKeys` to `LimitKeys`, `ProductMeters` to `MeterCatalog.Template`, `ProductAuditOperations` to `AuditOperations`, `ProductNotifications` to `NotificationCatalog.Template`, and the words from `messages/product/` to the template's messages.
3. Register the module where the template registers its own: `Api.csproj` and `ModuleComposition` in place of `ProductModules.props` and `ProductModules.cs`, and `TenantTables` in place of `ProductTenantTables`. Add the projects to the solution.
4. Move the screens the same way: the section into the template's menu, the group into `permissions.ts`.
5. Keep every stored name: the schema, the tables, the migrations' ids, the permissions, the keys, the meters, the audit operations and the notification types. The product's database already holds them, so a new name would lose its data.
6. Run `pnpm generate`, the tests and the example job, and open the template's pull request.

At the next merge in the product (section 3):

1. Delete the product's copy of the module folder. The template's copy has the same path, so the merge already shows the files as the template's.
2. Delete the moved lines from the product's lists, the registration lines from `ProductModules.props` and `ProductModules.cs`, and the moved words from `messages/product/`.
3. Search the product for each moved name, such as `tenant.projects.read`: it must be only in the template's files. Then run `pnpm generate` and the tests.

## 5. What can conflict

A merge does not conflict in the product's lists or in the product's folders. Only the files that both repositories change can conflict:

| File | Why both change it | On a conflict |
| --- | --- | --- |
| `packages/brand/brand.json` | The product sets its identity; the template rarely adds a field | Keep the product's values and add the template's new field |
| `.railway/`, `.clerk/`, `.stripe/`, `.betterstack/`, `.cloudflare/` | The product sets its own services; the template adds settings | Keep the product's values and add the template's new settings |
| Generated files, such as `apps/api/openapi/v1.json` | Both repositories generate them | Take either side, then run `pnpm generate` and commit what it writes |

Any other conflict means that the product changed a file of the template. Move that change into a product file, or into the template (section 4).
