import { FieldErrorCode } from "@repo/api-client";

// A field's error is a Zod message key of the form (F12, `validation.too_long`) or a field code of the API (`too_long`). Each
// becomes the board's text for that field (the Messages board), a key under `organisations`.

const tooLong = (error: string) =>
  error === FieldErrorCode.TooLong || error === "validation.too_long";

/** The name: too long, else empty (`tenant.name_invalid`, `required`). */
export const nameText = (error: string | undefined) =>
  error ? (tooLong(error) ? "nameTooLong" : "nameRequired") : undefined;

/**
 * The slug: a taken one (`tenant.slug_taken`, its own key) and a reserved one (`slug_reserved`) have one text (4.08); every other
 * refusal of its shape is `slug_invalid`.
 */
export const slugText = (error: string | undefined) => {
  if (!error) return undefined;
  return error === "slugTaken" || error === FieldErrorCode.SlugReserved
    ? "slugTaken"
    : "slugInvalid";
};

/** An email address: missing, malformed or too long. */
export const emailText = (error: string | undefined) =>
  error ? "emailInvalid" : undefined;

/** A suspension's reason: too long, else missing. */
export const reasonText = (error: string | undefined) =>
  error ? (tooLong(error) ? "reasonTooLong" : "reasonRequired") : undefined;
