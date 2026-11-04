import { BuiltInRole } from "@repo/api-client";
import { screen, within } from "@testing-library/react";
import { setupServer } from "msw/node";
import { afterAll, afterEach, beforeAll, describe, expect, it } from "vitest";
import { renderInTenant, tenantAnswers } from "../../../test/tenant";
import { HomePage } from "./home-page";

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

/** The cards' names, in order. */
const cards = () =>
  within(screen.getByRole("list"))
    .getAllByRole("link")
    .map((link) => link.querySelector("[data-slot=card-title]")?.textContent);

describe("HomePage", () => {
  it("HomePage_Owner_WelcomesByFirstNameAndOffersEveryPage", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner));

    renderInTenant(<HomePage />, { url: "/northwind" });

    expect(
      await screen.findByRole("heading", { level: 1, name: "Welcome, Ayşe" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { level: 1 }).nextElementSibling,
    ).toHaveTextContent("Northwind Traders");
    // A paragraph in the page's direction, so in Arabic the line starts at the right; only the name is isolated.
    expect(
      screen.getByRole("heading", { level: 1 }).nextElementSibling,
    ).toContainHTML(
      '<p class="text-body text-muted-foreground"><bdi>Northwind Traders</bdi></p>',
    );
    expect(cards()).toEqual(["Members", "Roles", "Activity log", "Settings"]);
    expect(screen.getByRole("link", { name: /^Members/ })).toHaveAttribute(
      "href",
      "/northwind/settings/members",
    );
    expect(screen.getByRole("link", { name: /^Settings/ })).toHaveAttribute(
      "href",
      "/northwind/settings/general",
    );
    expect(screen.getByRole("link", { name: /^Members/ })).toHaveTextContent(
      "People in Northwind Traders and their roles.",
    );
  });

  it("HomePage_Member_OffersOnlyThePagesTheRoleCanOpen", async () => {
    server.use(...tenantAnswers(BuiltInRole.Member));

    renderInTenant(<HomePage />, { url: "/northwind" });

    await screen.findByRole("heading", { level: 1, name: /^Welcome, / });
    expect(cards()).toEqual(["Members", "Settings"]);
  });
});
