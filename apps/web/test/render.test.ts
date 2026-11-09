import dns from "node:dns";
import { delay, HttpResponse, http } from "msw";
import { setupServer } from "msw/node";
import { afterAll, beforeAll, expect, it, vi } from "vitest";
import { origin } from "./render";

// MSW passes an idle connection of undici through to the real network after a request was aborted, and a later mocked request
// written on it fails when that connection fails (mswjs/interceptors#881). A lookup of a name that does not exist answers in
// about 100 ms on CI, inside the next request's time; this stand-in answers so for every name.
const server = setupServer(
  http.post(`${origin}/api/slow`, async () => {
    await delay(150);
    return HttpResponse.json({});
  }),
  http.post(`${origin}/api/fast`, async () => {
    await delay(50);
    return HttpResponse.json({});
  }),
);
beforeAll(() => {
  const lookup = dns.lookup;
  vi.spyOn(dns, "lookup").mockImplementation(((
    host: string,
    ...rest: unknown[]
  ) => {
    const callback = rest.at(-1) as (error: Error) => void;
    if (host === "localhost" || host === "127.0.0.1")
      return (lookup as (...args: unknown[]) => void)(host, ...rest);
    setTimeout(
      () =>
        callback(
          Object.assign(new Error(`getaddrinfo ENOTFOUND ${host}`), {
            code: "ENOTFOUND",
          }),
        ),
      110,
    );
  }) as typeof dns.lookup);
  server.listen({ onUnhandledFrame: "error" });
});
afterAll(() => {
  server.close();
  vi.restoreAllMocks();
});

// A request that the client aborts at its time limit, before the handler answers.
const aborted = () =>
  fetch(`${origin}/api/slow`, {
    method: "POST",
    signal: AbortSignal.timeout(100),
  }).catch(() => undefined);

it("Origin_AfterTwoAbortedRequests_AnswersTheNextMockedRequest", async () => {
  await aborted();
  await aborted();

  const answer = await fetch(`${origin}/api/fast`, { method: "POST" });

  expect(answer.status).toBe(200);
});
