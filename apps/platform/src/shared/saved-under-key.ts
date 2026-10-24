import { ErrorCode, ProblemDetailsError } from "@repo/api-client";

/**
 * Whether the API saved this answer under the Idempotency-Key (I6), so the same intent sent later needs a new key (F48): a 4xx
 * answer. A request still running (409 `idempotency.key_in_progress`), a server failure and a dropped connection keep the key.
 */
export const savedUnderKey = (error: unknown) =>
  error instanceof ProblemDetailsError &&
  error.status < 500 &&
  error.problem?.code !== ErrorCode.IdempotencyKeyInProgress;
