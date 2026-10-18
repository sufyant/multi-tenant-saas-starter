import {
  ErrorCode,
  type FieldErrorCode,
  type ProblemDetails,
} from "./generated/endpoints.schemas.gen";

/** What a screen does with an error answer (F4 in docs/frontend/ARCHITECTURE.md). */
export type ProblemAction =
  | { kind: "sign-in" }
  | { kind: "suspended" }
  | { kind: "field-errors"; fields: Record<string, FieldErrorCode[]> }
  | { kind: "message"; key: `errors.${ErrorCode}` };

/** True for a body of the API's Problem Details: it has a `code` and a `traceId` (N11g). */
export function isProblemDetails(body: unknown): body is ProblemDetails {
  return (
    typeof body === "object" &&
    body !== null &&
    typeof (body as Record<string, unknown>).code === "string" &&
    typeof (body as Record<string, unknown>).traceId === "string"
  );
}

/** The field codes of a validation answer, keyed by the field's JSON name (N11x). Empty for any other answer. */
export function fieldErrorsOf(
  problem: ProblemDetails,
): Record<string, FieldErrorCode[]> {
  return problem.errors ?? {};
}

/**
 * What to do with an error answer. A pure function, so a test covers each branch without React. A 401 goes to sign-in; a 403
 * `user.suspended` goes to the suspended screen; field errors go to the form; any other code shows its message `errors.<code>`.
 * A body that is not Problem Details shows the message of `server.error`.
 */
export function actionFor(
  status: number,
  problem: ProblemDetails | null,
): ProblemAction {
  if (status === 401) return { kind: "sign-in" };
  if (!problem)
    return { kind: "message", key: `errors.${ErrorCode.servererror}` };
  if (status === 403 && problem.code === ErrorCode.usersuspended)
    return { kind: "suspended" };
  const fields = fieldErrorsOf(problem);
  if (Object.keys(fields).length > 0) return { kind: "field-errors", fields };
  return { kind: "message", key: `errors.${problem.code}` };
}
