import { describe, expect, it, vi } from "vitest";
import { createBff } from "./bff";

// createBff is shared by both apps' route handlers (F6); each app's route.test.ts tests it with MSW. These tests hold the anonymous
// path for both apps at once: `send` is the fake of the network.
const apiUrl = "https://api.example.test";
const answer = () => new Response(null, { status: 204 });

describe("createBff", () => {
  it("CreateBff_AnonymousOperationWithoutASession_SendsItWithoutAToken", async () => {
    const send = vi.fn(async () => answer());
    const bff = createBff({ apiUrl, getToken: async () => null, send });

    const response = await bff(
      new Request("https://app.example.test/api/v1/public/terms"),
    );

    expect(response.status).toBe(204);
    expect(send).toHaveBeenCalledOnce();
    const [, init] = send.mock.calls[0] as unknown as [string, RequestInit];
    expect(new Headers(init.headers).has("Authorization")).toBe(false);
  });

  it("CreateBff_SignedInOperationWithoutASession_Answers401AndSendsNothing", async () => {
    const send = vi.fn(async () => answer());
    const bff = createBff({ apiUrl, getToken: async () => null, send });

    const response = await bff(
      new Request("https://app.example.test/api/v1/me/terms"),
    );

    expect(response.status).toBe(401);
    expect(send).not.toHaveBeenCalled();
  });
});
