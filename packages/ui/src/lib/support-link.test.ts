import { describe, expect, it } from "vitest";
import { shortRevision, supportLink } from "./support-link";

describe("supportLink", () => {
  it("SupportLink_Mailto_AddsTheVersionToTheBody", () => {
    expect(
      supportLink("mailto:support@starter.example", "Version: a1b2c3d"),
    ).toBe("mailto:support@starter.example?body=Version%3A%20a1b2c3d");
  });

  it("SupportLink_Https_StaysAsItIs", () => {
    expect(supportLink("https://help.example/", "Version: a1b2c3d")).toBe(
      "https://help.example/",
    );
  });

  it("SupportLink_NoVersion_StaysAsItIs", () => {
    expect(supportLink("mailto:support@starter.example", undefined)).toBe(
      "mailto:support@starter.example",
    );
  });
});

describe("shortRevision", () => {
  it("ShortRevision_Commit_GivesItsFirstSevenCharacters", () => {
    expect(shortRevision("a1b2c3d4e5f60718293a4b5c6d7e8f9012345678")).toBe(
      "a1b2c3d",
    );
  });

  it("ShortRevision_None_GivesNone", () => {
    expect(shortRevision(undefined)).toBeUndefined();
  });
});
