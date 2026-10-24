import { describe, expect, it } from "vitest";
import {
  ErrorCode,
  ProblemDetailsError,
  problemOf,
  referenceOf,
} from "./index";

const traceId = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

describe("problemOf", () => {
  it("ProblemOf_ProblemDetailsError_IsItsProblemWithItsReference", () => {
    const problem = { code: ErrorCode.TenantNotFound, traceId };

    expect(problemOf(new ProblemDetailsError(404, problem))).toEqual({
      ...problem,
      reference: "4bf92f3577b34da6a3ce929d0e0e4736",
    });
  });

  it("ProblemOf_AnswerThatIsNoProblemDetails_IsNull", () => {
    expect(problemOf(new ProblemDetailsError(502, null))).toBeNull();
  });

  it("ProblemOf_DroppedConnection_IsNull", () => {
    expect(problemOf(new TypeError("Failed to fetch"))).toBeNull();
  });
});

describe("referenceOf", () => {
  it("ReferenceOf_W3cTraceId_IsItsTraceIdDigits", () => {
    expect(referenceOf(traceId)).toBe("4bf92f3577b34da6a3ce929d0e0e4736");
  });

  it("ReferenceOf_OtherId_IsTheWholeId", () => {
    expect(referenceOf("00-trace")).toBe("00-trace");
  });
});
