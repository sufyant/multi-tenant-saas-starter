import { renderHook, waitFor } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { useTabTitle } from "./use-tab-title";

describe("useTabTitle", () => {
  it("UseTabTitle_Title_WritesTheTabsTitle", () => {
    renderHook(() => useTabTitle("(2) Members · Starter"));

    expect(document.title).toBe("(2) Members · Starter");
  });

  // Next.js streams the page's metadata after the first paint, and its title would replace the shell's (generateMetadata,
  // "Streaming metadata").
  it("UseTabTitle_AnotherWriteAfterIt_WritesTheTitleAgain", async () => {
    renderHook(() => useTabTitle("Organisations · Starter Platform"));

    document.title = "[Local] Starter Platform";

    await waitFor(() =>
      expect(document.title).toBe("Organisations · Starter Platform"),
    );
  });
});
