import { defineConfig } from "orval";

// One input, two outputs: the TanStack Query options with their types and MSW handlers, and the Zod schemas. Every output file ends
// in .gen.ts (D1, "Generated files" in apps/api/docs/ARCHITECTURE.md). tags-split writes one folder per operation tag, which is the
// audience and resource of the API's endpoints (N7d).
const input = { target: "../../apps/api/openapi/v1.json" };

export default defineConfig({
  client: {
    input,
    output: {
      mode: "tags-split",
      target: "src/generated/endpoints.gen.ts",
      fileExtension: ".gen.ts",
      client: "react-query",
      headers: true,
      httpClient: "fetch",
      clean: true,
      indexFiles: true,
      mock: { generators: [{ type: "msw", version: 3 }] },
      override: {
        mutator: { path: "src/mutator.ts", name: "apiFetch" },
        // Orval writes a query's options function only together with its hook, so both exist. The apps use the options only
        // (F2; Biome's noRestrictedImports refuses a `use...` import from this package).
        fetch: { includeHttpResponseReturnType: false },
        query: {
          useMutation: false,
          useOperationIdAsQueryKey: true,
          shouldExportKeys: true,
          signal: true,
          version: 5,
        },
        operations: {
          ListAuditEntries: {
            query: { useInfinite: true, useInfiniteQueryParam: "cursor" },
          },
        },
      },
    },
  },
  zod: {
    input,
    output: {
      mode: "tags-split",
      target: "src/generated/zod/zod.gen.ts",
      fileExtension: ".zod.gen.ts",
      client: "zod",
      clean: true,
      indexFiles: true,
    },
  },
});
