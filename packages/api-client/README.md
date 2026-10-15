# @repo/api-client

The typed client of the API, for every frontend: `apps/web`, and later `apps/admin` and `apps/mobile`. No frontend writes an API type by hand. Rule N11 in `apps/api/docs/ARCHITECTURE.md` says why.

## Where the types come from

The C# request and response types are the source. They sit at the end of each endpoint file (N7).

1. The API build writes the OpenAPI document `apps/api/openapi/v1.json` from them. The file is committed.
2. `pnpm api:generate` writes `src/schema.ts` from that file with `openapi-typescript`.
3. `src/index.ts` exports the generated types and `createApiClient`.

Never edit `src/schema.ts`. Change the C# type, build the API, and run `pnpm api:generate`. Commit both files. `apps/api/docs/local-run.md` shows the steps.

CI fails in two cases:

- The committed `openapi/v1.json` is not what the API build writes (`OpenApiDocumentFileTests`).
- The committed `src/schema.ts` is not what `pnpm api:generate` writes from the committed document (job `node` in `.github/workflows/ci.yml`).

## Use

```ts
import { createApiClient } from "@repo/api-client";

const api = createApiClient({
  baseUrl: "https://api.example.com",
  getToken: () => session.getToken(),
});

const { data, error } = await api.GET("/v1/tenants/{tenantId}/members", {
  params: { path: { tenantId } },
});
```

`createApiClient` adds `Authorization: Bearer <token>` to each request. When `getToken` gives no token, the request goes without the header, and the API answers 401. The client does nothing else: no retry, no cache, no state. It is `openapi-fetch`, so `data` is the typed success body and `error` is the typed `ProblemDetails`, with its `code`.

## Tools and licenses

| Package | Use | License |
| --- | --- | --- |
| `openapi-typescript` | Generates `src/schema.ts` (development only) | MIT |
| `openapi-fetch` | The typed `fetch` client | MIT |

Their dependencies are under MIT, ISC or Apache-2.0. `.github/dependency-review-config.yml` holds the licenses a dependency may have.

## Tests

`pnpm check-types` compiles `src/index.test.ts` against the generated types: one admin, one me and one tenant operation. A generation that drops an operation, a path parameter or a status value fails it. `pnpm test` checks the header and the request each call sends.
