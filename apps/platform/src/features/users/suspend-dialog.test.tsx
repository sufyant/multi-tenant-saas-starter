import { ErrorCode, UserResponseStatus } from "@repo/api-client";
import { screen, waitFor, within } from "@testing-library/react";
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
import {
  atlas,
  kestrel,
  meAnswer,
  northwind,
  problem,
  shannon,
} from "../../../test/organisations";
import { renderWithProviders, sentence } from "../../../test/render";
import { openUserAction } from "../../../test/user-page";
import {
  ayse,
  ayseMemberships,
  ciara,
  mehmet,
  mehmetMemberships,
  membershipOf,
  platformCommand,
  userAnswer,
  userDetailOf,
  userResponse,
} from "../../../test/users";
import { UserPage } from "./user-page";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const open = (user = ayse, name = "Ayşe Yılmaz") =>
  openUserAction(user, name, "Suspend", `Suspend ${name}?`);

const suspend = (dialog: Awaited<ReturnType<typeof open>>) =>
  userEvent.click(dialog.getByRole("button", { name: "Suspend user" }));

const lastOwnerOf = (
  ...organisations: { id: string; name: string; slug: string }[]
) =>
  organisations.map((organisation) =>
    membershipOf(organisation, { lastOwner: true }),
  );

describe("SuspendUserDialog", () => {
  it("Suspend_Success_ToastsAndReadsTheUserAgain", async () => {
    const reads = { count: 0 };
    const calls: { key: string | null; body: unknown }[] = [];
    server.use(
      meAnswer(),
      userAnswer(
        userDetailOf(mehmet, { memberships: mehmetMemberships }),
        reads,
      ),
      platformCommand(
        "post",
        `users/${mehmet.id}/suspend`,
        [userResponse(mehmet, { status: UserResponseStatus.Suspended })],
        calls,
      ),
    );

    const dialog = await open(mehmet, "Mehmet Kaya");
    expect(
      dialog.getByText(
        "They are refused on their next request, everywhere. Their memberships are kept. You can reactivate them at any time.",
      ),
    ).toBeVisible();
    expect(dialog.queryByText(/is the last Owner of/)).toBeNull();
    await suspend(dialog);

    expect(
      await screen.findByText(sentence("Mehmet Kaya suspended")),
    ).toBeVisible();
    expect(
      screen.getByText("They are refused on their next request."),
    ).toBeVisible();
    expect(calls).toEqual([{ key: null, body: null }]);
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("Suspend_LastOwner_WarnsByTheOrganisationsNameAndStillSuspends", async () => {
    const calls: { key: string | null; body: unknown }[] = [];
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ayse, { memberships: ayseMemberships })),
      platformCommand(
        "post",
        `users/${ayse.id}/suspend`,
        [userResponse(ayse, { status: UserResponseStatus.Suspended })],
        calls,
      ),
    );

    const dialog = await open();
    expect(
      dialog.getByText(
        sentence("Ayşe Yılmaz is the last Owner of Northwind Traders."),
      ),
    ).toBeVisible();
    expect(
      dialog.getByText(
        "While they are suspended, nobody there can invite, change roles or remove members.",
      ),
    ).toBeVisible();
    expect(dialog.getByRole("button", { name: "Suspend user" })).toBeEnabled();
    await suspend(dialog);

    expect(
      await screen.findByText(sentence("Ayşe Yılmaz suspended")),
    ).toBeVisible();
    expect(calls).toHaveLength(1);
  });

  it("Suspend_LastOwnerOfTwo_NamesBoth", async () => {
    server.use(
      meAnswer(),
      userAnswer(
        userDetailOf(ayse, { memberships: lastOwnerOf(northwind, atlas) }),
      ),
    );

    const dialog = await open();

    expect(
      dialog.getByText(
        sentence(
          "Ayşe Yılmaz is the last Owner of Northwind Traders and Atlas Logistics.",
        ),
      ),
    ).toBeVisible();
  });

  it("Suspend_LastOwnerOfFour_NamesThreeAndCountsTheRest", async () => {
    server.use(
      meAnswer(),
      userAnswer(
        userDetailOf(ayse, {
          memberships: lastOwnerOf(northwind, atlas, shannon, kestrel),
        }),
      ),
    );

    const dialog = await open();

    expect(
      dialog.getByText(
        sentence(
          "Ayşe Yılmaz is the last Owner of Northwind Traders, Atlas Logistics, Shannon Studio and 1 more.",
        ),
      ),
    ).toBeVisible();
  });

  it("Suspend_SystemAdmin_SaysRemoveThemFirst", async () => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ciara)),
      platformCommand("post", `users/${ciara.id}/suspend`, [
        () => problem(409, ErrorCode.UserIsSystemAdmin),
      ]),
    );

    const dialog = await open(ciara, "Ciara Byrne");
    await suspend(dialog);

    expect(
      await dialog.findByText(sentence("Ciara Byrne is a system admin.")),
    ).toBeVisible();
    expect(
      dialog.getByText("Remove them from System admins first."),
    ).toBeVisible();
  });

  it("Suspend_NotFound_SaysSoAndReadsTheUserAgain", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(mehmet), reads),
      platformCommand("post", `users/${mehmet.id}/suspend`, [
        () => problem(404, ErrorCode.UserNotFound),
      ]),
    );

    const dialog = await open(mehmet, "Mehmet Kaya");
    await suspend(dialog);

    expect(
      await dialog.findByText("This user no longer exists."),
    ).toBeVisible();
    await waitFor(() => expect(reads.count).toBe(2));
  });

  // In Arabic a Latin name reorders the punctuation around it unless it is isolated (W3C, "Inline markup and bidirectional text").
  it("Suspend_Arabic_IsolatesTheUsersNameInTheTitle", async () => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(mehmet, { memberships: mehmetMemberships })),
    );
    renderWithProviders(<UserPage userId={mehmet.id} />, "ar");

    await userEvent.click(
      await screen.findByRole("button", { name: "إجراءات Mehmet Kaya" }),
    );
    await userEvent.click(
      await screen.findByRole("menuitem", { name: "إيقاف" }),
    );
    const dialog = await screen.findByRole("alertdialog", {
      name: "إيقاف Mehmet Kaya؟",
    });

    expect(
      within(dialog).getByText("Mehmet Kaya", { selector: "bdi" }),
    ).toBeVisible();
  });
});
