import { ErrorCode } from "@repo/api-client";
import { expectVisibleToast } from "@repo/ui/test/toast";
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
import { problem } from "../../../test/organisations";
import { renderWithProviders, sentence } from "../../../test/render";
import { fakeSession } from "../../../test/session";
import {
  ciara,
  meAs,
  rory,
  tomas,
  usersAnswer,
  usersPageOf,
} from "../../../test/users";
import { SystemAdminsPage } from "./system-admins-page";

const clerk = vi.hoisted(() => ({ session: null as unknown }));
vi.mock("@clerk/nextjs", () => ({
  useSession: () => ({ session: clerk.session }),
}));
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  vi.clearAllMocks();
});
afterAll(() => server.close());

/** RevokeSystemAdmin for Tomás: it counts the calls, writes "revoke" into the log, and gives the answers in order. */
const revoke = (
  answers: (() => Response)[],
  calls: { count: number },
  log: string[] = [],
) =>
  http.delete(`*/v1/platform/users/${tomas.id}/system-admin`, () => {
    calls.count += 1;
    log.push("revoke");
    const answer = answers[Math.min(calls.count, answers.length) - 1];
    return answer ? answer() : HttpResponse.error();
  });

const revoked = () => new HttpResponse(null, { status: 204 });

async function remove() {
  renderWithProviders(<SystemAdminsPage />);
  const table = within(
    await screen.findByRole("table", { name: "System admins" }),
  );
  const row = within(
    (await table.findByText("Tomás Ryan")).closest("tr") as HTMLElement,
  );
  await userEvent.click(
    row.getByRole("button", { name: "Actions for Tomás Ryan" }),
  );
  await userEvent.click(
    await screen.findByRole("menuitem", { name: "Remove system admin" }),
  );
  const dialog = within(
    await screen.findByRole("alertdialog", {
      name: "Remove Tomás Ryan as a system admin?",
    }),
  );
  await userEvent.click(dialog.getByRole("button", { name: "Remove" }));
  return dialog;
}

describe("RemoveSystemAdminDialog", () => {
  it("Remove_Confirm_RevokesToastsAndReadsTheListAgain", async () => {
    const requests: URLSearchParams[] = [];
    const calls = { count: 0 };
    server.use(
      meAs(ciara),
      usersAnswer(usersPageOf([rory, ciara, tomas]), requests),
      revoke([revoked], calls),
    );

    await remove();

    await expectVisibleToast(
      sentence("Tomás Ryan is no longer a system admin."),
    );
    expect(calls.count).toBe(1);
    await vi.waitFor(() => expect(requests).toHaveLength(2));
  });

  it("Remove_Dialog_IsRedAndSaysWhatStays", async () => {
    server.use(
      meAs(ciara),
      usersAnswer(usersPageOf([tomas])),
      revoke([revoked], { count: 0 }),
    );
    renderWithProviders(<SystemAdminsPage />);
    const table = within(
      await screen.findByRole("table", { name: "System admins" }),
    );
    await userEvent.click(
      within(
        (await table.findByText("Tomás Ryan")).closest("tr") as HTMLElement,
      ).getByRole("button", { name: "Actions for Tomás Ryan" }),
    );
    await userEvent.click(
      await screen.findByRole("menuitem", { name: "Remove system admin" }),
    );

    const dialog = within(
      await screen.findByRole("alertdialog", {
        name: "Remove Tomás Ryan as a system admin?",
      }),
    );
    expect(
      dialog.getByText(
        "They can no longer open the platform. Their memberships in organisations stay.",
      ),
    ).toBeVisible();
    expect(dialog.getByRole("button", { name: "Remove" })).toHaveClass(
      "bg-destructive",
    );
  });

  it.each([
    [ErrorCode.SystemAdminRoot, 409, "Root admins can’t be removed here."],
    [
      ErrorCode.SystemAdminCannotRevokeSelf,
      409,
      "You can’t remove yourself. Ask another system admin.",
    ],
    [
      ErrorCode.SystemAdminNotFound,
      404,
      "Tomás Ryan is no longer a system admin.",
    ],
    [ErrorCode.UserNotFound, 404, "This user no longer exists."],
  ])("Remove_Refused%s_ShowsItsText", async (code, status, text) => {
    server.use(
      meAs(ciara),
      usersAnswer(usersPageOf([tomas])),
      revoke([() => problem(status, code)], { count: 0 }),
    );

    const dialog = await remove();

    expect(await dialog.findByText(sentence(text))).toBeVisible();
  });

  it("Remove_RightCodeAfterAnOldSecondFactor_RevokesAgainWithAFreshToken", async () => {
    const log: string[] = [];
    const session = fakeSession(log);
    clerk.session = session;
    const calls = { count: 0 };
    server.use(
      meAs(ciara),
      usersAnswer(usersPageOf([tomas])),
      revoke(
        [
          () => problem(403, ErrorCode.SystemAdminReverificationRequired),
          revoked,
        ],
        calls,
        log,
      ),
    );

    await remove();
    const step = within(
      await screen.findByRole("alertdialog", { name: "Confirm it’s you" }),
    );
    await userEvent.type(step.getByRole("textbox", { name: "Code" }), "654321");
    await userEvent.click(
      step.getByRole("button", { name: "Verify and continue" }),
    );

    await expectVisibleToast(
      sentence("Tomás Ryan is no longer a system admin."),
    );
    expect(log).toEqual(["revoke", "token", "revoke"]);
    expect(calls.count).toBe(2);
  });

  // In Arabic a Latin name reorders the punctuation around it unless it is isolated (W3C, "Inline markup and bidirectional text").
  it("Remove_Arabic_IsolatesTheAdminsNameInTheTitle", async () => {
    server.use(meAs(ciara), usersAnswer(usersPageOf([tomas])));
    renderWithProviders(<SystemAdminsPage />, "ar");
    const table = within(
      await screen.findByRole("table", { name: "مسؤولو النظام" }),
    );

    await userEvent.click(
      await table.findByRole("button", { name: "إجراءات Tomás Ryan" }),
    );
    await userEvent.click(
      await screen.findByRole("menuitem", { name: "إزالة مسؤول النظام" }),
    );
    const dialog = await screen.findByRole("alertdialog", {
      name: "إزالة Tomás Ryan من مسؤولي النظام؟",
    });

    expect(
      within(dialog).getByText("Tomás Ryan", { selector: "bdi" }),
    ).toBeVisible();
  });
});
