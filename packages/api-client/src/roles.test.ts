import { describe, expect, it } from "vitest";
import { BuiltInRole, builtInRoleOf } from "./index";

describe("builtInRoleOf", () => {
  it.each(Object.values(BuiltInRole))(
    "BuiltInRoleOf_BuiltIn%s_IsThatRole",
    (name) => {
      expect(builtInRoleOf({ name, builtIn: true })).toBe(name);
    },
  );

  it("BuiltInRoleOf_CustomRoleNamedOwner_IsNull", () => {
    expect(builtInRoleOf({ name: BuiltInRole.Owner, builtIn: false })).toBe(
      null,
    );
  });

  it("BuiltInRoleOf_CustomRole_IsNull", () => {
    expect(builtInRoleOf({ name: "Billing lead", builtIn: false })).toBe(null);
  });
});
