import { describe, expect, expectTypeOf, it } from "vitest";
import {
  type AuditDetailsResponse,
  declaredResponseHeaders,
  Language,
  MemberRole,
  type RoleResponse,
  schemas,
} from "./index";

describe("schemas", () => {
  it("RenameTenantBody_NameOfTheLimit_IsAccepted", () => {
    expect(schemas.RenameTenantBody.safeParse({ name: "a".repeat(100) }).success).toBe(true);
  });

  it("RenameTenantBody_NameOverTheLimit_IsRefused", () => {
    expect(schemas.RenameTenantBody.safeParse({ name: "a".repeat(101) }).success).toBe(false);
  });

  it("InviteMemberBody_NullLanguage_IsAccepted", () => {
    const invite = { email: "ayse@northwind.example", role: MemberRole.member, language: null };

    expect(schemas.InviteMemberBody.safeParse(invite).success).toBe(true);
  });

  it("InviteMemberBody_ListedLanguage_IsAccepted", () => {
    const invite = { email: "ayse@northwind.example", role: MemberRole.member, language: "tr" };

    expect(schemas.InviteMemberBody.safeParse(invite).success).toBe(true);
  });

  it("InviteMemberBody_UnlistedLanguage_IsRefused", () => {
    const invite = { email: "ayse@northwind.example", role: MemberRole.member, language: "de" };

    expect(schemas.InviteMemberBody.safeParse(invite).success).toBe(false);
  });
});

// Narrowing by the discriminator, without a cast: TypeScript accepts `details.newRole` only inside the branch.
const newRoleOf = (details: AuditDetailsResponse) =>
  details.operation === "member.role_changed" ? details.newRole : null;

describe("types", () => {
  it("AuditDetailsResponse_NarrowedByOperation_HasTheShapeOfThatOperation", () => {
    type RoleChanged = Extract<AuditDetailsResponse, { operation: "member.role_changed" }>;
    type Renamed = Extract<AuditDetailsResponse, { operation: "tenant.renamed" }>;

    expectTypeOf<RoleChanged["newRole"]>().toEqualTypeOf<RoleResponse>();
    expectTypeOf<Renamed["newName"]>().toEqualTypeOf<string>();
    expectTypeOf<Renamed>().not.toHaveProperty("newRole");
    expectTypeOf(newRoleOf).returns.toEqualTypeOf<RoleResponse | null>();
  });
});

describe("enums", () => {
  it("Language_AtRunTime_ListsTheDocumentsValuesInOrder", () => {
    expect(Object.values(Language)).toEqual(["en", "tr", "ar"]);
  });

  it("MemberRole_AtRunTime_ListsTheDocumentsValuesInOrder", () => {
    expect(Object.values(MemberRole)).toEqual(["owner", "admin", "member"]);
  });
});

describe("declaredResponseHeaders", () => {
  it("DeclaredResponseHeaders_CreateTenant_HoldsLocationAndIdempotentReplayed", () => {
    expect(declaredResponseHeaders("CreateTenant")).toEqual(expect.arrayContaining(["Location", "Idempotent-Replayed"]));
  });

  it("DeclaredResponseHeaders_GetMe_HoldsNeither", () => {
    expect(declaredResponseHeaders("GetMe")).not.toContain("Location");
    expect(declaredResponseHeaders("GetMe")).not.toContain("Idempotent-Replayed");
  });
});
