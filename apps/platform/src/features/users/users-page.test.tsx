import { ErrorCode, UserResponseStatus } from "@repo/api-client";
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
import { meAnswer, northwind, problem } from "../../../test/organisations";
import { renderWithProviders, sentence } from "../../../test/render";
import {
  ayse,
  ayseMemberships,
  ciara,
  erased,
  john,
  mehmet,
  membershipOf,
  platformCommand,
  rory,
  unnamed,
  userAnswer,
  userDetailOf,
  userResponse,
  usersAnswer,
  usersPageOf,
} from "../../../test/users";
import { UsersPage } from "./users-page";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
beforeEach(() => window.history.replaceState(null, "", "/users"));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const table = async () =>
  within(await screen.findByRole("table", { name: "Users" }));

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

const searchParam = (name: string) =>
  new URLSearchParams(window.location.search).get(name);

describe("UsersPage", () => {
  it("UsersPage_EachUser_ShowsNameStatusEmailAndOrganisations", async () => {
    server.use(meAnswer(), usersAnswer(usersPageOf([ayse, john])));

    renderWithProviders(<UsersPage />);

    const row = await rowOf("Ayşe Yılmaz");
    expect(row.getByText("ayse@northwind.example")).toBeVisible();
    expect(row.getByText("2")).toBeVisible();
    expect(row.getByText("Active")).toHaveAttribute("data-tone", "success");
    expect(row.queryByText("3 Mar 2026")).toBeNull();
    expect((await rowOf("John Byrne")).getByText("Suspended")).toHaveAttribute(
      "data-tone",
      "warning",
    );
    expect(
      (await table())
        .getAllByRole("columnheader")
        .map((header) => header.textContent),
    ).toEqual(["Name", "Status", "Email", "Organisations", "Actions"]);
  });

  it("UsersPage_EachUser_ShowsNoAvatar", async () => {
    server.use(meAnswer(), usersAnswer(usersPageOf([ayse])));

    renderWithProviders(<UsersPage />);

    expect((await rowOf("Ayşe Yılmaz")).queryByText("AY")).toBeNull();
  });

  it("UsersPage_Header_HasTheDescriptionAndNoFreshness", async () => {
    server.use(meAnswer(), usersAnswer(usersPageOf([ayse])));

    renderWithProviders(<UsersPage />);

    expect(
      await screen.findByText("Everyone who can sign in to Starter."),
    ).toBeVisible();
    await rowOf("Ayşe Yılmaz");
    expect(screen.queryByText(/Updated/)).toBeNull();
  });

  it("UsersPage_Tabs_ShowTheStatusCountsWithAllTheirSum", async () => {
    server.use(
      meAnswer(),
      usersAnswer(
        usersPageOf([ayse], {
          statusCounts: { active: 65, suspended: 2, deleted: 1 },
        }),
      ),
    );

    renderWithProviders(<UsersPage />);
    await rowOf("Ayşe Yılmaz");

    expect(screen.getAllByRole("tab").map((tab) => tab.textContent)).toEqual([
      "All 68",
      "Active 65",
      "Suspended 2",
    ]);
  });

  it("UsersPage_SystemAdmin_ShowsTheSystemAdminTagOnly", async () => {
    server.use(meAnswer(), usersAnswer(usersPageOf([ciara])));

    renderWithProviders(<UsersPage />);

    const row = await rowOf("Ciara Byrne");
    expect(row.getByText("System admin")).toBeVisible();
    expect(row.queryByText("Root")).toBeNull();
  });

  it("UsersPage_RootSystemAdmin_ShowsTheSystemAdminLabelOnly", async () => {
    server.use(meAnswer(), usersAnswer(usersPageOf([rory])));

    renderWithProviders(<UsersPage />);

    const row = await rowOf("Rory Brennan");
    expect(row.getByText("System admin")).toBeVisible();
    expect(row.queryByText("Root")).toBeNull();
  });

  it("UsersPage_SystemAdminLabel_IsNoButtonOrLink", async () => {
    server.use(meAnswer(), usersAnswer(usersPageOf([rory])));

    renderWithProviders(<UsersPage />);

    const row = await rowOf("Rory Brennan");
    expect(row.getByText("System admin").closest("a, button")).toBeNull();
  });

  it("UsersPage_DeletedUser_ShowsDeletedUserWithoutMenu", async () => {
    server.use(meAnswer(), usersAnswer(usersPageOf([erased])));

    renderWithProviders(<UsersPage />);

    const row = await rowOf("Deleted user");
    expect(row.getByText("Deleted")).toBeVisible();
    expect(row.queryByRole("button", { name: /Actions for/ })).toBeNull();
  });

  it("UsersPage_ActiveUserWithoutNames_ShowsTheEmail", async () => {
    server.use(meAnswer(), usersAnswer(usersPageOf([unnamed])));

    renderWithProviders(<UsersPage />);

    const row = await rowOf("root@starter.example");
    expect(row.getByText("Active")).toBeVisible();
    expect(row.queryByText("Deleted user")).toBeNull();
  });

  it("UsersPage_Active_OffersRenameSuspendDelete", async () => {
    server.use(meAnswer(), usersAnswer(usersPageOf([ayse])));

    renderWithProviders(<UsersPage />);

    expect(await menuOf("Ayşe Yılmaz")).toEqual([
      "Rename",
      "Suspend",
      "Delete user",
    ]);
  });

  it("UsersPage_Suspended_OffersRenameReactivateDelete", async () => {
    server.use(meAnswer(), usersAnswer(usersPageOf([john])));

    renderWithProviders(<UsersPage />);

    expect(await menuOf("John Byrne")).toEqual([
      "Rename",
      "Reactivate",
      "Delete user",
    ]);
  });

  it("UsersPage_SuspendFromARow_ReadsTheUserForTheLastOwnerWarning", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      usersAnswer(usersPageOf([ayse])),
      userAnswer(userDetailOf(ayse, { memberships: ayseMemberships }), reads),
    );

    renderWithProviders(<UsersPage />);
    await menuOf("Ayşe Yılmaz");
    await userEvent.click(screen.getByRole("menuitem", { name: "Suspend" }));

    const dialog = within(
      await screen.findByRole("alertdialog", { name: "Suspend Ayşe Yılmaz?" }),
    );
    expect(
      await dialog.findByText(
        sentence(
          "Ayşe Yılmaz is the only Owner of Northwind Traders. While they are suspended, nobody there can manage Owners. Make someone else Owner first if Northwind Traders needs one.",
        ),
      ),
    ).toBeVisible();
    expect(reads.count).toBe(1);
  });

  it("UsersPage_DeleteFromARowOfTheLastOwner_SaysWhyAtOnceAndSendsNothing", async () => {
    const calls: { key: string | null; body: unknown }[] = [];
    server.use(
      meAnswer(),
      usersAnswer(usersPageOf([ayse])),
      userAnswer(
        userDetailOf(ayse, {
          memberships: [membershipOf(northwind, { lastOwner: true })],
        }),
      ),
      platformCommand("delete", `users/${ayse.id}`, [], calls),
    );

    renderWithProviders(<UsersPage />);
    await menuOf("Ayşe Yılmaz");
    await userEvent.click(
      screen.getByRole("menuitem", { name: "Delete user" }),
    );

    const dialog = within(
      await screen.findByRole("alertdialog", {
        name: "Ayşe Yılmaz can’t be deleted yet",
      }),
    );
    expect(
      dialog.getByText(
        sentence(
          "Ayşe Yılmaz is the only Owner of Northwind Traders. Make someone else Owner there first.",
        ),
      ),
    ).toBeVisible();
    expect(dialog.queryByRole("textbox")).toBeNull();
    expect(calls).toHaveLength(0);
  });

  it("UsersPage_DeleteFromARowOfASystemAdmin_SaysWhyAtOnce", async () => {
    server.use(
      meAnswer(),
      usersAnswer(usersPageOf([ciara])),
      userAnswer(userDetailOf(ciara)),
    );

    renderWithProviders(<UsersPage />);
    await menuOf("Ciara Byrne");
    await userEvent.click(
      screen.getByRole("menuitem", { name: "Delete user" }),
    );

    const dialog = within(
      await screen.findByRole("alertdialog", {
        name: "Ciara Byrne can’t be deleted",
      }),
    );
    expect(
      dialog.getByText(
        sentence(
          "Ciara Byrne is a system admin. Remove the system admin access in Settings › System admins first.",
        ),
      ),
    ).toBeVisible();
    expect(dialog.getByRole("button", { name: "Close" })).toBeVisible();
  });

  it("UsersPage_RenameFromARow_ReadsTheListAgain", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      meAnswer(),
      usersAnswer(usersPageOf([ayse]), requests),
      platformCommand("patch", `users/${ayse.id}`, [
        userResponse(ayse, { name: "Ayşe Demir" }),
      ]),
    );

    renderWithProviders(<UsersPage />);
    await menuOf("Ayşe Yılmaz");
    await userEvent.click(screen.getByRole("menuitem", { name: "Rename" }));
    const dialog = within(
      await screen.findByRole("alertdialog", { name: "Rename Ayşe Yılmaz" }),
    );
    const name = dialog.getByRole("textbox", { name: "Name" });
    await userEvent.clear(name);
    await userEvent.type(name, "Ayşe Demir");
    await userEvent.click(dialog.getByRole("button", { name: "Rename" }));

    await expectVisibleToast(sentence("Renamed to Ayşe Demir."));
    await waitFor(() => expect(requests).toHaveLength(2));
  });

  it("UsersPage_FirstRead_SendsNoIsSystemAdminAndNoStatus", async () => {
    const requests: URLSearchParams[] = [];
    server.use(meAnswer(), usersAnswer(usersPageOf([ayse]), requests));

    renderWithProviders(<UsersPage />);

    expect(await rowOf("Ayşe Yılmaz")).toBeTruthy();
    expect(requests.map((request) => request.has("isSystemAdmin"))).toEqual([
      false,
    ]);
    expect(requests.map((request) => request.get("status"))).toEqual([null]);
  });

  it.each([
    ["Active", UserResponseStatus.Active],
    ["Suspended", UserResponseStatus.Suspended],
  ])("UsersPage_%sTab_FiltersByItsStatusInTheUrl", async (tab, status) => {
    const requests: URLSearchParams[] = [];
    server.use(meAnswer(), usersAnswer(usersPageOf([ayse]), requests));

    renderWithProviders(<UsersPage />);
    await userEvent.click(
      await screen.findByRole("tab", { name: new RegExp(`^${tab}`) }),
    );

    await waitFor(() => expect(requests.at(-1)?.get("status")).toBe(status));
    await waitFor(() => expect(searchParam("status")).toBe(status));
    expect(requests.some((request) => request.has("isSystemAdmin"))).toBe(
      false,
    );
  });

  it("UsersPage_Search_SendsQAndKeepsItInTheUrl", async () => {
    const requests: URLSearchParams[] = [];
    server.use(meAnswer(), usersAnswer(usersPageOf([ayse]), requests));

    renderWithProviders(<UsersPage />);
    await userEvent.type(
      await screen.findByRole("searchbox", { name: "Search by name or email" }),
      "ayse",
    );

    await waitFor(() => expect(requests.at(-1)?.get("q")).toBe("ayse"));
    await waitFor(() => expect(searchParam("q")).toBe("ayse"));
    expect(requests.map((request) => request.get("q"))).toEqual([null, "ayse"]);
  });

  it("UsersPage_QInTheUrl_IsSentAndShownInTheSearch", async () => {
    window.history.replaceState(
      null,
      "",
      "/users?q=niamh%40liffeydesign.example",
    );
    const requests: URLSearchParams[] = [];
    server.use(meAnswer(), usersAnswer(usersPageOf([ayse]), requests));

    renderWithProviders(<UsersPage />);

    expect(
      await screen.findByRole("searchbox", { name: "Search by name or email" }),
    ).toHaveValue("niamh@liffeydesign.example");
    await waitFor(() =>
      expect(requests.at(-1)?.get("q")).toBe("niamh@liffeydesign.example"),
    );
  });

  it("UsersPage_SortByName_SortsByNameInTheUrl", async () => {
    const requests: URLSearchParams[] = [];
    server.use(meAnswer(), usersAnswer(usersPageOf([ayse]), requests));

    renderWithProviders(<UsersPage />);
    await userEvent.click(
      (await table()).getByRole("button", { name: "Name" }),
    );
    await waitFor(() => expect(requests.at(-1)?.get("sort")).toBe("name"));
    await userEvent.click(
      (await table()).getByRole("button", { name: "Name" }),
    );

    await waitFor(() => expect(requests.at(-1)?.get("sort")).toBe("-name"));
    await waitFor(() => expect(searchParam("sort")).toBe("-name"));
  });

  it("UsersPage_EmailSortInTheUrl_IsSent", async () => {
    window.history.replaceState(null, "", "/users?sort=email");
    const requests: URLSearchParams[] = [];
    server.use(meAnswer(), usersAnswer(usersPageOf([ayse]), requests));

    renderWithProviders(<UsersPage />);

    await waitFor(() => expect(requests.at(-1)?.get("sort")).toBe("email"));
  });

  /** Two pages: Ayşe on the first, Mehmet on the second; the second answers with `second` when it is given. */
  const twoPages = (
    requests: URLSearchParams[] = [],
    second?: () => Response,
  ) =>
    http.get("*/v1/platform/users", ({ request }) => {
      const params = new URL(request.url).searchParams;
      requests.push(params);
      if (params.get("page") === "2" && second) return second();
      return HttpResponse.json(
        params.get("page") === "2"
          ? usersPageOf([mehmet], { page: 2, pageSize: 1, total: 2 })
          : usersPageOf([ayse], { pageSize: 1, total: 2 }),
      );
    });

  it("UsersPage_MoreThanOnePage_LoadMoreAddsTheNextPageWithNoPageInTheUrl", async () => {
    const requests: URLSearchParams[] = [];
    server.use(meAnswer(), twoPages(requests));

    renderWithProviders(<UsersPage />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Load more" }),
    );

    expect(await rowOf("Mehmet Kaya")).toBeTruthy();
    expect(await rowOf("Ayşe Yılmaz")).toBeTruthy();
    expect(screen.getByText("1 more user loaded.")).toHaveAttribute(
      "aria-live",
      "polite",
    );
    expect(requests.map((request) => request.get("page"))).toEqual(["1", "2"]);
    expect(searchParam("page")).toBeNull();
    expect(screen.queryByRole("button", { name: "Next" })).toBeNull();
  });

  it("UsersPage_LoadMoreFails_SaysSoUnderTheRowsAndTriesAgain", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      meAnswer(),
      twoPages(requests, () => problem(500, ErrorCode.ServerError)),
    );

    renderWithProviders(<UsersPage />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Load more" }),
    );

    expect(await screen.findByText("Couldn’t load more users")).toBeVisible();
    expect(await rowOf("Ayşe Yılmaz")).toBeTruthy();
    await userEvent.click(screen.getByRole("button", { name: "Try again" }));
    await waitFor(() =>
      expect(
        requests.filter((request) => request.get("page") === "2"),
      ).toHaveLength(2),
    );
  });

  it("UsersPage_Loading_ShowsTheSkeleton", async () => {
    server.use(
      meAnswer(),
      http.get("*/v1/platform/users", async () => {
        await delay("infinite");
        return HttpResponse.json(usersPageOf([]));
      }),
    );

    renderWithProviders(<UsersPage />);

    expect(
      await screen.findByRole("status", { name: "Loading users" }),
    ).toBeInTheDocument();
  });

  it("UsersPage_EmptySearch_SaysSoAndClears", async () => {
    window.history.replaceState(null, "", "/users?q=pilot");
    server.use(meAnswer(), usersAnswer(usersPageOf([])));

    renderWithProviders(<UsersPage />);
    expect(await screen.findByText("No user matches “pilot”")).toBeVisible();
    expect(
      screen.getByText("Search by name or by the full email address."),
    ).toBeVisible();
    await userEvent.click(screen.getByRole("button", { name: "Clear search" }));

    await waitFor(() => expect(window.location.search).toBe(""));
  });

  it("UsersPage_EmptyTab_SaysSo", async () => {
    window.history.replaceState(null, "", "/users?status=suspended");
    server.use(meAnswer(), usersAnswer(usersPageOf([])));

    renderWithProviders(<UsersPage />);

    expect(await screen.findByText("No suspended users")).toBeVisible();
  });

  it("UsersPage_Error_SaysWhatFailedAndRetries", async () => {
    const calls = vi.fn();
    server.use(
      meAnswer(),
      http.get("*/v1/platform/users", () => {
        calls();
        return problem(500, ErrorCode.ServerError);
      }),
    );

    renderWithProviders(<UsersPage />);
    expect(await screen.findByText("Couldn’t load users")).toBeVisible();
    await userEvent.click(screen.getByRole("button", { name: "Try again" }));

    await waitFor(() => expect(calls).toHaveBeenCalledTimes(2));
  });
});
