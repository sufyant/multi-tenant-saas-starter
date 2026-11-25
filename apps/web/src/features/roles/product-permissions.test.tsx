import { BuiltInRole, Permission } from "@repo/api-client";
import { screen } from "@testing-library/react";
import { setupServer } from "msw/node";
import { afterAll, afterEach, beforeAll, expect, it, vi } from "vitest";
import {
  customRole,
  renderInTenant,
  tenantAnswers,
} from "../../../test/tenant";
import { RolePage } from "./role-page";

// The product's permission groups, which the template ships empty: this file gives one before the roles screen loads (X1).
vi.mock("./product-permissions", () => ({
  productPermissionGroups: [{ id: "projects", permissions: ["projects.read"] }],
}));
vi.mock("next/navigation", () => ({ useRouter: () => ({ push: vi.fn() }) }));
vi.mock("@clerk/nextjs", () => ({
  useClerk: () => ({
    on: (_: string, handler: (status: string) => void) => handler("ready"),
    off: vi.fn(),
    session: { getToken: async () => "token" },
  }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const reviewer = customRole(
  "0198f0a2-0000-7000-8000-0000000000c3",
  "Reviewer",
  [Permission.TenantProfileRead],
);

// A product's group follows the template's. The template ships no word for it, so its area and its permission read as their ids
// until the product names them in messages/product (F14, X1).
it("RoleEditor_AProductsGroupWithoutItsWords_FollowsTheTemplatesAndReadsAsItsIds", async () => {
  server.use(...tenantAnswers(BuiltInRole.Owner, undefined, [reviewer]));

  renderInTenant(<RolePage roleId={reviewer.id} />, {
    url: `/northwind/settings/roles/${reviewer.id}`,
  });

  expect(
    await screen.findByRole("checkbox", { name: "projects.read" }),
  ).toBeVisible();
  const areas = screen
    .getAllByRole("group")
    .map((area) => area.querySelector("legend > span")?.textContent);
  expect(areas.at(-1)).toBe("projects");
});
