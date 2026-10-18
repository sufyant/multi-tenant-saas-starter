import type { z } from "zod";

// One Zod error map (F12): every issue becomes a message key of the shared messages (`validation.too_long`), never Zod's English.
// The form translates the key. An app installs it once: `z.config({ customError: zodErrorMap })`.

type Issue = z.core.$ZodRawIssue;

/** The message key of a Zod issue. */
export function messageKeyOf(issue: Issue): string {
  switch (issue.code) {
    case "too_big":
      return issue.origin === "string"
        ? "validation.too_long"
        : "validation.too_big";
    case "too_small":
      return issue.origin === "string" && issue.minimum === 1
        ? "validation.required"
        : "validation.too_small";
    case "invalid_format":
      return issue.format === "email"
        ? "validation.email_invalid"
        : "validation.format_invalid";
    case "invalid_value":
      return "validation.choose_value";
    case "invalid_type":
      return issue.input === undefined
        ? "validation.required"
        : "validation.invalid";
    default:
      return "validation.invalid";
  }
}

/** The error map to install with `z.config`. */
export const zodErrorMap = (issue: Issue) => ({ message: messageKeyOf(issue) });
