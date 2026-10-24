import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
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
import { dateOf } from "../../../test/organisations";
import { renderWithProviders } from "../../../test/render";
import {
  ciara,
  meAs,
  systemAdmins,
  tomas,
  usersAnswer,
  usersPageOf,
} from "../../../test/users";
import { SystemAdminsPage } from "./system-admins-page";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));
vi.mock("@clerk/nextjs", () => ({ useSession: () => ({ session: null }) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

// The table and the phone's card list both hold a row; jsdom has no container query to hide one.
const rowOf = async (name: string) => {
  const table = within(
    await screen.findByRole("table", { name: "System admins" }),
  );
  return within((await table.findByText(name)).closest("tr") as HTMLElement);
};

describe("SystemAdminsPage", () => {
  it("SystemAdmins_List_AsksOnlyForSystemAdmins", async () => {
    const requests: URLSearchParams[] = [];
    server.use(meAs(ciara), usersAnswer(usersPageOf(systemAdmins), requests));

    renderWithProviders(<SystemAdminsPage />);

    expect(
      await screen.findByRole("heading", { level: 1, name: "System admins" }),
    ).toBeVisible();
    await rowOf("Tomás Ryan");
    expect(requests[0]?.get("isSystemAdmin")).toBe("true");
  });

  it("SystemAdmins_List_ShowsEachAdminWithEmailAndDateAdded", async () => {
    server.use(meAs(ciara), usersAnswer(usersPageOf(systemAdmins)));

    renderWithProviders(<SystemAdminsPage />);

    const row = await rowOf("Tomás Ryan");
    expect(row.getByText("tomas@starter.example")).toBeVisible();
    expect(row.getByText(dateOf("2026-03-02T10:00:00Z"))).toBeVisible();
  });

  it("SystemAdmins_RootRow_HasNoActionsAndSaysWhereRootAdminsComeFrom", async () => {
    server.use(meAs(ciara), usersAnswer(usersPageOf(systemAdmins)));

    renderWithProviders(<SystemAdminsPage />);

    const row = await rowOf("Rory Brennan");
    expect(row.getByText("Root")).toBeVisible();
    expect(
      row.getByText("Root admins come from the platform’s configuration."),
    ).toBeVisible();
    expect(
      row.queryByRole("button", { name: "Actions for Rory Brennan" }),
    ).toBeNull();
  });

  it("SystemAdmins_OwnRow_DisablesRemoveWithTheReason", async () => {
    server.use(meAs(ciara), usersAnswer(usersPageOf(systemAdmins)));

    renderWithProviders(<SystemAdminsPage />);

    const row = await rowOf("Ciara Byrne");
    expect(row.getByText("You")).toBeVisible();
    await userEvent.click(
      row.getByRole("button", { name: "Actions for Ciara Byrne" }),
    );
    const item = await screen.findByRole("menuitem", {
      name: /Remove system admin/,
    });
    expect(item).toHaveAttribute("aria-disabled", "true");
    expect(item).toHaveTextContent(
      "You can’t remove yourself. Ask another system admin.",
    );
  });

  it("SystemAdmins_OtherAdmin_OffersRemove", async () => {
    server.use(meAs(ciara), usersAnswer(usersPageOf([tomas])));

    renderWithProviders(<SystemAdminsPage />);

    const row = await rowOf("Tomás Ryan");
    await userEvent.click(
      row.getByRole("button", { name: "Actions for Tomás Ryan" }),
    );
    expect(
      await screen.findByRole("menuitem", { name: "Remove system admin" }),
    ).not.toHaveAttribute("aria-disabled");
  });

  it("SystemAdmins_SettingsNav_HasPreferencesAndSystemAdmins", async () => {
    server.use(meAs(ciara), usersAnswer(usersPageOf(systemAdmins)));

    renderWithProviders(<SystemAdminsPage />);

    const nav = within(
      await screen.findByRole("navigation", { name: "Settings" }),
    );
    expect(nav.getByRole("link", { name: "Preferences" })).toHaveAttribute(
      "href",
      "/settings/preferences",
    );
    expect(nav.getByRole("link", { name: "System admins" })).toHaveAttribute(
      "aria-current",
      "page",
    );
  });
});
