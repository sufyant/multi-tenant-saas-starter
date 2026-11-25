import { screen } from "@testing-library/react";
import { setupServer } from "msw/node";
import { afterAll, afterEach, beforeAll, expect, it, vi } from "vitest";
import {
  detailAnswer,
  detailOf,
  meAnswer,
  northwind,
} from "../../../test/organisations";
import { anyTenantPlanAnswer, plansAnswer } from "../../../test/plans";
import { renderWithProviders } from "../../../test/render";
import { usageAnswer } from "../../../test/usage";
import { OrganisationPage } from "./organisation-page";

// The product's texts, which the template ships empty: this file names a meter where the plan editor reads it (F14, X1). A key
// with dots nests, as next-intl reads a key: `ai.requests` is `ai` › `requests`.
vi.mock("../../../messages/product/en.json", () => ({
  default: { plans: { meters: { ai: { requests: "AI requests" } } } },
}));
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

// The Usage tab names a meter with the product's word under plans.meters, the one place of a meter's name in the platform.
it("UsageSection_AProductsWordForTheMeter_NamesTheMeterWithIt", async () => {
  window.history.replaceState(
    null,
    "",
    `/organisations/${northwind.id}?tab=usage`,
  );
  server.use(
    anyTenantPlanAnswer(),
    plansAnswer(),
    meAnswer(),
    detailAnswer(detailOf(northwind)),
    usageAnswer(),
  );

  renderWithProviders(<OrganisationPage tenantId={northwind.id} />);

  expect(
    await screen.findByRole("meter", { name: "7,200 of 10,000 AI requests" }),
  ).toBeVisible();
});
