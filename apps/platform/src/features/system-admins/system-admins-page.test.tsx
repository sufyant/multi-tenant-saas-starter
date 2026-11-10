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

  it("SystemAdmins_Header_HasTheTextAndAddWithItsIconAndNoFreshness", async () => {
    server.use(meAs(ciara), usersAnswer(usersPageOf(systemAdmins)));

    renderWithProviders(<SystemAdminsPage />);

    await rowOf("Tomás Ryan");
    expect(
      screen.getByText(
        "System admins manage every organisation and user on the platform. Each one signs in with an authenticator app.",
      ),
    ).toBeVisible();
    expect(
      screen
        .getByRole("button", { name: "Add system admin" })
        .querySelector("svg"),
    ).toHaveClass("lucide-plus");
    expect(screen.queryByText(/Updated/)).toBeNull();
  });

  it("SystemAdmins_List_ShowsEachAdminWithEmailAndDateAdded", async () => {
    server.use(meAs(ciara), usersAnswer(usersPageOf(systemAdmins)));

    renderWithProviders(<SystemAdminsPage />);

    const row = await rowOf("Tomás Ryan");
    expect(row.getByText("tomas@starter.example")).toBeVisible();
    expect(row.getByText(dateOf("2026-03-02T10:00:00Z"))).toBeVisible();
    expect(
      within(await screen.findByRole("table", { name: "System admins" }))
        .getAllByRole("columnheader")
        .map((header) => header.textContent),
    ).toEqual(["Name", "Email", "Added", "Actions"]);
  });

  it("SystemAdmins_RootRow_HasNoActionsAndTheReasonIsTheRootLabelsTooltip", async () => {
    server.use(meAs(ciara), usersAnswer(usersPageOf(systemAdmins)));

    renderWithProviders(<SystemAdminsPage />);

    const row = await rowOf("Rory Brennan");
    expect(
      row.queryByText("Root admins come from the platform’s configuration."),
    ).toBeNull();
    expect(
      row.queryByRole("button", { name: "Actions for Rory Brennan" }),
    ).toBeNull();
    await userEvent.hover(row.getByText("Root"));
    expect(
      await screen.findByText(
        "Root admins come from the platform’s configuration.",
      ),
    ).toBeVisible();
  });

  it("SystemAdmins_RootLabel_GetsTheFocusForTheTooltip", async () => {
    server.use(meAs(ciara), usersAnswer(usersPageOf(systemAdmins)));

    renderWithProviders(<SystemAdminsPage />);

    const root = (await rowOf("Rory Brennan")).getByText("Root");
    expect(root.closest("[tabindex='0']")).not.toBeNull();
  });

  it("SystemAdmins_OwnRow_SaysYouAndDisablesRemoveWithTheReason", async () => {
    server.use(meAs(ciara), usersAnswer(usersPageOf(systemAdmins)));

    renderWithProviders(<SystemAdminsPage />);

    const row = await rowOf("Ciara Byrne (you)");
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

  it("SystemAdmins_MoreThanOnePage_LoadMoreAddsTheNextPageWithNoPageInTheUrl", async () => {
    window.history.replaceState(null, "", "/settings/system-admins");
    const pages: (string | null)[] = [];
    server.use(
      meAs(ciara),
      http.get("*/v1/platform/users", ({ request }) => {
        const page = new URL(request.url).searchParams.get("page");
        pages.push(page);
        return HttpResponse.json(
          page === "2"
            ? usersPageOf([tomas], { page: 2, pageSize: 2, total: 3 })
            : usersPageOf([rory, ciara], { pageSize: 2, total: 3 }),
        );
      }),
    );

    renderWithProviders(<SystemAdminsPage />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Load more" }),
    );

    expect(await rowOf("Tomás Ryan")).toBeTruthy();
    expect(screen.getByText("1 more system admin loaded.")).toHaveAttribute(
      "aria-live",
      "polite",
    );
    expect(pages).toEqual(["1", "2"]);
    expect(window.location.search).toBe("");
    expect(screen.queryByRole("button", { name: "Next" })).toBeNull();
  });

  it("SystemAdmins_PhoneCard_LinksToTheAdminsPage", async () => {
    server.use(meAs(ciara), usersAnswer(usersPageOf([tomas])));

    renderWithProviders(<SystemAdminsPage />);

    const cards = within(
      await screen.findByRole("list", { name: "System admins" }),
    );
    expect(cards.getByRole("link", { name: "Tomás Ryan" })).toHaveAttribute(
      "href",
      `/settings/system-admins/${tomas.id}`,
    );
  });
});
