import { describe, expect, it } from "vitest";
import { safeRedirect, signInAgainUrl } from "./safe-redirect";

const origin = "https://web.example.test";

describe("safeRedirect", () => {
  it.each([
    ["/northwind", "/northwind"],
    ["/northwind/settings?tab=one#top", "/northwind/settings?tab=one#top"],
    [`${origin}/invitations/accept?code=abc`, "/invitations/accept?code=abc"],
  ])("SafeRedirect_PageOfThisApp_%s_IsItsPath", (value, expected) => {
    expect(safeRedirect(value, origin)).toBe(expected);
  });

  it.each([
    [null],
    [""],
    ["https://evil.example/steal"],
    ["//evil.example/steal"],
    ["/\\evil.example"],
    ["javascript:alert(1)"],
    ["https://web.example.test.evil.example/"],
  ])("SafeRedirect_AnythingElse_%s_IsHome", (value) => {
    expect(safeRedirect(value, origin)).toBe("/");
  });
});

describe("signInAgainUrl", () => {
  it("SignInAgainUrl_Page_SaysTheSessionEndedAndComesBack", () => {
    expect(
      signInAgainUrl({
        pathname: "/northwind/settings/members",
        search: "?tab=invitations",
      }),
    ).toBe(
      "/sign-in?reason=session-ended&redirect_url=%2Fnorthwind%2Fsettings%2Fmembers%3Ftab%3Dinvitations",
    );
  });
});
