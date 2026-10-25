import { ClerkAPIResponseError } from "@clerk/nextjs/errors";
import { ErrorCode, ProblemDetailsError } from "@repo/api-client";
import { CancelledError } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";
import { isNotSent } from "./not-sent";

const problem = (status: number, code: ErrorCode) =>
  new ProblemDetailsError(status, { code, traceId: "00-trace-01" });

const clerkError = (code: string) =>
  new ClerkAPIResponseError("refused", {
    data: [{ code, message: "", long_message: "" }],
    status: 422,
  });

const withDigest = (digest: string) =>
  Object.assign(new Error("control flow"), { digest });

describe("isNotSent", () => {
  it.each([
    ["an API 403 the screen shows", problem(403, ErrorCode.TenantSuspended)],
    ["an API 404 the screen shows", problem(404, ErrorCode.InvitationNotFound)],
    [
      "an API 500, which the API sends itself",
      problem(500, ErrorCode.ServerError),
    ],
    ["a Clerk code the sign-in shows", clerkError("form_code_incorrect")],
    ["a request the user cancelled", new DOMException("aborted", "AbortError")],
    ["a query TanStack Query cancelled", new CancelledError()],
    ["Next.js's redirect()", withDigest("NEXT_REDIRECT;replace;/sign-in;307;")],
    ["Next.js's notFound()", withDigest("NEXT_HTTP_ERROR_FALLBACK;404")],
  ])("IsNotSent_%s_IsTrue", (_, error) => {
    expect(isNotSent(error)).toBe(true);
  });

  it.each([
    ["a TypeError of the app", new TypeError("x is undefined")],
    ["a Clerk code without a message", clerkError("a_code_of_tomorrow")],
    ["a server error with its digest", withDigest("2847501833")],
    ["a thrown string", "failed"],
    ["nothing", undefined],
  ])("IsNotSent_%s_IsFalse", (_, error) => {
    expect(isNotSent(error)).toBe(false);
  });
});
