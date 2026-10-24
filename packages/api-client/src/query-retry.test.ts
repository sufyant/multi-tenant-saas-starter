import { describe, expect, it } from "vitest";
import { ErrorCode } from "./generated/endpoints.schemas.gen";
import { ProblemDetailsError } from "./mutator";
import { retryUnlessClientError } from "./query-retry";

const refused = (status: number, code: ErrorCode) =>
  new ProblemDetailsError(status, { code, traceId: "00-trace-01" });

describe("retryUnlessClientError", () => {
  it.each([ErrorCode.UserSuspended, ErrorCode.UserDeleted])(
    "RetryUnlessClientError_403_%s_NeverRetries",
    (code) => {
      expect(retryUnlessClientError(0, refused(403, code))).toBe(false);
    },
  );

  it("RetryUnlessClientError_404_NeverRetries", () => {
    expect(
      retryUnlessClientError(0, refused(404, ErrorCode.TenantNotFound)),
    ).toBe(false);
  });

  it("RetryUnlessClientError_409_NeverRetries", () => {
    expect(
      retryUnlessClientError(0, refused(409, ErrorCode.TenantNotActive)),
    ).toBe(false);
  });

  it("RetryUnlessClientError_500_Retries", () => {
    expect(retryUnlessClientError(0, refused(500, ErrorCode.ServerError))).toBe(
      true,
    );
  });

  it("RetryUnlessClientError_NetworkError_Retries", () => {
    expect(retryUnlessClientError(0, new TypeError("Failed to fetch"))).toBe(
      true,
    );
  });

  it.each([
    [0, true],
    [2, true],
    [3, false],
  ])(
    "RetryUnlessClientError_ServerErrorAfter%sFailures_Retries_%s",
    (count, retries) => {
      expect(
        retryUnlessClientError(count, refused(500, ErrorCode.ServerError)),
      ).toBe(retries);
    },
  );
});
