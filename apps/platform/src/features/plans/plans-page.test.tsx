import { ErrorCode } from "@repo/api-client";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { setupServer } from "msw/node";
import {
  afterAll,
  afterEach,
  beforeAll,
  beforeEach,
  describe,
  expect,
  it,
} from "vitest";
import { meAnswer, problem } from "../../../test/organisations";
import { free, plansAnswer, pro } from "../../../test/plans";
import { renderWithProviders } from "../../../test/render";
import { PlansPage } from "./plans-page";

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
beforeEach(() => server.use(meAnswer()));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

describe("PlansPage", () => {
  it("PlansPage_List_ShowsEachPlanWithItsOrganisationsAndTheDefaultTag", async () => {
    server.use(plansAnswer());

    renderWithProviders(<PlansPage />);

    const table = await screen.findByRole("table", { name: "Plans" });
    const freeRow = within(table)
      .getByRole("link", { name: "Free" })
      .closest("tr") as HTMLElement;
    expect(within(freeRow).getByText("Default")).toBeVisible();
    expect(within(freeRow).getByText("1")).toBeVisible();
    const proRow = within(table)
      .getByRole("link", { name: "Pro" })
      .closest("tr") as HTMLElement;
    expect(within(proRow).queryByText("Default")).toBeNull();
    expect(within(proRow).getByText("6")).toBeVisible();
    expect(within(table).getByRole("link", { name: "Pro" })).toHaveAttribute(
      "href",
      `/settings/plans/${pro.id}`,
    );
    expect(within(table).getByRole("link", { name: "Free" })).toHaveAttribute(
      "href",
      `/settings/plans/${free.id}`,
    );
  });

  it("PlansPage_Header_HasTheIntroAndNewPlan", async () => {
    server.use(plansAnswer());

    renderWithProviders(<PlansPage />);

    expect(
      await screen.findByRole("heading", { level: 1, name: "Plans" }),
    ).toBeVisible();
    expect(
      screen.getByText(
        "A plan gives each organisation its features and limits. New organisations get the default plan.",
      ),
    ).toBeVisible();
    expect(screen.getByRole("link", { name: "New plan" })).toHaveAttribute(
      "href",
      "/settings/plans/new",
    );
  });

  it("PlansPage_Loading_ShowsTheSkeleton", async () => {
    server.use(http.get("*/v1/platform/plans", () => new Promise(() => {})));

    renderWithProviders(<PlansPage />);

    expect(
      await screen.findByRole("status", { name: "Loading plans" }),
    ).toBeVisible();
  });

  it("PlansPage_ServerError_ShowsTheErrorAndTriesAgain", async () => {
    let calls = 0;
    server.use(
      http.get("*/v1/platform/plans", () => {
        calls += 1;
        return calls === 1
          ? problem(500, ErrorCode.ServerError)
          : HttpResponse.json({
              items: [],
              page: 1,
              pageSize: 100,
              total: 0,
            });
      }),
    );

    renderWithProviders(<PlansPage />);

    expect(await screen.findByText("Couldn’t load plans")).toBeVisible();
    await userEvent.click(screen.getByRole("button", { name: "Try again" }));
    expect(await screen.findByRole("table", { name: "Plans" })).toBeVisible();
  });

  it("PlansPage_Nav_MarksPlansInSettings", async () => {
    server.use(plansAnswer());

    renderWithProviders(<PlansPage />);

    const nav = await screen.findByRole("navigation", { name: "Settings" });
    expect(within(nav).getByRole("link", { name: "Plans" })).toHaveAttribute(
      "aria-current",
      "page",
    );
  });
});
