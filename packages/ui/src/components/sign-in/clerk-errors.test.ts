import { ClerkAPIResponseError } from "@clerk/nextjs/errors";
import { afterEach, describe, expect, it, vi } from "vitest";
import { type SignInStep, signInErrorKey } from "./clerk-errors";

const apiError = (code: string) =>
  new ClerkAPIResponseError("refused", {
    data: [{ code, message: "", long_message: "" }],
    status: 422,
  });

afterEach(() => {
  vi.restoreAllMocks();
});

describe("signInErrorKey", () => {
  it.each<[string, SignInStep, string]>([
    ["form_param_nil", "email", "emailRequired"],
    ["form_param_format_invalid", "email", "emailInvalid"],
    ["form_identifier_not_found", "email", "cannotSignIn"],
    ["not_allowed_access", "email", "cannotSignIn"],
    ["sign_up_mode_restricted", "email", "cannotSignIn"],
    ["captcha_invalid", "email", "captcha"],
    ["too_many_requests", "email", "tooManyRequests"],
    ["oauth_access_denied", "email", "oauthDenied"],
    ["session_exists", "email", "alreadySignedIn"],
    ["form_code_incorrect", "code", "codeIncorrect"],
    ["verification_expired", "code", "codeExpired"],
    ["verification_failed", "code", "tooManyAttempts"],
    ["verification_code_too_many_requests", "code", "tooManyRequests"],
    ["form_code_incorrect", "totp", "totpIncorrect"],
    ["user_locked", "totp", "locked"],
    ["form_code_incorrect", "backupCode", "backupCodeIncorrect"],
    ["ticket_invalid_code", "ticket", "invitationLinkInvalid"],
    ["ticket_expired_code", "ticket", "invitationLinkExpired"],
  ])("SignInErrorKey_%s_In_%s_Is_%s", (code, step, key) => {
    expect(signInErrorKey(apiError(code), step)).toBe(key);
  });

  it("SignInErrorKey_UnknownCode_IsGenericAndLogsTheCodeOnlyOnce", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});

    expect(signInErrorKey(apiError("a_code_of_tomorrow"), "code")).toBe(
      "generic",
    );
    expect(signInErrorKey(apiError("a_code_of_tomorrow"), "totp")).toBe(
      "generic",
    );

    expect(warn).toHaveBeenCalledTimes(1);
    expect(warn).toHaveBeenCalledWith(
      expect.stringContaining("a_code_of_tomorrow"),
    );
  });
});
