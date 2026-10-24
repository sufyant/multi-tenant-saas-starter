import { ErrorCode, UserResponseStatus } from "@repo/api-client";
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
import { renderWithProviders } from "../../../test/render";
import {
  ayse,
  ayseMemberships,
  ciara,
  erased,
  john,
  membershipOf,
  platformCommand,
  rory,
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
  it("UsersPage_EachUser_ShowsNameEmailOrganisationsStatusAndCreated", async () => {
    server.use(meAnswer(), usersAnswer(usersPageOf([ayse, john])));

    renderWithProviders(<UsersPage />);

    const row = await rowOf("Ayşe Yılmaz");
    expect(row.getByText("ayse@northwind.example")).toBeVisible();
    expect(row.getByText("2")).toBeVisible();
    expect(row.getByText("Active")).toBeVisible();
    expect(row.getByText("3 Mar 2026")).toBeVisible();
    expect((await rowOf("John Byrne")).getByText("Suspended")).toBeVisible();
    expect(
      (await table())
        .getAllByRole("columnheader")
        .map((header) => header.textContent),
    ).toEqual(["User", "Organisations", "Status", "Created", "Actions"]);
  });

  it("UsersPage_SystemAdmin_ShowsTheSystemAdminTagOnly", async () => {
    server.use(meAnswer(), usersAnswer(usersPageOf([ciara])));

    renderWithProviders(<UsersPage />);

    const row = await rowOf("Ciara Byrne");
    expect(row.getByText("System admin")).toBeVisible();
    expect(row.queryByText("Root")).toBeNull();
  });

  it("UsersPage_RootSystemAdmin_ShowsTheRootTagBesideIt", async () => {
    server.use(meAnswer(), usersAnswer(usersPageOf([rory])));

    renderWithProviders(<UsersPage />);

    const row = await rowOf("Rory Brennan");
    expect(row.getByText("System admin")).toBeVisible();
    expect(row.getByText("Root")).toBeVisible();
  });

  it("UsersPage_Tags_AreNoButtonsOrLinks", async () => {
    server.use(meAnswer(), usersAnswer(usersPageOf([rory])));

    renderWithProviders(<UsersPage />);

    const row = await rowOf("Rory Brennan");
    expect(row.getByText("System admin").closest("a, button")).toBeNull();
    expect(row.getByText("Root").closest("a, button")).toBeNull();
  });

  it("UsersPage_DeletedUser_ShowsDeletedUserWithoutMenu", async () => {
    server.use(meAnswer(), usersAnswer(usersPageOf([erased])));

    renderWithProviders(<UsersPage />);

    const row = await rowOf("Deleted user");
    expect(row.getByText("Deleted")).toBeVisible();
    expect(row.queryByRole("button", { name: /Actions for/ })).toBeNull();
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
        "Ayşe Yılmaz is the last Owner of Northwind Traders.",
      ),
    ).toBeVisible();
    expect(reads.count).toBe(1);
  });

  it("UsersPage_DeleteFromARow_NamesTheLastOwnersOrganisationsFromGetUser", async () => {
    server.use(
      meAnswer(),
      usersAnswer(usersPageOf([ayse])),
      userAnswer(
        userDetailOf(ayse, {
          memberships: [membershipOf(northwind, { lastOwner: true })],
        }),
      ),
      platformCommand("delete", `users/${ayse.id}`, [
        () => problem(409, ErrorCode.UserLastOwner, undefined, [northwind.id]),
      ]),
    );

    renderWithProviders(<UsersPage />);
    await menuOf("Ayşe Yılmaz");
    await userEvent.click(
      screen.getByRole("menuitem", { name: "Delete user" }),
    );
    const dialog = within(
      await screen.findByRole("alertdialog", { name: "Delete Ayşe Yılmaz?" }),
    );
    await userEvent.type(
      dialog.getByRole("textbox", {
        name: "Type ayse@northwind.example to confirm",
      }),
      "ayse@northwind.example",
    );
    await userEvent.click(dialog.getByRole("button", { name: "Delete user" }));

    expect(
      await screen.findByRole("link", { name: /Northwind Traders/ }),
    ).toHaveAttribute("href", `/organisations/${northwind.id}`);
  });

  it("UsersPage_RenameFromARow_ReadsTheListAgain", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      meAnswer(),
      usersAnswer(usersPageOf([ayse]), requests),
      platformCommand("patch", `users/${ayse.id}`, [
        userResponse(ayse, { lastName: "Demir" }),
      ]),
    );

    renderWithProviders(<UsersPage />);
    await menuOf("Ayşe Yılmaz");
    await userEvent.click(screen.getByRole("menuitem", { name: "Rename" }));
    const dialog = within(
      await screen.findByRole("alertdialog", { name: "Rename Ayşe Yılmaz" }),
    );
    const lastName = dialog.getByRole("textbox", { name: "Last name" });
    await userEvent.clear(lastName);
    await userEvent.type(lastName, "Demir");
    await userEvent.click(dialog.getByRole("button", { name: "Rename user" }));

    expect(await screen.findByText("Renamed to Ayşe Demir.")).toBeVisible();
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

  it("UsersPage_Tabs_AreAllActiveSuspended", async () => {
    server.use(meAnswer(), usersAnswer());

    renderWithProviders(<UsersPage />);

    expect(
      (await screen.findAllByRole("tab")).map((tab) => tab.textContent),
    ).toEqual(["All", "Active", "Suspended"]);
  });

  it.each([
    ["Active", UserResponseStatus.Active],
    ["Suspended", UserResponseStatus.Suspended],
  ])("UsersPage_%sTab_FiltersByItsStatusInTheUrl", async (tab, status) => {
    const requests: URLSearchParams[] = [];
    server.use(meAnswer(), usersAnswer(usersPageOf([ayse]), requests));

    renderWithProviders(<UsersPage />);
    await userEvent.click(await screen.findByRole("tab", { name: tab }));

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

  it("UsersPage_SortByUser_SortsByLastNameInTheUrl", async () => {
    const requests: URLSearchParams[] = [];
    server.use(meAnswer(), usersAnswer(usersPageOf([ayse]), requests));

    renderWithProviders(<UsersPage />);
    await userEvent.click(
      (await table()).getByRole("button", { name: "User" }),
    );
    await waitFor(() => expect(requests.at(-1)?.get("sort")).toBe("lastName"));
    await userEvent.click(
      (await table()).getByRole("button", { name: "User" }),
    );

    await waitFor(() => expect(requests.at(-1)?.get("sort")).toBe("-lastName"));
    await waitFor(() => expect(searchParam("sort")).toBe("-lastName"));
  });

  it("UsersPage_EmailSortInTheUrl_IsSent", async () => {
    window.history.replaceState(null, "", "/users?sort=email");
    const requests: URLSearchParams[] = [];
    server.use(meAnswer(), usersAnswer(usersPageOf([ayse]), requests));

    renderWithProviders(<UsersPage />);

    await waitFor(() => expect(requests.at(-1)?.get("sort")).toBe("email"));
  });

  it("UsersPage_NextPage_PutsThePageInTheUrl", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      meAnswer(),
      usersAnswer(usersPageOf([ayse], { total: 41 }), requests),
    );

    renderWithProviders(<UsersPage />);
    await userEvent.click(
      (
        await screen.findAllByRole("button", { name: "Next" })
      )[0] as HTMLElement,
    );

    await waitFor(() => expect(requests.at(-1)?.get("page")).toBe("2"));
    await waitFor(() => expect(searchParam("page")).toBe("2"));
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
    expect(await screen.findByText("No users match “pilot”")).toBeVisible();
    expect(screen.getByText("Search by name or email.")).toBeVisible();
    await userEvent.click(
      screen.getByRole("button", { name: "Clear filters" }),
    );

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
