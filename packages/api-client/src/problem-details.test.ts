import { describe, expect, it } from "vitest";
import type { ProblemDetails } from "./index";
import { actionFor, fieldErrorsOf, isProblemDetails } from "./problem-details";

const problem = (overrides: Partial<ProblemDetails>): ProblemDetails => ({
  code: "server.error",
  traceId: "00-trace",
  ...overrides,
});

describe("isProblemDetails", () => {
  it("IsProblemDetails_BodyWithCodeAndTraceId_IsTrue", () => {
    expect(
      isProblemDetails({
        code: "tenant.not_found",
        traceId: "00-trace",
        status: 404,
      }),
    ).toBe(true);
  });

  it("IsProblemDetails_BodyWithoutCode_IsFalse", () => {
    expect(isProblemDetails({ title: "Bad Gateway" })).toBe(false);
  });

  it("IsProblemDetails_Text_IsFalse", () => {
    expect(isProblemDetails("<html>")).toBe(false);
  });
});

describe("fieldErrorsOf", () => {
  it("FieldErrorsOf_ValidationAnswer_KeysEachFieldByItsName", () => {
    const answer = problem({
      code: "request.validation_failed",
      errors: { name: ["too_long"], email: ["required"] },
    });

    expect(fieldErrorsOf(answer)).toEqual({
      name: ["too_long"],
      email: ["required"],
    });
  });

  it("FieldErrorsOf_AnswerWithoutErrors_IsEmpty", () => {
    expect(fieldErrorsOf(problem({ code: "tenant.not_found" }))).toEqual({});
  });
});

describe("actionFor", () => {
  it("ActionFor_401_GoesToSignIn", () => {
    expect(actionFor(401, problem({ code: "auth.unauthenticated" }))).toEqual({
      kind: "sign-in",
    });
  });

  it("ActionFor_403UserSuspended_GoesToTheSuspendedScreen", () => {
    expect(actionFor(403, problem({ code: "user.suspended" }))).toEqual({
      kind: "account-suspended",
    });
  });

  it("ActionFor_403UserDeleted_GoesToTheDeletedAccountPage", () => {
    expect(actionFor(403, problem({ code: "user.deleted" }))).toEqual({
      kind: "account-deleted",
    });
  });

  it("ActionFor_409UserDeleted_ShowsTheMessageOfTheCode", () => {
    expect(actionFor(409, problem({ code: "user.deleted" }))).toEqual({
      kind: "message",
      key: "errors.user.deleted",
    });
  });

  it("ActionFor_ValidationAnswer_GivesTheFieldErrorsToTheForm", () => {
    const answer = problem({
      code: "request.validation_failed",
      errors: { name: ["too_long"] },
    });

    expect(actionFor(400, answer)).toEqual({
      kind: "field-errors",
      fields: { name: ["too_long"] },
    });
  });

  it("ActionFor_AnswerWithACode_ShowsTheMessageOfTheCode", () => {
    expect(actionFor(409, problem({ code: "member.last_owner" }))).toEqual({
      kind: "message",
      key: "errors.member.last_owner",
    });
  });

  it("ActionFor_403TenantSuspended_GoesToTheSuspendedOrganisationPage", () => {
    expect(actionFor(403, problem({ code: "tenant.suspended" }))).toEqual({
      kind: "tenant-blocked",
      status: "suspended",
    });
  });

  it("ActionFor_403TenantClosed_GoesToTheClosedOrganisationPage", () => {
    expect(actionFor(403, problem({ code: "tenant.closed" }))).toEqual({
      kind: "tenant-blocked",
      status: "closed",
    });
  });

  it("ActionFor_403OtherCode_ShowsTheMessageOfTheCode", () => {
    expect(
      actionFor(403, problem({ code: "member.owner_requires_owner" })),
    ).toEqual({
      kind: "message",
      key: "errors.member.owner_requires_owner",
    });
  });

  it("ActionFor_AnswerThatIsNoProblemDetails_ShowsTheMessageOfAServerError", () => {
    expect(actionFor(502, null)).toEqual({
      kind: "message",
      key: "errors.server.error",
    });
  });
});
