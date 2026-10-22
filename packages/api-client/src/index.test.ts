import { describe, expect, expectTypeOf, it } from "vitest";
import {
  type AuditDetailsResponse,
  type AuditRoleResponse,
  declaredResponseHeaders,
  findOperation,
  Language,
  schemas,
} from "./index";

const memberRoleId = "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e30";

describe("schemas", () => {
  it("RenameTenantBody_NameOfTheLimit_IsAccepted", () => {
    expect(
      schemas.RenameTenantBody.safeParse({ name: "a".repeat(100) }).success,
    ).toBe(true);
  });

  it("RenameTenantBody_NameOverTheLimit_IsRefused", () => {
    expect(
      schemas.RenameTenantBody.safeParse({ name: "a".repeat(101) }).success,
    ).toBe(false);
  });

  it("InviteMemberBody_NullLanguage_IsAccepted", () => {
    const invite = {
      email: "ayse@northwind.example",
      roleId: memberRoleId,
      language: null,
    };

    expect(schemas.InviteMemberBody.safeParse(invite).success).toBe(true);
  });

  it("InviteMemberBody_ListedLanguage_IsAccepted", () => {
    const invite = {
      email: "ayse@northwind.example",
      roleId: memberRoleId,
      language: "tr",
    };

    expect(schemas.InviteMemberBody.safeParse(invite).success).toBe(true);
  });

  it("InviteMemberBody_UnlistedLanguage_IsRefused", () => {
    const invite = {
      email: "ayse@northwind.example",
      roleId: memberRoleId,
      language: "de",
    };

    expect(schemas.InviteMemberBody.safeParse(invite).success).toBe(false);
  });
});

// Narrowing by the discriminator, without a cast: TypeScript accepts `details.newRole` only inside the branch.
const newRoleOf = (details: AuditDetailsResponse) =>
  details.operation === "member.role_changed" ? details.newRole : null;

describe("types", () => {
  it("AuditDetailsResponse_NarrowedByOperation_HasTheShapeOfThatOperation", () => {
    type RoleChanged = Extract<
      AuditDetailsResponse,
      { operation: "member.role_changed" }
    >;
    type Renamed = Extract<
      AuditDetailsResponse,
      { operation: "tenant.renamed" }
    >;

    expectTypeOf<RoleChanged["newRole"]>().toEqualTypeOf<AuditRoleResponse>();
    expectTypeOf<Renamed["newName"]>().toEqualTypeOf<string>();
    expectTypeOf<Renamed>().not.toHaveProperty("newRole");
    expectTypeOf(newRoleOf).returns.toEqualTypeOf<AuditRoleResponse | null>();
  });
});

describe("enums", () => {
  it("Language_AtRunTime_ListsTheDocumentsValuesInOrder", () => {
    expect(Object.values(Language)).toEqual(["en", "tr", "ar"]);
  });
});

describe("declaredResponseHeaders", () => {
  it("DeclaredResponseHeaders_CreateTenant_HoldsLocationAndIdempotentReplayed", () => {
    expect(declaredResponseHeaders("CreateTenant")).toEqual(
      expect.arrayContaining(["Location", "Idempotent-Replayed"]),
    );
  });

  it("DeclaredResponseHeaders_GetMe_HoldsNeither", () => {
    expect(declaredResponseHeaders("GetMe")).not.toContain("Location");
    expect(declaredResponseHeaders("GetMe")).not.toContain(
      "Idempotent-Replayed",
    );
  });
});

describe("findOperation", () => {
  it("FindOperation_MethodAndPathOfATenantOperation_IsThatOperation", () => {
    const operation = findOperation(
      "get",
      "/v1/tenants/0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f/members",
    );

    expect(operation?.operationId).toBe("ListMembers");
  });

  it("FindOperation_PathTheDocumentLacks_IsUndefined", () => {
    expect(findOperation("GET", "/v1/tenants/x/members/y/z")).toBeUndefined();
  });

  it("FindOperation_MethodTheDocumentLacksForThePath_IsUndefined", () => {
    expect(findOperation("PUT", "/v1/me")).toBeUndefined();
  });
});
