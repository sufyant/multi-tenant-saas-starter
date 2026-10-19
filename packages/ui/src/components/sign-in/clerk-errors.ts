import { isClerkAPIResponseError } from "@clerk/nextjs/errors";

/** Where in the flow the error came: the same code means another thing in another step. */
export type SignInStep = "email" | "code" | "totp" | "backupCode" | "ticket";

/** A key under `signIn.errors` of packages/ui/messages. */
export type SignInErrorKey =
  | "emailRequired"
  | "emailInvalid"
  | "cannotSignIn"
  | "captcha"
  | "tooManyRequests"
  | "oauthDenied"
  | "alreadySignedIn"
  | "codeIncorrect"
  | "codeExpired"
  | "tooManyAttempts"
  | "totpIncorrect"
  | "backupCodeIncorrect"
  | "locked"
  | "invitationLinkInvalid"
  | "invitationLinkExpired"
  | "signInAgain"
  | "generic";

// Clerk's codes, from its Frontend API errors page (https://clerk.com/docs/guides/development/errors/frontend-api). The screen
// shows our own text in the user's language, never Clerk's message. No text tells whether an address has an account: strict
// enumeration protection keeps Clerk from saying it, and the codes that could say it share one neutral message.
const everywhere: Record<string, SignInErrorKey> = {
  too_many_requests: "tooManyRequests",
  verification_code_too_many_requests: "tooManyRequests",
  verification_code_rate_limit_exceeded: "tooManyRequests",
  captcha_invalid: "captcha",
  captcha_missing_token: "captcha",
  user_locked: "locked",
  session_exists: "alreadySignedIn",
  identifier_already_signed_in: "alreadySignedIn",
  not_allowed_access: "cannotSignIn",
  form_identifier_not_found: "cannotSignIn",
  strategy_for_user_invalid: "cannotSignIn",
  sign_up_mode_restricted: "cannotSignIn",
  sign_up_restricted_waitlist: "cannotSignIn",
  external_account_not_found: "cannotSignIn",
  oauth_access_denied: "oauthDenied",
  session_reverification_required: "signInAgain",
};

const byStep: Record<SignInStep, Record<string, SignInErrorKey>> = {
  email: {
    form_param_nil: "emailRequired",
    form_param_format_invalid: "emailInvalid",
  },
  code: {
    form_code_incorrect: "codeIncorrect",
    form_param_nil: "codeIncorrect",
    verification_expired: "codeExpired",
    verification_failed: "tooManyAttempts",
  },
  totp: {
    form_code_incorrect: "totpIncorrect",
    form_param_nil: "totpIncorrect",
    verification_failed: "locked",
  },
  backupCode: {
    form_code_incorrect: "backupCodeIncorrect",
    form_param_nil: "backupCodeIncorrect",
    verification_failed: "locked",
  },
  ticket: {
    ticket_invalid_code: "invitationLinkInvalid",
    ticket_expired_code: "invitationLinkExpired",
  },
};

const logged = new Set<string>();

/** The code of an error that a Clerk method returned: the first API error's code, else the error's own. */
export function clerkErrorCode(error: unknown): string | undefined {
  if (isClerkAPIResponseError(error)) return error.errors[0]?.code;
  if (
    typeof error === "object" &&
    error !== null &&
    "code" in error &&
    typeof error.code === "string"
  )
    return error.code;
  return undefined;
}

/**
 * The message key of an error in a step. An unknown code shows the generic message, and the code is logged once, so a new code of
 * Clerk shows up in the console without a flood. Only the code is logged: never the address or a code the person typed.
 */
export function signInErrorKey(
  error: unknown,
  step: SignInStep,
): SignInErrorKey {
  const code = clerkErrorCode(error);
  const key = code && (byStep[step][code] ?? everywhere[code]);
  if (key) return key;
  const name = code ?? "none";
  if (!logged.has(name)) {
    logged.add(name);
    console.warn(`Sign-in: no message for the Clerk error code "${name}".`);
  }
  return "generic";
}
