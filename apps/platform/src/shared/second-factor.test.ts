import { describe, expect, it } from "vitest";
import { hasVerifiedSecondFactor } from "./second-factor";

describe("hasVerifiedSecondFactor", () => {
  it.each<[readonly [number, number] | null]>([[null], [[0, -1]], [[3, -1]]])(
    "HasVerifiedSecondFactor_NoSecondFactorIn_%j_IsFalse",
    (age) => {
      expect(hasVerifiedSecondFactor(age)).toBe(false);
    },
  );

  it.each<[readonly [number, number]]>([[[0, 0]], [[12, 4]]])(
    "HasVerifiedSecondFactor_SecondFactorIn_%j_IsTrue",
    (age) => {
      expect(hasVerifiedSecondFactor(age)).toBe(true);
    },
  );
});
