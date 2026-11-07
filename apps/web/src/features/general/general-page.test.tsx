import { BuiltInRole } from "@repo/api-client";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { setupServer } from "msw/node";
import { afterAll, afterEach, beforeAll, describe, expect, it } from "vitest";
import { free, renderInTenant, tenantAnswers } from "../../../test/tenant";
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
    expect(valueBeside("Plan")).toHaveTextContent("Pro");
  });

  it("GeneralPage_Owner_DescribesTheOrganisationUnderTheTitle", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner));

    renderInTenant(<GeneralPage />, { url });

    expect(
      await screen.findByText("Northwind Traders’s details."),
    ).toBeVisible();
  });

  it("GeneralPage_Owner_SaysOnlyThePlatformRenamesTheOrganisation", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner));

    renderInTenant(<GeneralPage />, { url });

    expect(await screen.findByText("Name", { selector: "dt" })).toBeVisible();
    expect(valueBeside("Name")).toHaveTextContent(
      "Only the platform can rename an organisation.",
    );
  });

  it("GeneralPage_Owner_ShowsTheAddressThatNeverChanges", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner));

    renderInTenant(<GeneralPage />, { url });

    expect(
      await screen.findByText("Address", { selector: "dt" }),
    ).toBeVisible();
    expect(valueBeside("Address")).toHaveTextContent(
      "app.example.test/northwind",
    );
    expect(valueBeside("Address")).toHaveTextContent("It never changes.");
    expect(screen.queryByText(/slug/i)).not.toBeInTheDocument();
  });

  it("GeneralPage_CopyTheAddress_PutsTheAddressOnTheClipboard", async () => {
    const user = userEvent.setup();
    server.use(...tenantAnswers(BuiltInRole.Owner));
    renderInTenant(<GeneralPage />, { url });

    await user.click(
      await screen.findByRole("button", { name: "Copy the address" }),
    );

    expect(await navigator.clipboard.readText()).toBe(
      "app.example.test/northwind",
    );
  });

  it("GeneralPage_ActiveOrganisation_ShowsTheStatusAsASuccessPill", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner));

    renderInTenant(<GeneralPage />, { url });

    expect(await screen.findByText("Active")).toHaveAttribute(
      "data-tone",
      "success",
    );
  });

  it("GeneralPage_Member_ShowsThePlanReadOnly", async () => {
    server.use(...tenantAnswers(BuiltInRole.Member, undefined, [], free));

    renderInTenant(<GeneralPage />, { url });

    expect(await screen.findByText("Plan", { selector: "dt" })).toBeVisible();
    expect(valueBeside("Plan")).toHaveTextContent("Free");
  });

  it("GeneralPage_InArabic_NamesThePlanRow", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner));

    renderInTenant(<GeneralPage />, { url, locale: "ar" });

    expect(await screen.findByText("الخطة", { selector: "dt" })).toBeVisible();
    expect(valueBeside("الخطة")).toHaveTextContent("Pro");
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
      await screen.findByRole("heading", { name: "You can’t open this page" }),
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
