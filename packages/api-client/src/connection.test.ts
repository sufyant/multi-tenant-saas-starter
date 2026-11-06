import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";
import { isServerUnreachable, serverReach } from "./connection";
import { ErrorCode } from "./generated/endpoints.schemas.gen";
import { ProblemDetailsError } from "./mutator";

const answer = (status: number) =>
  new ProblemDetailsError(status, {
    code: ErrorCode.ServerError,
    traceId: "00-trace-01",
  });

describe("isServerUnreachable", () => {
  it.each([502, 503, 504])("IsServerUnreachable_%s_IsTrue", (status) => {
    expect(isServerUnreachable(answer(status))).toBe(true);
  });

  // fetch rejects with a TypeError when no answer comes at all.
  it("IsServerUnreachable_NetworkError_IsTrue", () => {
    expect(isServerUnreachable(new TypeError("Failed to fetch"))).toBe(true);
  });

  it.each([400, 403, 404, 500])("IsServerUnreachable_%s_IsFalse", (status) => {
    expect(isServerUnreachable(answer(status))).toBe(false);
  });

  it("IsServerUnreachable_CancelledRequest_IsFalse", () => {
    expect(
      isServerUnreachable(new DOMException("The user aborted.", "AbortError")),
    ).toBe(false);
  });
});

describe("serverReach", () => {
  const client = () =>
    new QueryClient({ defaultOptions: { queries: { retry: false } } });
  /** A store that a test follows, as the shell's bar does, with every value it said. */
  const follow = (queryClient: QueryClient) => {
    const reach = serverReach(queryClient);
    const said: boolean[] = [];
    reach.subscribe(() => said.push(reach.isDown()));
    return { reach, said };
  };
  const noServer = () => new TypeError("Failed to fetch");
  const call = (
    queryClient: QueryClient,
    key: string,
    result: () => Promise<unknown>,
    retry = 0,
  ) =>
    queryClient
      .fetchQuery({ queryKey: [key], queryFn: result, retry, retryDelay: 0 })
      .catch(() => undefined);
  const fails = (error: unknown) => () => Promise.reject(error);
  const answers = () => Promise.resolve(1);

  // No flash (the Connection bar): one failed call alone says nothing.
  it("ServerReach_OneCallFindsNoServer_StaysUp", async () => {
    const queryClient = client();
    const { reach, said } = follow(queryClient);

    await call(queryClient, "a", fails(noServer()));

    expect(reach.isDown()).toBe(false);
    expect(said).not.toContain(true);
  });

  it("ServerReach_ItsRetryFindsNoServerToo_SaysDown", async () => {
    const queryClient = client();
    const { reach } = follow(queryClient);

    await call(queryClient, "a", fails(noServer()), 1);

    expect(reach.isDown()).toBe(true);
  });

  it("ServerReach_NextCallFindsNoServerToo_SaysDown", async () => {
    const queryClient = client();
    const { reach } = follow(queryClient);

    await call(queryClient, "a", fails(noServer()));
    await call(queryClient, "b", fails(noServer()));

    expect(reach.isDown()).toBe(true);
  });

  // Safari and iOS fail a fetch with "Load failed" when the page navigates or goes to the background.
  it("ServerReach_OneLoadFailed_StaysUp", async () => {
    const queryClient = client();
    const { reach } = follow(queryClient);

    await call(queryClient, "a", fails(new TypeError("Load failed")));

    expect(reach.isDown()).toBe(false);
  });

  // The computer wakes or the tab comes back: every query reads again at once, and every one fails. That is one try.
  it("ServerReach_CallsThatFailTogether_StayUp", async () => {
    const queryClient = client();
    const { reach } = follow(queryClient);

    await Promise.all([
      call(queryClient, "a", fails(noServer())),
      call(queryClient, "b", fails(noServer())),
      call(queryClient, "c", fails(noServer())),
    ]);

    expect(reach.isDown()).toBe(false);
  });

  it("ServerReach_TryAfterCallsThatFailedTogether_Decides", async () => {
    const queryClient = client();
    const { reach } = follow(queryClient);
    await Promise.all([
      call(queryClient, "a", fails(noServer())),
      call(queryClient, "b", fails(noServer())),
    ]);

    await call(queryClient, "a", fails(noServer()));

    expect(reach.isDown()).toBe(true);
  });

  // A deploy: one 503 and then an answer shows no bar.
  it("ServerReach_One503ThenAnAnswer_NeverSaysDown", async () => {
    const queryClient = client();
    const { reach, said } = follow(queryClient);

    await call(queryClient, "a", fails(answer(503)));
    await call(queryClient, "a", answers);

    expect(reach.isDown()).toBe(false);
    expect(said).not.toContain(true);
  });

  it("ServerReach_Two503s_SaysDown", async () => {
    const queryClient = client();
    const { reach, said } = follow(queryClient);

    await call(queryClient, "a", fails(answer(503)), 1);

    expect(reach.isDown()).toBe(true);
    expect(said).toContain(true);
  });

  it("ServerReach_TwoNetworkErrors_SaysNoAnswer", async () => {
    const queryClient = client();
    const { reach } = follow(queryClient);

    await call(queryClient, "a", fails(noServer()), 1);

    expect(reach.noAnswer()).toBe(true);
  });

  // A proxy's 502, 503 or 504 is an answer, so the bar says "isn't answering", never "offline".
  it("ServerReach_Two503s_SaysAnAnswerCame", async () => {
    const queryClient = client();
    const { reach } = follow(queryClient);

    await call(queryClient, "a", fails(answer(503)), 1);

    expect(reach.noAnswer()).toBe(false);
  });

  it("ServerReach_AnswerAfterTwo503s_SaysUp", async () => {
    const queryClient = client();
    const { reach } = follow(queryClient);
    await call(queryClient, "a", fails(answer(503)), 1);

    await call(queryClient, "b", answers);

    expect(reach.isDown()).toBe(false);
  });

  // A refusal is an answer: it ends the doubt of the failure before it.
  it("ServerReach_RefusalBetweenTwoFailures_StaysUp", async () => {
    const queryClient = client();
    const { reach } = follow(queryClient);

    await call(queryClient, "a", fails(noServer()));
    await call(queryClient, "b", fails(answer(403)));
    await call(queryClient, "c", fails(noServer()));

    expect(reach.isDown()).toBe(false);
  });

  // A call that the app or the browser cancels counts as nothing: it neither decides nor ends the doubt.
  it("ServerReach_CancelledCallAfterAFailure_CountsAsNothing", async () => {
    const queryClient = client();
    const { reach } = follow(queryClient);
    const cancelled = new DOMException("The user aborted.", "AbortError");

    await call(queryClient, "a", fails(noServer()));
    await call(queryClient, "b", fails(cancelled));
    expect(reach.isDown()).toBe(false);

    await call(queryClient, "c", fails(noServer()));
    expect(reach.isDown()).toBe(true);
  });

  it("ServerReach_CommandAfterAFailedQuery_SaysDown", async () => {
    const queryClient = client();
    const { reach } = follow(queryClient);
    await call(queryClient, "a", fails(noServer()));

    await queryClient
      .getMutationCache()
      .build(queryClient, { mutationFn: fails(noServer()) })
      .execute(undefined)
      .catch(() => undefined);

    expect(reach.isDown()).toBe(true);
  });

  it("ServerReach_403_StaysUp", async () => {
    const queryClient = client();
    const { reach } = follow(queryClient);

    await call(queryClient, "a", fails(answer(403)), 1);

    expect(reach.isDown()).toBe(false);
  });
});
