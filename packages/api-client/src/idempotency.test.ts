import { describe, expect, it } from "vitest";
import { ErrorCode, ProblemDetailsError, savedUnderKey } from "./index";

const traceId = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

describe("savedUnderKey", () => {
  it("SavedUnderKey_ARefusal_IsSaved", () => {
    expect(
      savedUnderKey(
        new ProblemDetailsError(409, {
          code: ErrorCode.UserEmailTaken,
          traceId,
        }),
      ),
    ).toBe(true);
  });

  it("SavedUnderKey_ARequestStillRunning_IsNotSaved", () => {
    expect(
      savedUnderKey(
        new ProblemDetailsError(409, {
          code: ErrorCode.IdempotencyKeyInProgress,
          traceId,
        }),
      ),
    ).toBe(false);
  });

  it("SavedUnderKey_AServerFailureOrADroppedConnection_IsNotSaved", () => {
    expect([
      savedUnderKey(
        new ProblemDetailsError(500, { code: ErrorCode.ServerError, traceId }),
      ),
      savedUnderKey(new TypeError("Failed to fetch")),
    ]).toEqual([false, false]);
  });
});
