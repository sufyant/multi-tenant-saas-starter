import { describe, expect, it } from "vitest";
import { environmentOf, environmentPattern } from "./environment";

describe("environmentOf", () => {
  it.each([
    ["production", { kind: "production" }],
    ["staging", { kind: "staging" }],
    ["local", { kind: "local" }],
    ["preview-182", { kind: "preview", number: 182 }],
  ])("EnvironmentOf_%s_GivesItsKind", (value, environment) => {
    expect(environmentOf(value)).toEqual(environment);
  });
});

describe("environmentPattern", () => {
  it.each(["production", "staging", "local", "preview-182"])(
    "EnvironmentPattern_%s_Matches",
    (value) => {
      expect(environmentPattern.test(value)).toBe(true);
    },
  );

  it.each(["", "Production", "preview", "preview-0", "preview-x", "test"])(
    "EnvironmentPattern_%s_DoesNotMatch",
    (value) => {
      expect(environmentPattern.test(value)).toBe(false);
    },
  );
});
