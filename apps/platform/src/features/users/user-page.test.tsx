import {
  ErrorCode,
  type UserDetailResponse,
  UserResponseStatus,
} from "@repo/api-client";
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

const memberships = async (name = "Organisations") =>
  within(await screen.findByRole("list", { name }));

const membershipRow = async (organisation: string, list?: string) => {
  const link = await (await memberships(list)).findByRole("link", {
    name: organisation,
  });
  return within(link.closest("li") as HTMLElement);
};

const term = async (name: string) => {
  const dt = await screen.findByText(name, { selector: "dt" });
  return dt.nextElementSibling as HTMLElement;
};

const suspendedJohn = (values: Partial<UserDetailResponse> = {}) =>
  userDetailOf(john, {
    suspendedAt: "2026-10-25T09:00:00Z",
    suspendedBy: { name: "Ciara Byrne", deleted: false },
    suspensionReason: "Shared account",
    ...values,
  });

describe("UserPage", () => {
  it("UserPage_Active_ShowsTheHeaderAndTheDetails", async () => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ayse, { memberships: ayseMemberships })),
    );

    renderWithProviders(<UserPage userId={ayse.id} />);

    expect(
      await screen.findByRole("heading", { level: 1, name: "Ayşe Yılmaz" }),
    ).toBeVisible();
    expect(screen.getByRole("link", { name: "Users" })).toHaveAttribute(
      "href",
      "/users",
    );
    expect(await term("Email")).toHaveTextContent("ayse@northwind.example");
    expect(
      within(await term("Email")).getByRole("button", {
        name: "Copy the email",
      }),
    ).toBeVisible();
    expect(within(await term("Status")).getByText("Active")).toHaveAttribute(
      "data-tone",
      "success",
    );
    expect(await term("Signed up")).toHaveTextContent("3 Mar 2026");
    expect(await term("Organisations")).toHaveTextContent("2");
    expect(screen.queryByText("System admin")).toBeNull();
    expect(screen.queryByText(/Suspended on/)).toBeNull();
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

  it("UserPage_Suspended_SaysWhenByWhomAndWhyWithReactivate", async () => {
    server.use(meAnswer(), userAnswer(suspendedJohn()));

    renderWithProviders(<UserPage userId={john.id} />);

    expect(
      await screen.findByText("Suspended on 25 Oct 2026 by Ciara Byrne"),
    ).toBeVisible();
    expect(
      screen.getByText("Reason: Shared account. They can’t sign in."),
    ).toBeVisible();
    await userEvent.click(screen.getByRole("button", { name: "Reactivate" }));
    expect(
      await screen.findByRole("alertdialog", {
        name: "Reactivate John Byrne?",
      }),
    ).toBeVisible();
  });

  it("UserPage_SuspendedWithoutWhoOrWhy_ShowsNoByPartAndNoReason", async () => {
    server.use(
      meAnswer(),
      userAnswer(suspendedJohn({ suspendedBy: null, suspensionReason: null })),
    );

    renderWithProviders(<UserPage userId={john.id} />);

    expect(await screen.findByText("Suspended on 25 Oct 2026")).toBeVisible();
    expect(screen.getByText("They can’t sign in.")).toBeVisible();
    expect(screen.queryByText(/ by /)).toBeNull();
    expect(screen.queryByText(/Reason:/)).toBeNull();
  });

  it("UserPage_SuspendedWithoutAMoment_ShowsNoBanner", async () => {
    server.use(meAnswer(), userAnswer(suspendedJohn({ suspendedAt: null })));

    renderWithProviders(<UserPage userId={john.id} />);

    expect(
      await screen.findByRole("heading", { level: 1, name: "John Byrne" }),
    ).toBeVisible();
    expect(screen.queryByText(/Suspended on/)).toBeNull();
  });

  it("UserPage_Suspended_OffersReactivate", async () => {
    server.use(meAnswer(), userAnswer(suspendedJohn()));

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
      userAnswer(suspendedJohn({ suspendedBy: { name: null, deleted: true } })),
    );

    renderWithProviders(<UserPage userId={john.id} />);

    expect(
      await screen.findByText("Suspended on 25 Oct 2026 by Deleted user"),
    ).toBeVisible();
  });

  it("UserPage_Deleted_ShowsTheDeletedStateWithoutActions", async () => {
    server.use(meAnswer(), userAnswer(userDetailOf(erased)));

    renderWithProviders(<UserPage userId={erased.id} />);

    expect(
      await screen.findByRole("heading", { level: 1, name: "Deleted user" }),
    ).toBeVisible();
    expect(screen.getAllByText("Deleted")[0]).toBeVisible();
    expect(screen.queryByRole("button", { name: /Actions for/ })).toBeNull();
    expect(screen.queryByRole("button", { name: /Suspend/ })).toBeNull();
    expect(screen.getByText("In no organisation.")).toBeVisible();
  });

  it("UserPage_SystemAdmin_ShowsTheSystemAdminLabel", async () => {
    server.use(meAnswer(), userAnswer(userDetailOf(rory)));

    renderWithProviders(<UserPage userId={rory.id} />);

    expect(await screen.findByText("System admin")).toBeVisible();
    expect(screen.getByText("Root")).toBeVisible();
    expect(screen.getByText("In no organisation.")).toBeVisible();
  });

  it("UserPage_DeleteASystemAdmin_SaysWhyAtOnce", async () => {
    server.use(meAnswer(), userAnswer(userDetailOf(rory)));

    renderWithProviders(<UserPage userId={rory.id} />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Actions for Rory Brennan" }),
    );
    await userEvent.click(
      await screen.findByRole("menuitem", { name: "Delete user" }),
    );

    expect(
      await screen.findByRole("alertdialog", {
        name: "Rory Brennan can’t be deleted",
      }),
    ).toBeVisible();
  });

  it("UserPage_DeleteTheLastOwner_SaysWhyAtOnce", async () => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ayse, { memberships: ayseMemberships })),
    );

    renderWithProviders(<UserPage userId={ayse.id} />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Actions for Ayşe Yılmaz" }),
    );
    await userEvent.click(
      await screen.findByRole("menuitem", { name: "Delete user" }),
    );

    expect(
      await screen.findByRole("alertdialog", {
        name: "Ayşe Yılmaz can’t be deleted yet",
      }),
    ).toBeVisible();
  });

  it("UserPage_Memberships_ShowEachOrganisationWithItsRole", async () => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(mehmet, { memberships: mehmetMemberships })),
    );

    renderWithProviders(<UserPage userId={mehmet.id} />);

    const northwindRow = await membershipRow("Northwind Traders");
    expect(northwindRow.getByText("Admin")).toBeVisible();
    expect(northwindRow.queryByText("northwind")).toBeNull();
    expect(northwindRow.queryByText("5 Mar 2026")).toBeNull();
    expect(
      northwindRow.getByRole("link", { name: "Northwind Traders" }),
    ).toHaveAttribute("href", `/organisations/${northwind.id}`);
    const custom = (await membershipRow("Shannon Studio")).getByText(
      "Billing lead",
    );
    expect(custom.tagName).toBe("BDI");
  });

  it("UserPage_MembershipOfAStoppedOrganisation_ShowsItsStatus", async () => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ayse, { memberships: ayseMemberships })),
    );

    renderWithProviders(<UserPage userId={ayse.id} />);

    expect(
      (await membershipRow("Atlas Logistics")).getByText("Suspended"),
    ).toHaveAttribute("data-tone", "warning");
    expect(
      (await membershipRow("Northwind Traders")).queryByText("Active"),
    ).toBeNull();
  });

  it("UserPage_BuiltInRole_ShowsItsTranslatedName", async () => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(mehmet, { memberships: mehmetMemberships })),
    );

    renderWithProviders(<UserPage userId={mehmet.id} />, "tr");

    expect(
      (await membershipRow("Northwind Traders", "Kuruluşlar")).getByText(
        "Yönetici",
      ),
    ).toBeVisible();
  });

  it("UserPage_OwnerMembership_OffersNoMakeOwner", async () => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ayse, { memberships: ayseMemberships })),
    );

    renderWithProviders(<UserPage userId={ayse.id} />);

    const row = await membershipRow("Northwind Traders");
    expect(row.getByText("Owner")).toBeVisible();
    expect(row.queryByRole("button", { name: /Make Owner/ })).toBeNull();
  });

  it("UserPage_OtherMembership_MakeOwnerOpensItsDialog", async () => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ayse, { memberships: ayseMemberships })),
    );

    renderWithProviders(<UserPage userId={ayse.id} />);
    await userEvent.click(
      (await membershipRow("Atlas Logistics")).getByRole("button", {
        name: "Make Owner of Atlas Logistics",
      }),
    );

    expect(
      await screen.findByRole("alertdialog", {
        name: "Make Ayşe Yılmaz an Owner of Atlas Logistics?",
      }),
    ).toBeVisible();
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
