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

/**
 * The options of one call: the generated function's `RequestInit`, and `onResponseHeaders`, which receives the headers of a
 * successful answer, such as the `Location` of a 201 (RFC 9110, section 10.2.2). It never goes into the request.
 */
export type ApiFetchInit = RequestInit & {
  onResponseHeaders?: (headers: Headers) => void;
};

/** The error type of every generated function. Orval reads this name from the mutator. */
export type ErrorType<_Error> = ProblemDetailsError;

let configured: ApiClientOptions | undefined;

/** Sets the base address and the headers of every call. An app calls it once, before its first query. */
export function configureApiClient(options: ApiClientOptions): void {
  configured = options;
}

/** The address of an API path through the base: `/api/v1/...` in the apps. The live stream's `EventSource` opens it (F6). */
export function apiUrl(path: string): string {
  if (!configured)
    throw new Error("configureApiClient() must run before the first API call.");
  return `${configured.baseUrl}${path}`;
}

/**
 * It sends the request through `send`, the one place in this package that calls `fetch`. It returns the parsed body on success and throws a
 * ProblemDetailsError on an error status. No retry, no cache, no token: the BFF adds the token.
 */
export const apiFetch = async <T>(
  path: string,
  { onResponseHeaders, ...init }: ApiFetchInit = {},
): Promise<T> => {
  const response = await send(path, init);
  const body = await readBody(response);
  if (!response.ok)
    throw new ProblemDetailsError(
      response.status,
      isProblemDetails(body) ? body : null,
    );
  onResponseHeaders?.(response.headers);
  return body as T;
};

/** An export's file: its bytes and the name the web's server gave it in `Content-Disposition` (RFC 6266). */
export type DownloadedFile = { blob: Blob; name: string };

const fileName = /filename="([^"]+)"/;

/**
 * The file of an export, through the same one `fetch`. An error status throws its ProblemDetailsError (F4). An answer that the
 * server cuts midway, after a page failed, rejects while the body is read: half a file is never a success.
 */
export const apiDownload = async (
  path: string,
  init: RequestInit = {},
): Promise<DownloadedFile> => {
  const response = await send(path, init);
  if (!response.ok) {
    const body = await readBody(response);
    throw new ProblemDetailsError(
      response.status,
      isProblemDetails(body) ? body : null,
    );
  }
  const name = fileName.exec(
    response.headers.get("Content-Disposition") ?? "",
  )?.[1];
  if (!name)
    throw new Error(
      "The export's answer has no file name in Content-Disposition.",
    );
  return { blob: await response.blob(), name };
};

/** Sends a call through the base, with the configured headers and the call's own. */
function send(path: string, init: RequestInit): Promise<Response> {
  const headers = new Headers(configured?.headers);
  new Headers(init.headers).forEach((value, name) => {
    headers.set(name, value);
  });
  return fetch(apiUrl(path), { ...init, headers });
}

async function readBody(response: Response): Promise<unknown> {
  const text = await response.text();
  if (text === "") return undefined;
  const type = response.headers.get("Content-Type") ?? "";
  return type.includes("json") ? JSON.parse(text) : text;
}
