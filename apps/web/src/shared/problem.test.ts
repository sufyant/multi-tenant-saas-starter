import { ErrorCode, ProblemDetailsError } from "@repo/api-client";
import { describe, expect, it } from "vitest";
import { accountBlockOf } from "./problem";

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
