import { operations } from "./generated/operations.gen";

export * from "./generated/endpoints.schemas.gen";
export * from "./generated/index.gen";
export * as schemas from "./generated/zod/index.zod.gen";
export {
  type ApiClientOptions,
  apiFetch,
  configureApiClient,
  type ErrorType,
  ProblemDetailsError,
} from "./mutator";
export {
  actionFor,
  fieldErrorsOf,
  isProblemDetails,
  type ProblemAction,
} from "./problem-details";

export type Operation = (typeof operations)[number];
export type OperationId = Operation["operationId"];

/** The response headers that `v1.json` declares for an operation, on any of its answers (decision 10). */
export function declaredResponseHeaders(
  operationId: OperationId,
): readonly string[] {
  return (
    operations.find((operation) => operation.operationId === operationId)
      ?.responseHeaders ?? []
  );
}

/** The operation of a method and a path such as `/v1/tenants/0198.../members`, or undefined for a path the document lacks. */
export function findOperation(
  method: string,
  path: string,
): Operation | undefined {
  return operations.find(
    (operation) =>
      operation.method === method.toUpperCase() &&
      new RegExp(`^${operation.path.replace(/\{[^}]+\}/g, "[^/]+")}$`).test(
        path,
      ),
  );
}
