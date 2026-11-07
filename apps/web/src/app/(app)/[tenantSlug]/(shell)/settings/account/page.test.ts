import { describe, expect, it, vi } from "vitest";
import TenantPage from "./page";

const navigation = vi.hoisted(() => ({
  permanentRedirect: vi.fn((to: string) => {
    throw new Error(`redirect ${to}`);
  }),
}));
vi.mock("next/navigation", () => navigation);

describe("The old address under the organisation", () => {
  // The page opens without an organisation, at the platform's path (decision 69): an old link or bookmark leads there.
  it("TenantPage_OldAddress_LeadsToTheAddressWithoutAnOrganisation", () => {
    expect(() => TenantPage()).toThrow("redirect /settings/account");
    expect(navigation.permanentRedirect).toHaveBeenCalledWith(
      "/settings/account",
    );
  });
});
