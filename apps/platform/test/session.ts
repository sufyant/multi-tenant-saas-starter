import { ClerkAPIResponseError } from "@clerk/nextjs/errors";
import { vi } from "vitest";

/**
 * A Clerk session that answers the session reverification as Clerk does (Khorikov, Chapter 9: fake only what we do not own). Each
 * call of `getToken` writes "token" into the log, so a test sees whether the fresh token came before a request.
 */
export function fakeSession(log: string[] = []) {
  return {
    factorVerificationAge: [0, 12],
    getToken: vi.fn(async () => {
      log.push("token");
      return "token";
    }),
    startVerification: vi.fn(async () => ({ status: "needs_second_factor" })),
    attemptSecondFactorVerification: vi.fn(
      async (): Promise<{ status: string }> => ({ status: "complete" }),
    ),
  };
}

/** Clerk's answer to a wrong authenticator code. */
export const wrongCode = () =>
  new ClerkAPIResponseError("refused", {
    data: [{ code: "form_code_incorrect", message: "", long_message: "" }],
    status: 422,
  });
