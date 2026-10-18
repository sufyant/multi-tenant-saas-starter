import { ErrorCode } from "./generated/endpoints.schemas.gen";
import { declaredResponseHeaders, findOperation } from "./operations";

export type BffOptions = {
  /** The API's origin, such as https://api.example.com. A server variable; the browser never sees it (F7). */
  apiUrl: string;
  /** The signed-in user's session token, or null without a session. */
  getToken: () => Promise<string | null>;
  /** The global `fetch`. The route handler passes it, so only the handler calls it (F1). */
  send: (input: string, init: RequestInit) => Promise<Response>;
};

// RFC 9110, section 7.6.1: a proxy never forwards a hop-by-hop field.
const hopByHop =
  /^(connection|keep-alive|transfer-encoding|upgrade|te|trailer|proxy-.*)$/i;
// A separator or a dot written in percent-encoding could leave /v1 once the API decodes it.
const encodedSeparator = /%2f|%5c|%2e/i;
const prefix = "/api";

function problem(status: number, code: ErrorCode): Response {
  return Response.json(
    { status, code, traceId: "bff" },
    { status, headers: { "Content-Type": "application/problem+json" } },
  );
}

/**
 * The backend for frontend (F6). The browser calls `/api/v1/...` on the app's own origin; the handler adds the user's bearer
 * token, sends the call to `apiUrl/v1/...` and returns the API's status, body and declared headers. Any other path answers 404
 * without a call.
 */
export function createBff({
  apiUrl,
  getToken,
  send,
}: BffOptions): (request: Request) => Promise<Response> {
  return async (request) => {
    const url = new URL(request.url);
    const raw = url.pathname;
    if (
      !raw.startsWith(`${prefix}/v1/`) ||
      encodedSeparator.test(raw) ||
      raw.split("/").includes("..")
    ) {
      return problem(404, ErrorCode.routenot_found);
    }
    const path = raw.slice(prefix.length);
    const token = await getToken();
    if (!token) return problem(401, ErrorCode.authunauthenticated);

    const operation = findOperation(request.method, path);
    const headers = new Headers({ Authorization: `Bearer ${token}` });
    for (const name of [
      "Content-Type",
      "Accept",
      ...(operation?.requestHeaders ?? []),
    ]) {
      const value = request.headers.get(name);
      if (value !== null) headers.set(name, value);
    }
    const hasBody = request.method !== "GET" && request.method !== "HEAD";
    let answer: Response;
    try {
      answer = await send(`${apiUrl}${path}${url.search}`, {
        method: request.method,
        headers,
        body: hasBody ? await request.arrayBuffer() : undefined,
        redirect: "manual",
      });
    } catch {
      // The API did not answer: down, unreachable or timed out. The browser gets Problem Details, as for any error (F4).
      return problem(502, ErrorCode.servererror);
    }

    const passed = new Headers();
    for (const name of [
      "Content-Type",
      ...(operation ? declaredResponseHeaders(operation.operationId) : []),
    ]) {
      const value = answer.headers.get(name);
      if (value !== null && !hopByHop.test(name)) passed.set(name, value);
    }
    const location = passed.get("Location");
    if (location?.startsWith("/v1/"))
      passed.set("Location", `${prefix}${location}`);
    return new Response(answer.status === 204 ? null : answer.body, {
      status: answer.status,
      headers: passed,
    });
  };
}
