import { Language } from "@repo/api-client";
import { describe, expect, it } from "vitest";
import { chooseLocale, directionOf, directions } from "./locale";

const supported = ["en", "tr", "ar"] as const;

describe("locale", () => {
  it("ChooseLocale_CookieOfAKnownLanguage_WinsOverTheBrowser", () => {
    expect(chooseLocale("ar", "tr-TR,tr;q=0.9", supported, "en")).toBe("ar");
  });

  it("ChooseLocale_NoCookie_TakesTheBestKnownBrowserLanguage", () => {
    expect(
      chooseLocale(
        undefined,
        "de-DE,de;q=0.9,tr;q=0.8,en;q=0.5",
        supported,
        "en",
      ),
    ).toBe("tr");
  });

  it("ChooseLocale_NothingKnown_IsTheFallback", () => {
    expect(chooseLocale("xx", "de-DE", supported, "en")).toBe("en");
  });

  it("Directions_EveryLanguageOfTheContract_HasAnEntry", () => {
    expect(Object.keys(directions).sort()).toEqual(
      Object.values(Language).sort(),
    );
  });

  it("DirectionOf_Arabic_IsRightToLeft", () => {
    expect(directionOf("ar")).toBe("rtl");
  });

  it("DirectionOf_Turkish_IsLeftToRight", () => {
    expect(directionOf("tr")).toBe("ltr");
  });
});
