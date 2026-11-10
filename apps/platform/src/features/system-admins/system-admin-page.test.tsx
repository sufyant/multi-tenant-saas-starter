import { ErrorCode } from "@repo/api-client";
import { screen, waitFor, within } from "@testing-library/react";
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
import { problem } from "../../../test/organisations";
import { renderWithProviders } from "../../../test/render";
import {
  ayse,
  ciara,
  meAs,
  rory,
  tomas,
  userAnswer,
  userDetailOf,
} from "../../../test/users";
import { SystemAdminPage } from "./system-admin-page";

const router = vi.hoisted(() => ({ push: vi.fn(), refresh: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => router }));
vi.mock("@clerk/nextjs", () => ({ useSession: () => ({ session: null }) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const added = async () => {
  const dt = await screen.findByText("Added", { selector: "dt" });
  return dt.nextElementSibling as HTMLElement;
};

describe("SystemAdminPage", () => {
  it("SystemAdminPage_Admin_ShowsTheNameEmailAndWhenAndByWhomAdded", async () => {
    server.use(
      meAs(ciara),
      userAnswer(
        userDetailOf(tomas, {
          systemAdminGrantedBy: { name: "Rory Brennan", deleted: false },
        }),
      ),
    );

    renderWithProviders(<SystemAdminPage userId={tomas.id} />);

    expect(
      await screen.findByRole("heading", { level: 1, name: "Tomás Ryan" }),
    ).toBeVisible();
    expect(screen.getByText("tomas@starter.example")).toBeVisible();
    expect(screen.getByRole("link", { name: "System admins" })).toHaveAttribute(
      "href",
      "/settings/system-admins",
    );
    expect(await added()).toHaveTextContent("2 Mar 2026, by Rory Brennan");
  });

  it("SystemAdminPage_GrantedByNobodyKnown_ShowsOnlyTheDate", async () => {
    server.use(meAs(ciara), userAnswer(userDetailOf(tomas)));

    renderWithProviders(<SystemAdminPage userId={tomas.id} />);

    expect(await added()).toHaveTextContent(/^2 Mar 2026$/);
  });

  it("SystemAdminPage_GrantedByADeletedAdmin_NamesThemDeletedUser", async () => {
    server.use(
      meAs(ciara),
      userAnswer(
        userDetailOf(tomas, {
          systemAdminGrantedBy: { name: null, deleted: true },
        }),
      ),
    );

    renderWithProviders(<SystemAdminPage userId={tomas.id} />);

    expect(await added()).toHaveTextContent("2 Mar 2026, by Deleted user");
  });

  // No field says whether a new admin has set up their app yet, so the row says the rule and no "On" (the maintainer's decision of
  // 2026-11-10).
  it("SystemAdminPage_TwoStep_SaysItIsRequiredWithNoOnLabel", async () => {
    server.use(meAs(ciara), userAnswer(userDetailOf(tomas)));

    renderWithProviders(<SystemAdminPage userId={tomas.id} />);

    const dt = await screen.findByText("Two-step verification", {
      selector: "dt",
    });
    expect(dt.nextElementSibling).toHaveTextContent(
      /^Required for every system admin\.$/,
    );
    expect(screen.queryByText("On")).toBeNull();
  });

  it("SystemAdminPage_OtherAdmin_RemoveOpensItsDialog", async () => {
    server.use(meAs(ciara), userAnswer(userDetailOf(tomas)));

    renderWithProviders(<SystemAdminPage userId={tomas.id} />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Remove system admin" }),
    );

    expect(
      await screen.findByRole("alertdialog", {
        name: "Remove Tomás Ryan as a system admin?",
      }),
    ).toBeVisible();
  });

  // After a removal the user is no system admin, so the page has nothing to show: it goes back to the list.
  it("SystemAdminPage_Removed_GoesBackToSystemAdmins", async () => {
    server.use(
      meAs(ciara),
      userAnswer(userDetailOf(tomas)),
      http.delete(
        `*/v1/platform/users/${tomas.id}/system-admin`,
        () => new HttpResponse(null, { status: 204 }),
      ),
    );

    renderWithProviders(<SystemAdminPage userId={tomas.id} />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Remove system admin" }),
    );
    await userEvent.click(
      within(
        await screen.findByRole("alertdialog", {
          name: "Remove Tomás Ryan as a system admin?",
        }),
      ).getByRole("button", { name: "Remove" }),
    );

    await waitFor(() =>
      expect(router.push).toHaveBeenCalledWith("/settings/system-admins"),
    );
  });

  it("SystemAdminPage_OwnPage_DisablesRemoveWithTheReason", async () => {
    server.use(meAs(ciara), userAnswer(userDetailOf(ciara)));

    renderWithProviders(<SystemAdminPage userId={ciara.id} />);

    const remove = await screen.findByRole("button", {
      name: "Remove system admin",
    });
    await waitFor(() =>
      expect(remove).toHaveAccessibleDescription(
        "You can’t remove yourself. Ask another system admin.",
      ),
    );
    expect(remove).toBeDisabled();
  });

  it("SystemAdminPage_Root_OffersNoRemoveAndSaysWhy", async () => {
    server.use(meAs(ciara), userAnswer(userDetailOf(rory)));

    renderWithProviders(<SystemAdminPage userId={rory.id} />);

    expect(await screen.findByText("Root")).toBeVisible();
    expect(
      screen.getByText("Root admins come from the platform’s configuration."),
    ).toBeVisible();
    expect(
      screen.queryByRole("button", { name: "Remove system admin" }),
    ).toBeNull();
  });

  it("SystemAdminPage_UserWhoIsNoSystemAdmin_ShowsPageNotFound", async () => {
    server.use(meAs(ciara), userAnswer(userDetailOf(ayse)));

    renderWithProviders(<SystemAdminPage userId={ayse.id} />);

    expect(await screen.findByText("Page not found")).toBeVisible();
  });

  it("SystemAdminPage_UnknownUser_ShowsPageNotFound", async () => {
    server.use(
      meAs(ciara),
      http.get(`*/v1/platform/users/${tomas.id}`, () =>
        problem(404, ErrorCode.UserNotFound),
      ),
    );

    renderWithProviders(<SystemAdminPage userId={tomas.id} />);

    expect(await screen.findByText("Page not found")).toBeVisible();
  });

  it("SystemAdminPage_IdThatIsNoId_ShowsPageNotFoundWithoutARead", async () => {
    const read = vi.fn();
    server.use(
      meAs(ciara),
      http.get("*/v1/platform/users/*", () => {
        read();
        return HttpResponse.json({});
      }),
    );

    renderWithProviders(<SystemAdminPage userId="not-an-id" />);

    expect(await screen.findByText("Page not found")).toBeVisible();
    expect(read).not.toHaveBeenCalled();
  });
});
