import { ErrorCode, UserResponseStatus } from "@repo/api-client";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { setupServer } from "msw/node";
import {
  afterAll,
  afterEach,
  beforeAll,
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
  erased,
  john,
  mehmet,
  mehmetMemberships,
  rory,
  unnamed,
  userAnswer,
  userDetailOf,
} from "../../../test/users";
import { UserPage } from "./user-page";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const memberships = async (name: string) =>
  within(await screen.findByRole("table", { name }));

const membershipRow = async (
  organisation: string,
  table = "Organisations and roles",
) => {
  const link = await (await memberships(table)).findByRole("link", {
    name: organisation,
  });
  return within(link.closest("tr") as HTMLElement);
};

const term = async (name: string) => {
  const dt = await screen.findByText(name, { selector: "dt" });
  return dt.nextElementSibling as HTMLElement;
};

describe("UserPage", () => {
  it("UserPage_Active_ShowsNameEmailStatusAndDetails", async () => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ayse, { memberships: ayseMemberships })),
    );

    renderWithProviders(<UserPage userId={ayse.id} />);

    expect(
      await screen.findByRole("heading", { level: 1, name: "Ayşe Yılmaz" }),
    ).toBeVisible();
    expect(screen.getByText("ayse@northwind.example")).toBeVisible();
    expect(screen.getAllByText("Active")[0]).toBeVisible();
    expect(await term("Organisations")).toHaveTextContent("2");
    expect(await term("Created")).toHaveTextContent("3 Mar 2026");
    expect(screen.queryByText("System admin")).toBeNull();
    expect(screen.queryByText(/Suspended since/)).toBeNull();
  });

  it("UserPage_Active_OffersRenameSuspendDelete", async () => {
    server.use(meAnswer(), userAnswer(userDetailOf(ayse)));

    renderWithProviders(<UserPage userId={ayse.id} />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Actions for Ayşe Yılmaz" }),
    );

    expect(
      (await screen.findAllByRole("menuitem")).map((item) => item.textContent),
    ).toEqual(["Rename", "Suspend", "Delete user"]);
  });

  it("UserPage_SuspendedByAPerson_SaysWhenAndByWhom", async () => {
    server.use(
      meAnswer(),
      userAnswer(
        userDetailOf(john, {
          suspendedAt: "2026-10-16T09:00:00Z",
          suspendedBy: {
            firstName: "Ciara",
            lastName: "Byrne",
            deleted: false,
          },
        }),
      ),
    );

    renderWithProviders(<UserPage userId={john.id} />);

    expect(
      await screen.findByText("Suspended since 16 Oct 2026 by Ciara Byrne."),
    ).toBeVisible();
    expect(
      screen.getByText(
        "They are refused on every request. Their memberships are kept.",
      ),
    ).toBeVisible();
    expect(await term("Suspended by")).toHaveTextContent("Ciara Byrne");
    expect(await term("Suspended")).toHaveTextContent("16 Oct 2026");
  });

  it("UserPage_Suspended_OffersReactivate", async () => {
    server.use(
      meAnswer(),
      userAnswer(
        userDetailOf(john, {
          suspendedAt: "2026-10-16T09:00:00Z",
          suspendedBy: {
            firstName: "Ciara",
            lastName: "Byrne",
            deleted: false,
          },
        }),
      ),
    );

    renderWithProviders(<UserPage userId={john.id} />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Actions for John Byrne" }),
    );

    expect(
      (await screen.findAllByRole("menuitem")).map((item) => item.textContent),
    ).toEqual(["Rename", "Reactivate", "Delete user"]);
  });

  it("UserPage_SuspendedByADeletedAdmin_NamesThemDeletedUser", async () => {
    server.use(
      meAnswer(),
      userAnswer(
        userDetailOf(john, {
          suspendedAt: "2026-10-16T09:00:00Z",
          suspendedBy: { firstName: null, lastName: null, deleted: true },
        }),
      ),
    );

    renderWithProviders(<UserPage userId={john.id} />);

    expect(
      await screen.findByText("Suspended since 16 Oct 2026 by Deleted user."),
    ).toBeVisible();
    expect(await term("Suspended by")).toHaveTextContent("Deleted user");
  });

  it("UserPage_Deleted_ShowsTheDeletedStateWithoutActions", async () => {
    server.use(meAnswer(), userAnswer(userDetailOf(erased)));

    renderWithProviders(<UserPage userId={erased.id} />);

    expect(
      await screen.findByRole("heading", { level: 1, name: "Deleted user" }),
    ).toBeVisible();
    expect(screen.getByText("Deleted")).toBeVisible();
    expect(screen.queryByRole("button", { name: /Actions for/ })).toBeNull();
    expect(screen.getByText("Not a member of any organisation.")).toBeVisible();
  });

  it("UserPage_RootSystemAdmin_BothBadgesLinkToSystemAdmins", async () => {
    server.use(meAnswer(), userAnswer(userDetailOf(rory)));

    renderWithProviders(<UserPage userId={rory.id} />);

    expect(
      await screen.findByRole("link", { name: "System admin" }),
    ).toHaveAttribute("href", "/settings/system-admins");
    expect(screen.getByRole("link", { name: "Root" })).toHaveAttribute(
      "href",
      "/settings/system-admins",
    );
  });

  it("UserPage_Memberships_ShowEachOrganisationSlugRoleAndJoined", async () => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(mehmet, { memberships: mehmetMemberships })),
    );

    renderWithProviders(<UserPage userId={mehmet.id} />);

    const northwindRow = await membershipRow("Northwind Traders");
    expect(northwindRow.getByText("northwind")).toBeVisible();
    expect(northwindRow.getByText("Admin")).toBeVisible();
    expect(northwindRow.getByText("5 Mar 2026")).toBeVisible();
    expect(
      (await membershipRow("Northwind Traders")).getByRole("link", {
        name: "Northwind Traders",
      }),
    ).toHaveAttribute("href", `/organisations/${northwind.id}`);
    const custom = (await membershipRow("Shannon Studio")).getByText(
      "Billing lead",
    );
    expect(custom.tagName).toBe("BDI");
  });

  it("UserPage_BuiltInRole_ShowsItsTranslatedName", async () => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(mehmet, { memberships: mehmetMemberships })),
    );

    renderWithProviders(<UserPage userId={mehmet.id} />, "tr");

    expect(
      (
        await membershipRow("Northwind Traders", "Kuruluşlar ve roller")
      ).getByText("Yönetici"),
    ).toBeVisible();
  });

  it("UserPage_OwnerMembership_DisablesMakeOwnerWithItsReason", async () => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ayse, { memberships: ayseMemberships })),
    );

    renderWithProviders(<UserPage userId={ayse.id} />);
    await userEvent.click(
      (await membershipRow("Northwind Traders")).getByRole("button", {
        name: "Actions for Northwind Traders",
      }),
    );

    const item = await screen.findByRole("menuitem", { name: /Make Owner/ });
    expect(item).toHaveAttribute("aria-disabled", "true");
    expect(item).toHaveTextContent("Already an Owner.");
  });

  it("UserPage_OtherMembership_EnablesMakeOwner", async () => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ayse, { memberships: ayseMemberships })),
    );

    renderWithProviders(<UserPage userId={ayse.id} />);
    await userEvent.click(
      (await membershipRow("Atlas Logistics")).getByRole("button", {
        name: "Actions for Atlas Logistics",
      }),
    );

    expect(
      await screen.findByRole("menuitem", { name: "Make Owner" }),
    ).not.toHaveAttribute("aria-disabled");
  });

  it("UserPage_NotFound_ShowsPageNotFound", async () => {
    server.use(
      meAnswer(),
      http.get(`*/v1/platform/users/${ayse.id}`, () =>
        problem(404, ErrorCode.UserNotFound),
      ),
    );

    renderWithProviders(<UserPage userId={ayse.id} />);

    expect(await screen.findByText("Page not found")).toBeVisible();
  });

  it("UserPage_IdThatIsNoId_ShowsPageNotFoundWithoutARead", async () => {
    const read = vi.fn();
    server.use(
      meAnswer(),
      http.get("*/v1/platform/users/*", () => {
        read();
        return HttpResponse.json({});
      }),
    );

    renderWithProviders(<UserPage userId="not-an-id" />);

    expect(await screen.findByText("Page not found")).toBeVisible();
    expect(read).not.toHaveBeenCalled();
  });

  it("UserPage_StatusDeletedWithNames_StillShowsDeletedUser", async () => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ayse, { status: UserResponseStatus.Deleted })),
    );

    renderWithProviders(<UserPage userId={ayse.id} />);

    expect(
      await screen.findByRole("heading", { level: 1, name: "Deleted user" }),
    ).toBeVisible();
  });

  it("UserPage_ActiveWithoutNames_ShowsTheEmail", async () => {
    server.use(meAnswer(), userAnswer(userDetailOf(unnamed)));

    renderWithProviders(<UserPage userId={unnamed.id} />);

    expect(
      await screen.findByRole("heading", {
        level: 1,
        name: "root@starter.example",
      }),
    ).toBeVisible();
    expect(screen.queryByText("Deleted user")).toBeNull();
  });
});
