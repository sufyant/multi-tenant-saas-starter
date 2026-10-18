# @repo/api-client

The typed client of the API, for every frontend: `apps/web`, `apps/platform` and later `apps/mobile`. No frontend writes an API type by hand. Rule N11a in `apps/api/docs/ARCHITECTURE.md` and rule F1 in `docs/frontend/ARCHITECTURE.md` say why.

## Where the files come from

The C# request and response types are the source. They sit at the end of each endpoint file (N7).

1. The API build writes the OpenAPI document `apps/api/openapi/v1.json` from them. The file is committed.
2. `pnpm api:generate` runs `pnpm generate` in this package:
   - Orval reads the document with `orval.config.ts` and writes `src/generated/`: the types and enum objects (`endpoints.schemas.gen.ts`), one folder for each operation tag with its functions and query options (`tenant-members/tenant-members.gen.ts`) and its MSW handlers (`tenant-members.msw.gen.ts`), and the Zod schemas (`zod/tenant-members/tenant-members.zod.gen.ts`).
   - `scripts/generate-operations.mjs` writes `src/generated/operations.gen.ts`, each operation's method, path, tag, request headers and declared response headers, and `src/generated/msw.gen.ts`, the index of the MSW handlers.
3. `src/index.ts` exports the generated modules, the Zod schemas as `schemas`, the mutator's `configureApiClient` and `ProblemDetailsError`, the error helpers of `src/problem-details.ts`, `declaredResponseHeaders` and `findOperation` (`src/operations.ts`), and `createBff` (`src/bff.ts`), the BFF that each app's route handler builds (F6). `src/msw.ts` is a second entry, `@repo/api-client/msw`, for tests only.

Never edit a file in `src/generated/`. Change the C# type, build the API, and run `pnpm generate` at the repository root. Commit what it changes. `apps/api/docs/local-run.md` shows the steps.

CI fails in two cases:

- The committed `openapi/v1.json` is not what the API build writes (`OpenApiDocumentFileTests`).
- A committed file in `src/generated/` is not what `pnpm generate` writes from the committed document (job `generated` in `.github/workflows/ci.yml`).

## Names

Orval names each function after its operation id in camel case, and keeps these names:

| For the operation | Orval writes |
| --- | --- |
| The call | `listMembers(tenantId, params)` |
| The query options of a GET | `getListMembersQueryOptions(tenantId, params)` |
| The query key | `getListMembersQueryKey(tenantId, params)`: `["listMembers", tenantId, params]` |
| The hook of a GET (not used, F2) | `useListMembers` |
| The request's Zod schema | `schemas.InviteMemberBody`, and `schemas.ListMembersQueryParams` for the query |
| The MSW handler and its response | `getListMembersMockHandler(answer)`, `getListMembersResponseMock()` |
| A header the operation takes | the second parameter: `createTenant(body, { "Idempotency-Key": key })` |

An enum is a const object whose member names drop the dot of the value: `ErrorCode.tenantnot_found` is `tenant.not_found`, and `MemberRole.owner` is `owner`.

## Use

```ts
import { configureApiClient, getListMembersQueryOptions } from "@repo/api-client";
import { useQuery } from "@tanstack/react-query";

configureApiClient({ baseUrl: "/api" }); // once, in the app's query provider

const members = useQuery(getListMembersQueryOptions(tenantId, { page: 1 }));
```

The browser calls the app's BFF at `/api`, which adds the token (F6). The mutator sends the request and returns the parsed body. On an error status it throws a `ProblemDetailsError` with the `status` and the `problem`, or null when the body is not Problem Details. `actionFor(status, problem)` says what a screen does with it (F4). The client does nothing else: no retry, no cache, no token.

## Tools and licenses

| Package | Use | License |
| --- | --- | --- |
| `orval` | Generates `src/generated/` (development only) | MIT |
| `@tanstack/react-query` | The types of the query options | MIT |
| `zod` | The Zod schemas | MIT |
| `msw` | The generated MSW handlers and the tests (development only) | MIT |
| `@faker-js/faker` | The generated response mocks (development only) | MIT |

Their dependencies are under MIT, ISC, Apache-2.0 or BSD. `.github/dependency-review-config.yml` holds the licenses a dependency may have.

## Tests

`pnpm test` runs three files:

- `src/mutator.test.ts`: the calls on the generated functions, against MSW: the forwarded headers, a body, a path parameter, the code of an error answer, `Idempotency-Key`, and a type error on a status the document does not list.
- `src/index.test.ts`: the document's limits and enums reach the Zod schemas and the const objects; `AuditDetailsResponse` narrows by `operation`; `declaredResponseHeaders`.
- `src/problem-details.test.ts`: each branch of `actionFor`.

The BFF's tests are in each app (`src/app/api/[...path]/route.test.ts`), because they go through the route handler.

`pnpm check-types` compiles them against the generated files, so a generation that drops an operation, a parameter or a value fails it.
