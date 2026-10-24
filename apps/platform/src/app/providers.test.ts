import { retryUnlessClientError } from "@repo/api-client";
import { describe, expect, it } from "vitest";
import { newQueryClient } from "./providers";

describe("newQueryClient", () => {
  it("NewQueryClient_Queries_RetryUnlessClientError", () => {
    expect(newQueryClient().getDefaultOptions().queries?.retry).toBe(
      retryUnlessClientError,
    );
  });
});
