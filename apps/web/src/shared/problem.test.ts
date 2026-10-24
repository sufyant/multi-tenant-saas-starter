import { ErrorCode, ProblemDetailsError } from "@repo/api-client";
import { describe, expect, it } from "vitest";
import { accountBlockOf, retryUnlessAccountBlocked } from "./problem";

const refused = (status: number, code: ErrorCode) =>
  new ProblemDetailsError(status, { code, traceId: "00-trace-01" });

describe("accountBlockOf", () => {
  it.each([ErrorCode.UserSuspended, ErrorCode.UserDeleted])(
    "AccountBlockOf_403_%s_IsItsCode",
    (code) => {
      expect(accountBlockOf(refused(403, code))).toBe(code);
    },
  );

  it("AccountBlockOf_AnotherCode_IsNull", () => {
    expect(accountBlockOf(refused(403, ErrorCode.TenantSuspended))).toBeNull();
  });
});

describe("retryUnlessAccountBlocked", () => {
  it.each([ErrorCode.UserSuspended, ErrorCode.UserDeleted])(
    "RetryUnlessAccountBlocked_%s_NeverRetries",
    (code) => {
      expect(retryUnlessAccountBlocked(0, refused(403, code))).toBe(false);
    },
  );

  it.each([
    [0, true],
    [2, true],
    [3, false],
  ])(
    "RetryUnlessAccountBlocked_ServerErrorAfter%sFailures_Retries_%s",
    (count, retries) => {
      expect(
        retryUnlessAccountBlocked(count, refused(500, ErrorCode.ServerError)),
      ).toBe(retries);
    },
  );
});
