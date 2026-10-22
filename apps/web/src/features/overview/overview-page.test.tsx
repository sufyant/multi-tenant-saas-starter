import { screen } from "@testing-library/react";
import { setupServer } from "msw/node";
import { afterAll, afterEach, beforeAll, describe, expect, it } from "vitest";
import { BuiltInRole } from "@/shared/roles";
import { renderInTenant, tenantAnswers } from "../../../test/tenant";
import { OverviewPage } from "./overview-page";

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const valueBeside = (term: string) =>
  screen.getByText(term, { selector: "dt" }).nextElementSibling;

describe("OverviewPage", () => {
  it("OverviewPage_Admin_ShowsTheOrganisationWithItsSlugAndTheRole", async () => {
    server.use(...tenantAnswers(BuiltInRole.Admin));

    renderInTenant(<OverviewPage />, { url: "/northwind" });

    expect(
      await screen.findByRole("heading", { level: 1, name: "Overview" }),
    ).toBeInTheDocument();
    expect(valueBeside("Organisation")).toHaveTextContent(
      "Northwind Traders northwind",
    );
    expect(valueBeside("Your role")).toHaveTextContent("Admin");
    expect(
      screen.getByText(
        "Product sections appear in the rail as they are added. There is nothing else to do here right now.",
      ),
    ).toBeInTheDocument();
  });

  it("OverviewPage_Loaded_SaysWhenItWasUpdated", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner));

    renderInTenant(<OverviewPage />, { url: "/northwind" });

    expect(await screen.findByText("Updated")).toBeInTheDocument();
  });
});
