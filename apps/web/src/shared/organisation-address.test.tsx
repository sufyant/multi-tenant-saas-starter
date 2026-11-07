import { renderHook } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it } from "vitest";
import {
  useOrganisationAddress,
  WebAppUrlProvider,
} from "./organisation-address";

const wrapper =
  (url: string) =>
  ({ children }: { children: ReactNode }) => (
    <WebAppUrlProvider value={url}>{children}</WebAppUrlProvider>
  );

describe("useOrganisationAddress", () => {
  // The address as a person types it: the host of WEB_APP_URL and the slug, without the scheme (7.01).
  it("UseOrganisationAddress_WebAppUrl_ReturnsItsHostAndTheSlug", () => {
    const { result } = renderHook(() => useOrganisationAddress("northwind"), {
      wrapper: wrapper("https://app.starter.example"),
    });
    expect(result.current).toBe("app.starter.example/northwind");
  });

  // A local run keeps its port, so the address opens the same server.
  it("UseOrganisationAddress_WebAppUrlWithAPort_KeepsThePort", () => {
    const { result } = renderHook(() => useOrganisationAddress("northwind"), {
      wrapper: wrapper("http://localhost:3000"),
    });
    expect(result.current).toBe("localhost:3000/northwind");
  });
});
