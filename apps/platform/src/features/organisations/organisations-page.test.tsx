import { ErrorCode, TenantResponseStatus } from "@repo/api-client";
import { expectVisibleToast } from "@repo/ui/test/toast";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { delay, HttpResponse, http } from "msw";
import { setupServer } from "msw/node";
import {
  afterAll,
  afterEach,
  beforeAll,
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from "vitest";
import {
  atlas,
  corrib,
  kestrel,
  liffey,
  listAnswer,
  meAnswer,
  moyne,
  northwind,
  pageOf,
  problem,
  shannon,
} from "../../../test/organisations";
import { renderWithProviders, sentence } from "../../../test/render";
import { OrganisationsPage } from "./organisations-page";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
beforeEach(() => window.history.replaceState(null, "", "/"));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const table = async () =>
  within(await screen.findByRole("table", { name: "Organisations" }));

const rowOf = async (name: string) => {
  const cell = await (await table()).findByRole("link", { name });
  return within(cell.closest("tr") as HTMLElement);
};

const menuOf = async (name: string) => {
  await userEvent.click(
    (await rowOf(name)).getByRole("button", { name: `Actions for ${name}` }),
  );
  return (await screen.findAllByRole("menuitem")).map(
    (item) => item.textContent,
  );
};

const counts = { active: 7, suspended: 1, closed: 1, purging: 1, purged: 1 };

describe("OrganisationsPage", () => {
  it("OrganisationsPage_EachStatus_ShowsItsWord", async () => {
    server.use(meAnswer(), listAnswer());

    renderWithProviders(<OrganisationsPage />);

    expect(
      (await rowOf("Northwind Traders")).getByText("Active"),
    ).toBeVisible();
    expect(
      (await rowOf("Atlas Logistics")).getByText("Suspended"),
    ).toBeVisible();
    expect((await rowOf("Kestrel Labs")).getByText("Closed")).toBeVisible();
    expect(
      (await rowOf("Moyne Gallery")).getByText("Being deleted"),
    ).toBeVisible();
    expect((await rowOf("Corrib Press")).getByText("Deleted")).toBeVisible();
  });

  it("OrganisationsPage_Table_HasTheFramesColumns", async () => {
    server.use(meAnswer(), listAnswer(pageOf([northwind])));

    renderWithProviders(<OrganisationsPage />);

    expect(
      (await table())
        .getAllByRole("columnheader")
        .map((column) => column.textContent)
        .slice(0, 4),
    ).toEqual(["Name", "Status", "Address", "Members"]);
  });

  it("OrganisationsPage_Row_ShowsTheAddressAndTheMembersWithTheInvited", async () => {
    server.use(
      meAnswer(),
      listAnswer(
        pageOf([
          { ...northwind, memberCount: 10, pendingInvitationCount: 2 },
          { ...liffey, pendingInvitationCount: 1 },
          { ...shannon, memberCount: 24, pendingInvitationCount: 0 },
        ]),
      ),
    );

    renderWithProviders(<OrganisationsPage />);

    const row = await rowOf("Northwind Traders");
    expect(row.getByText("northwind")).toBeVisible();
    expect(row.getByText("10 members · 2 invited")).toBeVisible();
    expect(
      (await rowOf("Liffey Design")).getByText("No members yet · 1 invited"),
    ).toBeVisible();
    expect(
      (await rowOf("Shannon Studio")).getByText("24 members"),
    ).toBeVisible();
  });

  it("OrganisationsPage_PhoneCard_ShowsThePlanAndTheMembers", async () => {
    server.use(meAnswer(), listAnswer(pageOf([northwind, liffey])));

    renderWithProviders(<OrganisationsPage />);
    await table();

    const cards = within(
      screen.getByRole("list", { name: "Organisations" }),
    ).getAllByRole("listitem");
    expect(cards[0]).toHaveTextContent("Team · 7 members");
    expect(cards[1]).toHaveTextContent("Team · waiting for Owner");
  });

  it.each([
    ["Moyne Gallery", moyne],
    ["Corrib Press", corrib],
  ])("OrganisationsPage_%s_HasNoMenuAndNoCount", async (name, organisation) => {
    server.use(meAnswer(), listAnswer(pageOf([organisation])));

    renderWithProviders(<OrganisationsPage />);

    const row = await rowOf(name);
    expect(
      row.queryByRole("button", { name: `Actions for ${name}` }),
    ).toBeNull();
    expect(row.getByText("—")).toBeVisible();
  });

  it("OrganisationsPage_ActiveWithAnOwner_OffersRenameSuspendClose", async () => {
    server.use(meAnswer(), listAnswer(pageOf([northwind])));

    renderWithProviders(<OrganisationsPage />);

    expect(await menuOf("Northwind Traders")).toEqual([
      "Rename",
      "Suspend",
      "Close organisation",
    ]);
  });

  it("OrganisationsPage_ActiveWithoutAnOwner_OffersInviteOwnerFirst", async () => {
    server.use(meAnswer(), listAnswer(pageOf([liffey])));

    renderWithProviders(<OrganisationsPage />);

    expect(await menuOf("Liffey Design")).toEqual([
      "Invite Owner",
      "Rename",
      "Suspend",
      "Close organisation",
    ]);
  });

  it("OrganisationsPage_Suspended_OffersRenameReactivateClose", async () => {
    server.use(meAnswer(), listAnswer(pageOf([atlas])));

    renderWithProviders(<OrganisationsPage />);

    expect(await menuOf("Atlas Logistics")).toEqual([
      "Rename",
      "Reactivate",
      "Close organisation",
    ]);
  });

  it("OrganisationsPage_Closed_OffersReopen", async () => {
    server.use(meAnswer(), listAnswer(pageOf([kestrel])));

    renderWithProviders(<OrganisationsPage />);

    expect(await menuOf("Kestrel Labs")).toEqual(["Reopen"]);
  });

  it("OrganisationsPage_Tabs_CountFromTheStatusCounts", async () => {
    server.use(
      meAnswer(),
      listAnswer(pageOf([northwind], { statusCounts: counts })),
    );

    renderWithProviders(<OrganisationsPage />);

    await waitFor(() =>
      expect(screen.getAllByRole("tab").map((tab) => tab.textContent)).toEqual([
        "All 10",
        "Active 7",
        "Suspended 1",
        "Closed 2",
        "Deleted 1",
      ]),
    );
  });

  it("OrganisationsPage_AllTab_AsksEveryStatusButDeleted", async () => {
    const requests: URLSearchParams[] = [];
    server.use(meAnswer(), listAnswer(pageOf([kestrel, moyne]), requests));

    renderWithProviders(<OrganisationsPage />);

    expect(await rowOf("Moyne Gallery")).toBeTruthy();
    expect(screen.getByRole("tab", { name: /^All/ })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    expect(requests[0]?.getAll("status")).toEqual([
      TenantResponseStatus.Active,
      TenantResponseStatus.Suspended,
      TenantResponseStatus.Closed,
      TenantResponseStatus.Purging,
    ]);
  });

  it.each([
    ["Active", "active", [TenantResponseStatus.Active]],
    ["Suspended", "suspended", [TenantResponseStatus.Suspended]],
    [
      "Closed",
      "closed",
      [TenantResponseStatus.Closed, TenantResponseStatus.Purging],
    ],
    ["Deleted", "purged", [TenantResponseStatus.Purged]],
  ])(
    "OrganisationsPage_%sTab_FiltersByItsStatusesInTheUrl",
    async (tab, inUrl, statuses) => {
      const requests: URLSearchParams[] = [];
      server.use(meAnswer(), listAnswer(pageOf([northwind]), requests));

      renderWithProviders(<OrganisationsPage />);
      await userEvent.click(
        await screen.findByRole("tab", { name: new RegExp(`^${tab}`) }),
      );

      await waitFor(() =>
        expect(requests.at(-1)?.getAll("status")).toEqual(statuses),
      );
      await waitFor(() =>
        expect(new URLSearchParams(window.location.search).get("status")).toBe(
          inUrl,
        ),
      );
    },
  );

  it("OrganisationsPage_StatusChoice_FiltersAsTheTabsDo", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      meAnswer(),
      listAnswer(pageOf([northwind], { statusCounts: counts }), requests),
    );

    renderWithProviders(<OrganisationsPage />);
    await userEvent.click(
      await screen.findByRole("combobox", { name: "Filter by status" }),
    );
    await userEvent.click(
      await screen.findByRole("option", { name: "Suspended · 1" }),
    );

    await waitFor(() =>
      expect(requests.at(-1)?.getAll("status")).toEqual([
        TenantResponseStatus.Suspended,
      ]),
    );
  });

  it("OrganisationsPage_Search_SendsQAndKeepsItInTheUrl", async () => {
    const requests: URLSearchParams[] = [];
    server.use(meAnswer(), listAnswer(pageOf([northwind]), requests));

    renderWithProviders(<OrganisationsPage />);
    await userEvent.type(
      await screen.findByRole("searchbox", {
        name: "Search by name or address",
      }),
      "north",
    );

    await waitFor(() => expect(requests.at(-1)?.get("q")).toBe("north"));
    await waitFor(() =>
      expect(new URLSearchParams(window.location.search).get("q")).toBe(
        "north",
      ),
    );
    expect(requests.map((request) => request.get("q"))).toEqual([
      null,
      "north",
    ]);
  });

  it("OrganisationsPage_SortByName_PutsTheSortInTheUrl", async () => {
    const requests: URLSearchParams[] = [];
    server.use(meAnswer(), listAnswer(pageOf([northwind]), requests));

    renderWithProviders(<OrganisationsPage />);
    await userEvent.click(
      (await table()).getByRole("button", { name: "Name" }),
    );
    await waitFor(() => expect(requests.at(-1)?.get("sort")).toBe("name"));
    await userEvent.click(
      (await table()).getByRole("button", { name: "Name" }),
    );

    await waitFor(() => expect(requests.at(-1)?.get("sort")).toBe("-name"));
    await waitFor(() =>
      expect(new URLSearchParams(window.location.search).get("sort")).toBe(
        "-name",
      ),
    );
  });

  it("OrganisationsPage_SlugSortInTheUrl_IsSent", async () => {
    window.history.replaceState(null, "", "/?sort=slug");
    const requests: URLSearchParams[] = [];
    server.use(meAnswer(), listAnswer(pageOf([northwind]), requests));

    renderWithProviders(<OrganisationsPage />);

    await waitFor(() => expect(requests.at(-1)?.get("sort")).toBe("slug"));
  });

  it("OrganisationsPage_MoreThanOnePage_LoadMoreAddsTheNextPageAndKeepsNoPageInTheUrl", async () => {
    const pages: (string | null)[] = [];
    server.use(
      meAnswer(),
      http.get("*/v1/platform/tenants", ({ request }) => {
        const page = new URL(request.url).searchParams.get("page");
        pages.push(page);
        return HttpResponse.json(
          page === "2"
            ? pageOf([atlas], { page: 2, total: 2, pageSize: 1 })
            : pageOf([northwind], { total: 2, pageSize: 1 }),
        );
      }),
    );

    renderWithProviders(<OrganisationsPage />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Load more" }),
    );

    expect(await rowOf("Atlas Logistics")).toBeTruthy();
    expect(await rowOf("Northwind Traders")).toBeTruthy();
    expect(screen.getByText("1 more organisation loaded.")).toHaveAttribute(
      "aria-live",
      "polite",
    );
    expect(screen.queryByRole("button", { name: "Load more" })).toBeNull();
    expect(pages).toEqual(["1", "2"]);
    expect(new URLSearchParams(window.location.search).get("page")).toBeNull();
  });

  it("OrganisationsPage_LoadMoreFails_SaysSoAndKeepsTheRows", async () => {
    server.use(
      meAnswer(),
      http.get("*/v1/platform/tenants", ({ request }) =>
        new URL(request.url).searchParams.get("page") === "2"
          ? problem(500, ErrorCode.ServerError)
          : HttpResponse.json(pageOf([northwind], { total: 2, pageSize: 1 })),
      ),
    );

    renderWithProviders(<OrganisationsPage />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Load more" }),
    );

    await expectVisibleToast("Couldn’t load more organisations");
    expect(await rowOf("Northwind Traders")).toBeTruthy();
  });

  it("OrganisationsPage_BackAfterATab_RestoresTheList", async () => {
    const requests: URLSearchParams[] = [];
    server.use(meAnswer(), listAnswer(pageOf([northwind]), requests));

    renderWithProviders(<OrganisationsPage />);
    await userEvent.click(await screen.findByRole("tab", { name: /^Closed/ }));
    // nuqs writes the address after its throttle; back() goes to the entry before it only once it is there.
    await waitFor(() => expect(window.location.search).toBe("?status=closed"));
    window.history.back();

    await waitFor(() =>
      expect(screen.getByRole("tab", { name: /^All/ })).toHaveAttribute(
        "aria-selected",
        "true",
      ),
    );
  });

  it("OrganisationsPage_RenameFromARow_ReadsTheListAgain", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      meAnswer(),
      listAnswer(pageOf([northwind]), requests),
      http.patch(`*/v1/platform/tenants/${northwind.id}`, () =>
        HttpResponse.json({
          id: northwind.id,
          name: "Northwind Traders Ltd",
          slug: northwind.slug,
          status: TenantResponseStatus.Active,
          suspensionReason: null,
          closedAt: null,
          purgedAt: null,
        }),
      ),
    );

    renderWithProviders(<OrganisationsPage />);
    await menuOf("Northwind Traders");
    await userEvent.click(screen.getByRole("menuitem", { name: "Rename" }));
    const dialog = within(
      await screen.findByRole("alertdialog", {
        name: "Rename Northwind Traders",
      }),
    );
    await userEvent.type(dialog.getByRole("textbox", { name: "Name" }), " Ltd");
    await userEvent.click(dialog.getByRole("button", { name: "Rename" }));

    await expectVisibleToast(sentence("Renamed to Northwind Traders Ltd."));
    await waitFor(() => expect(requests).toHaveLength(2));
  });

  it("OrganisationsPage_Loading_ShowsTheSkeleton", async () => {
    server.use(
      meAnswer(),
      http.get("*/v1/platform/tenants", async () => {
        await delay("infinite");
        return HttpResponse.json(pageOf([]));
      }),
    );

    renderWithProviders(<OrganisationsPage />);

    expect(
      await screen.findByRole("status", { name: "Loading organisations" }),
    ).toBeInTheDocument();
  });

  it("OrganisationsPage_Header_HasTheIntroAndNoFreshness", async () => {
    server.use(meAnswer(), listAnswer(pageOf([northwind])));

    renderWithProviders(<OrganisationsPage />);
    await table();

    expect(screen.getByText("Every organisation on Starter.")).toBeVisible();
    expect(screen.queryByText(/^Updated/)).toBeNull();
  });

  it("OrganisationsPage_FirstRun_OffersCreateOrganisationTwice", async () => {
    server.use(meAnswer(), listAnswer(pageOf([])));

    renderWithProviders(<OrganisationsPage />);

    expect(await screen.findByText("No organisations yet")).toBeVisible();
    expect(
      screen.getByText(
        "Create the first one. Its Owner gets an invitation by email.",
      ),
    ).toBeVisible();
    expect(
      screen.getAllByRole("button", { name: "Create organisation" }),
    ).toHaveLength(2);
    expect(screen.queryByRole("tab")).toBeNull();
  });

  it("OrganisationsPage_NoMatchForTheSearch_SaysSoAndClearsTheSearch", async () => {
    window.history.replaceState(null, "", "/?q=harbor");
    server.use(meAnswer(), listAnswer(pageOf([])));

    renderWithProviders(<OrganisationsPage />);
    expect(
      await screen.findByText("No organisation matches “harbor”"),
    ).toBeVisible();
    expect(
      screen.getByText("Check the spelling, or search by address."),
    ).toBeVisible();
    await userEvent.click(screen.getByRole("button", { name: "Clear search" }));

    await waitFor(() => expect(window.location.search).toBe(""));
  });

  it("OrganisationsPage_EmptyTab_SaysSoAndClears", async () => {
    window.history.replaceState(null, "", "/?status=closed");
    server.use(meAnswer(), listAnswer(pageOf([])));

    renderWithProviders(<OrganisationsPage />);
    expect(await screen.findByText("No closed organisations")).toBeVisible();
    await userEvent.click(
      screen.getByRole("button", { name: "Clear filters" }),
    );

    await waitFor(() => expect(window.location.search).toBe(""));
  });

  it("OrganisationsPage_Error_SaysWhatFailedAndRetries", async () => {
    const calls = vi.fn();
    server.use(
      meAnswer(),
      http.get("*/v1/platform/tenants", () => {
        calls();
        return problem(500, ErrorCode.ServerError);
      }),
    );

    renderWithProviders(<OrganisationsPage />);
    expect(
      await screen.findByText("Couldn’t load organisations"),
    ).toBeVisible();
    await userEvent.click(screen.getByRole("button", { name: "Try again" }));

    await waitFor(() => expect(calls).toHaveBeenCalledTimes(2));
  });
});
