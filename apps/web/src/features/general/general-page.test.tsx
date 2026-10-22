import { screen } from "@testing-library/react";
import { setupServer } from "msw/node";
import { afterAll, afterEach, beforeAll, describe, expect, it } from "vitest";
import { BuiltInRole } from "@/shared/roles";
import { renderInTenant, tenantAnswers } from "../../../test/tenant";
import { GeneralPage } from "./general-page";

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const url = "/northwind/settings/general";

/** The value beside a term of the description list. */
const valueBeside = (term: string) =>
  screen.getByText(term, { selector: "dt" }).nextElementSibling;

describe("GeneralPage", () => {
  it("GeneralPage_Owner_ShowsNameStatusCreatedAndRole", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner));

    renderInTenant(<GeneralPage />, { url });

    expect(
      await screen.findByRole("heading", { level: 1, name: "General" }),
    ).toBeInTheDocument();
    expect(valueBeside("Name")).toHaveTextContent("Northwind Traders");
    expect(valueBeside("Status")).toHaveTextContent("Active");
    expect(valueBeside("Created")).toHaveTextContent("5 Jan 2026");
    expect(valueBeside("Your role")).toHaveTextContent("Owner");
  });

  it("GeneralPage_Member_ShowsTheSameRowsAndNoForm", async () => {
    server.use(...tenantAnswers(BuiltInRole.Member));

    renderInTenant(<GeneralPage />, { url });

    expect(
      await screen.findByText("Your role", { selector: "dt" }),
    ).toBeInTheDocument();
    expect(valueBeside("Your role")).toHaveTextContent("Member");
    expect(screen.queryByRole("textbox")).not.toBeInTheDocument();
    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
  });

  it("GeneralPage_NoProfilePermission_SaysPermissionDenied", async () => {
    server.use(...tenantAnswers(BuiltInRole.Member, []));

    renderInTenant(<GeneralPage />, { url });

    expect(
      await screen.findByRole("heading", { name: "You can’t open General" }),
    ).toBeInTheDocument();
  });

  it("GeneralPage_InTurkish_ShowsTurkishTexts", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner));

    renderInTenant(<GeneralPage />, { url, locale: "tr" });

    expect(
      await screen.findByRole("heading", { level: 1, name: "Genel" }),
    ).toBeInTheDocument();
  });
});
