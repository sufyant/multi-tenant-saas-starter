import { ErrorCode, TenantResponseStatus } from "@repo/api-client";
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
  burren,
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
import { renderWithProviders } from "../../../test/render";
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

  it("OrganisationsPage_Closed_CountsTheDaysFromReopenableUntil", async () => {
    server.use(meAnswer(), listAnswer(pageOf([kestrel])));

    renderWithProviders(<OrganisationsPage />);

    expect(
      (await rowOf("Kestrel Labs")).getByText("Deleted in 3 days"),
    ).toBeVisible();
  });

  it.each([
    ["Moyne Gallery", moyne],
    ["Corrib Press", corrib],
  ])(
    "OrganisationsPage_%s_HasNoMenuAndNoCounts",
    async (name, organisation) => {
      server.use(meAnswer(), listAnswer(pageOf([organisation])));

      renderWithProviders(<OrganisationsPage />);

      const row = await rowOf(name);
      expect(
        row.queryByRole("button", { name: `Actions for ${name}` }),
      ).toBeNull();
      expect(row.getAllByText("—")).toHaveLength(2);
    },
  );

  it("OrganisationsPage_OwnerColumn_CountsTheOwners", async () => {
    server.use(meAnswer(), listAnswer(pageOf([northwind, shannon])));

    renderWithProviders(<OrganisationsPage />);

    expect(
      (await rowOf("Northwind Traders")).getByText("1 Owner"),
    ).toBeVisible();
    expect((await rowOf("Shannon Studio")).getByText("3 Owners")).toBeVisible();
  });

  it("OrganisationsPage_NoOwnerAndAPendingInvitation_ShowsItsEmailAndExpiry", async () => {
    server.use(meAnswer(), listAnswer(pageOf([liffey])));

    renderWithProviders(<OrganisationsPage />);

    const row = await rowOf("Liffey Design");
    expect(row.getByText("Waiting for Owner")).toBeVisible();
    expect(row.getByText("niamh@liffeydesign.example")).toBeVisible();
    expect(row.getByText("in 7 days")).toBeVisible();
  });

  it("OrganisationsPage_NoOwnerAndAnExpiredInvitation_SaysItExpired", async () => {
    server.use(meAnswer(), listAnswer(pageOf([burren])));

    renderWithProviders(<OrganisationsPage />);

    const row = await rowOf("Burren Supply");
    expect(row.getByText("Waiting for Owner")).toBeVisible();
    expect(row.getByText("Invitation expired")).toBeVisible();
    expect(row.queryByText("orla@burren.example")).toBeNull();
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

  it("OrganisationsPage_AllTab_ListsBeingDeletedWithoutAStatusFilter", async () => {
    const requests: URLSearchParams[] = [];
    server.use(meAnswer(), listAnswer(pageOf([kestrel, moyne]), requests));

    renderWithProviders(<OrganisationsPage />);

    expect(await rowOf("Moyne Gallery")).toBeTruthy();
    expect(screen.getByRole("tab", { name: "All" })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    expect(requests.map((request) => request.get("status"))).toEqual([null]);
  });

  it.each([
    ["Active", TenantResponseStatus.Active],
    ["Suspended", TenantResponseStatus.Suspended],
    ["Closed", TenantResponseStatus.Closed],
    ["Deleted", TenantResponseStatus.Purged],
  ])(
    "OrganisationsPage_%sTab_FiltersByItsStatusInTheUrl",
    async (tab, status) => {
      const requests: URLSearchParams[] = [];
      server.use(meAnswer(), listAnswer(pageOf([northwind]), requests));

      renderWithProviders(<OrganisationsPage />);
      await userEvent.click(await screen.findByRole("tab", { name: tab }));

      await waitFor(() => expect(requests.at(-1)?.get("status")).toBe(status));
      await waitFor(() =>
        expect(new URLSearchParams(window.location.search).get("status")).toBe(
          status,
        ),
      );
    },
  );

  it("OrganisationsPage_NoTabForBeingDeleted", async () => {
    server.use(meAnswer(), listAnswer());

    renderWithProviders(<OrganisationsPage />);

    expect(
      (await screen.findAllByRole("tab")).map((tab) => tab.textContent),
    ).toEqual(["All", "Active", "Suspended", "Closed", "Deleted"]);
  });

  it("OrganisationsPage_Search_SendsQAndKeepsItInTheUrl", async () => {
    const requests: URLSearchParams[] = [];
    server.use(meAnswer(), listAnswer(pageOf([northwind]), requests));

    renderWithProviders(<OrganisationsPage />);
    await userEvent.type(
      await screen.findByRole("searchbox", { name: "Search by name or slug" }),
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
    const sortButton = (await table()).getByRole("button", {
      name: "Organisation",
    });
    await userEvent.click(sortButton);
    await waitFor(() => expect(requests.at(-1)?.get("sort")).toBe("name"));
    await userEvent.click(
      (await table()).getByRole("button", { name: "Organisation" }),
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

  it("OrganisationsPage_NextPage_PutsThePageInTheUrl", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      meAnswer(),
      listAnswer(pageOf([northwind], { total: 41 }), requests),
    );

    renderWithProviders(<OrganisationsPage />);
    await userEvent.click(
      (
        await screen.findAllByRole("button", { name: "Next" })
      )[0] as HTMLElement,
    );

    await waitFor(() => expect(requests.at(-1)?.get("page")).toBe("2"));
    await waitFor(() =>
      expect(new URLSearchParams(window.location.search).get("page")).toBe("2"),
    );
  });

  it("OrganisationsPage_BackAfterATab_RestoresTheList", async () => {
    const requests: URLSearchParams[] = [];
    server.use(meAnswer(), listAnswer(pageOf([northwind]), requests));

    renderWithProviders(<OrganisationsPage />);
    await userEvent.click(await screen.findByRole("tab", { name: "Closed" }));
    // nuqs writes the address after its throttle; back() goes to the entry before it only once it is there.
    await waitFor(() => expect(window.location.search).toBe("?status=closed"));
    window.history.back();

    await waitFor(() =>
      expect(screen.getByRole("tab", { name: "All" })).toHaveAttribute(
        "aria-selected",
        "true",
      ),
    );
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

  it("OrganisationsPage_FirstRun_OffersCreateOrganisation", async () => {
    server.use(meAnswer(), listAnswer(pageOf([])));

    renderWithProviders(<OrganisationsPage />);

    expect(await screen.findByText("No organisations yet")).toBeVisible();
    expect(
      screen.getByRole("button", { name: "Create organisation" }),
    ).toBeVisible();
    expect(screen.queryByRole("tab")).toBeNull();
  });

  it("OrganisationsPage_FilteredEmpty_SaysSoAndClears", async () => {
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
