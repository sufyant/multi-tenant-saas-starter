import { describe, expect, it } from "vitest";
import { isNavigation } from "./navigation";

// A request of the browser, as each app's proxy sees it (F63).
const request = (headers: Record<string, string>, method = "GET") =>
  new Request("https://web.example.test/northwind/settings/general", {
    method,
    headers,
  });

describe("isNavigation", () => {
  // A navigation of the App Router is a fetch: Next.js strips its RSC header off the proxy's request.
  it("IsNavigation_AFetch_IsANavigation", () => {
    expect(isNavigation(request({ "Sec-Fetch-Dest": "empty" }))).toBe(true);
  });

  // Clerk's handshake runs on a document request, so a page load is no navigation.
  it.each(["document", "iframe"])(
    "IsNavigation_ADocumentRequest_%s_IsNot",
    (destination) => {
      expect(isNavigation(request({ "Sec-Fetch-Dest": destination }))).toBe(
        false,
      );
    },
  );

  // Without Fetch Metadata, Clerk's test is the Accept header.
  it("IsNavigation_NoFetchMetadataAcceptingHtml_IsNot", () => {
    expect(isNavigation(request({ Accept: "text/html,*/*" }))).toBe(false);
  });

  it("IsNavigation_NoFetchMetadataAcceptingAnythingElse_IsANavigation", () => {
    expect(isNavigation(request({ Accept: "*/*" }))).toBe(true);
  });

  it("IsNavigation_AServerAction_IsNot", () => {
    expect(
      isNavigation(
        request({ "Sec-Fetch-Dest": "empty", "Next-Action": "a1" }, "POST"),
      ),
    ).toBe(false);
  });

  it("IsNavigation_APost_IsNot", () => {
    expect(isNavigation(request({ "Sec-Fetch-Dest": "empty" }, "POST"))).toBe(
      false,
    );
  });
});
