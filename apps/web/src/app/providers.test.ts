import { retryUnlessClientError } from "@repo/api-client";
import { describe, expect, it } from "vitest";
import { newQueryClient } from "./providers";

describe("newQueryClient", () => {
  it("NewQueryClient_Queries_RetryUnlessClientError", () => {
    expect(newQueryClient().getDefaultOptions().queries?.retry).toBe(
      retryUnlessClientError,
    );
  });

  // The app's own calls are the truth (the Connection bar): the browser's offline signal holds no query and no command.
  it("NewQueryClient_QueriesAndCommands_GoWhateverTheBrowserSays", () => {
    const options = newQueryClient().getDefaultOptions();

    expect(options.queries?.networkMode).toBe("always");
    expect(options.mutations?.networkMode).toBe("always");
  });
});
