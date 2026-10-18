import type { ProblemDetails } from "./generated/endpoints.schemas.gen";
import { isProblemDetails } from "./problem-details";

export type ApiClientOptions = {
  /** Where the API's paths start. The apps use `/api`: the BFF route handler, which adds the token (F6). */
  baseUrl: string;
  /** Headers that every request sends. */
  headers?: Record<string, string>;
};

/** An error answer of the API. `problem` is null when the body is not Problem Details, such as an HTML page of a proxy. */
export class ProblemDetailsError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails | null;

  constructor(status: number, problem: ProblemDetails | null) {
    super(problem ? `${status} ${problem.code}` : `${status}`);
    this.name = "ProblemDetailsError";
    this.status = status;
    this.problem = problem;
  }
}

/** The error type of every generated function. Orval reads this name from the mutator. */
export type ErrorType<_Error> = ProblemDetailsError;

let configured: ApiClientOptions | undefined;

/** Sets the base address and the headers of every call. An app calls it once, before its first query. */
export function configureApiClient(options: ApiClientOptions): void {
  configured = options;
}

/**
 * The one place in this package that calls `fetch`. It sends the request, returns the parsed body on success and throws a
 * ProblemDetailsError on an error status. No retry, no cache, no token: the BFF adds the token.
 */
export const apiFetch = async <T>(
  path: string,
  init?: RequestInit,
): Promise<T> => {
  if (!configured)
    throw new Error("configureApiClient() must run before the first API call.");
  const headers = new Headers(configured.headers);
  new Headers(init?.headers).forEach((value, name) => {
    headers.set(name, value);
  });

  const response = await fetch(`${configured.baseUrl}${path}`, {
    ...init,
    headers,
  });
  const body = await readBody(response);
  if (!response.ok)
    throw new ProblemDetailsError(
      response.status,
      isProblemDetails(body) ? body : null,
    );
  return body as T;
};

async function readBody(response: Response): Promise<unknown> {
  const text = await response.text();
  if (text === "") return undefined;
  const type = response.headers.get("Content-Type") ?? "";
  return type.includes("json") ? JSON.parse(text) : text;
}
