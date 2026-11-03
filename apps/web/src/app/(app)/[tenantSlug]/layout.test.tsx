import { ReservedSlug } from "@repo/api-client";
import { describe, expect, it, vi } from "vitest";
import TenantLayout from "./layout";

vi.mock("next/navigation", () => ({
  notFound: () => {
    throw new Error("not found");
  },
}));

const layoutOf = (tenantSlug: string) =>
  TenantLayout({
    children: "the page",
    params: Promise.resolve({ tenantSlug }),
  });

describe("TenantLayout", () => {
  // A reserved slug is a top-level route's name (F58): /sign-in/missing is "Page not found" outside the shell.
  it.each(Object.values(ReservedSlug))(
    "TenantLayout_ReservedSlug_%s_IsNotFound",
    async (slug) => {
      await expect(layoutOf(slug)).rejects.toThrow("not found");
    },
  );

  it("TenantLayout_OrganisationsSlug_RendersItsPage", async () => {
    await expect(layoutOf("northwind")).resolves.toBe("the page");
  });
});
