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
import { free, itemOf, partner, plansAnswer, pro } from "../../../test/plans";
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
      `/plans/${pro.id}`,
    );
    expect(within(table).getByRole("link", { name: "Free" })).toHaveAttribute(
      "href",
      `/plans/${free.id}`,
    );
  });

  it("PlansPage_List_ShowsWhenEachPlanWasCreated", async () => {
    server.use(plansAnswer());

    renderWithProviders(<PlansPage />);

    const table = await screen.findByRole("table", { name: "Plans" });
    expect(
      within(table).getByRole("columnheader", { name: "Created" }),
    ).toBeVisible();
    const proRow = within(table)
      .getByRole("link", { name: "Pro" })
      .closest("tr") as HTMLElement;
    expect(within(proRow).getByText(/^1 Sept? 2026$/)).toHaveAttribute(
      "datetime",
      "2026-09-01T09:00:00.000Z",
    );
  });

  // 6.17: a phone card says how many organisations are on the plan.
  it("PlansPage_PhoneCard_SaysHowManyOrganisations", async () => {
    server.use(plansAnswer());

    renderWithProviders(<PlansPage />);

    const cards = within(await screen.findByRole("list", { name: "Plans" }));
    expect(cards.getByText("6 organisations")).toBeInTheDocument();
    expect(cards.getByText("One organisation")).toBeInTheDocument();
  });

  it("PlansPage_MoreThanOnePage_LoadMoreAddsTheNextPageAndSaysHowMany", async () => {
    const pages: (string | null)[] = [];
    server.use(
      http.get("*/v1/platform/plans", ({ request }) => {
        const page = new URL(request.url).searchParams.get("page");
        pages.push(page);
        return HttpResponse.json(
          page === "2"
            ? { items: [itemOf(partner)], page: 2, pageSize: 2, total: 3 }
            : {
                items: [free, pro].map(itemOf),
                page: 1,
                pageSize: 2,
                total: 3,
              },
        );
      }),
    );
    renderWithProviders(<PlansPage />);

    await userEvent.click(
      await screen.findByRole("button", { name: "Load more" }),
    );

    const table = await screen.findByRole("table", { name: "Plans" });
    expect(
      await within(table).findByRole("link", { name: "Partner" }),
    ).toBeVisible();
    expect(within(table).getByRole("link", { name: "Free" })).toBeVisible();
    expect(screen.getByText("1 more plan loaded.")).toHaveAttribute(
      "aria-live",
      "polite",
    );
    expect(
      screen.queryByRole("button", { name: "Load more" }),
    ).not.toBeInTheDocument();
    expect(pages).toEqual(["1", "2"]);
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
      "/plans/new",
    );
    await screen.findByRole("table", { name: "Plans" });
    expect(screen.queryByText("Updated", { exact: false })).toBeNull();
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
});
