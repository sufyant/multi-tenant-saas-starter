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

  it("ServerReach_QueryGets503_SaysDown", async () => {
    const queryClient = client();
    const reach = serverReach(queryClient);
    const changes: boolean[] = [];
    reach.subscribe(() => changes.push(reach.isDown()));

    await queryClient
      .fetchQuery({
        queryKey: ["a"],
        queryFn: () => Promise.reject(answer(503)),
      })
      .catch(() => undefined);

    expect(reach.isDown()).toBe(true);
    expect(changes).toContain(true);
  });

  it("ServerReach_AnswerAfterA503_SaysUp", async () => {
    const queryClient = client();
    const reach = serverReach(queryClient);
    reach.subscribe(() => undefined);
    await queryClient
      .fetchQuery({
        queryKey: ["a"],
        queryFn: () => Promise.reject(answer(503)),
      })
      .catch(() => undefined);

    await queryClient.fetchQuery({ queryKey: ["b"], queryFn: () => 1 });

    expect(reach.isDown()).toBe(false);
  });

  it("ServerReach_403_StaysUp", async () => {
    const queryClient = client();
    const reach = serverReach(queryClient);
    reach.subscribe(() => undefined);

    await queryClient
      .fetchQuery({
        queryKey: ["a"],
        queryFn: () => Promise.reject(answer(403)),
      })
      .catch(() => undefined);

    expect(reach.isDown()).toBe(false);
  });
});
