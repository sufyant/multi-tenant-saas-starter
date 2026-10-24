import { ErrorCode, type UserResponse } from "@repo/api-client";
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
import { fakeSession, wrongCode } from "../../../test/session";
import {
  ciara,
  meAs,
  mehmet,
  systemAdmins,
  tomas,
  userResponse,
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

/** ListUsers: the page's list without `q`, the people a search by email finds with it. Each request is recorded. */
const users = (found: UserResponse[], requests: URLSearchParams[] = []) =>
  http.get("*/v1/platform/users", ({ request }) => {
    const params = new URL(request.url).searchParams;
    requests.push(params);
    return HttpResponse.json(
      usersPageOf(params.has("q") ? found : systemAdmins),
    );
  });

/** GrantSystemAdmin for Mehmet: it records each key, writes "grant" into the log, and gives the answers in order. */
const grant = (
  answers: (() => Response)[],
  keys: (string | null)[],
  log: string[] = [],
) =>
  http.post(`*/v1/platform/users/${mehmet.id}/system-admin`, ({ request }) => {
    keys.push(request.headers.get("Idempotency-Key"));
    log.push("grant");
    const answer = answers[Math.min(keys.length, answers.length) - 1];
    return answer ? answer() : HttpResponse.error();
  });

const granted = userResponse(mehmet, {
  isSystemAdmin: true,
  systemAdminGrantedAt: "2026-10-24T10:00:00Z",
});

async function openAdd() {
  renderWithProviders(<SystemAdminsPage />);
  await userEvent.click(
    await screen.findByRole("button", { name: "Add system admin" }),
  );
  return within(
    await screen.findByRole("alertdialog", { name: "Add system admin" }),
  );
}

async function find(email: string) {
  const dialog = await openAdd();
  await userEvent.type(dialog.getByRole("textbox", { name: "Email" }), email);
  return dialog;
}

async function confirmMehmet() {
  const dialog = await find("mehmet@northwind.example");
  await dialog.findByText("Member of 2 organisations");
  await userEvent.click(dialog.getByRole("button", { name: "Continue" }));
  return within(
    await screen.findByRole("alertdialog", {
      name: "Make Mehmet Kaya a system admin?",
    }),
  );
}

async function makeSystemAdmin() {
  const dialog = await confirmMehmet();
  await userEvent.click(
    dialog.getByRole("button", { name: "Make system admin" }),
  );
  return dialog;
}

async function reverify() {
  return within(
    await screen.findByRole("alertdialog", { name: "Confirm it’s you" }),
  );
}

describe("AddSystemAdminDialog", () => {
  it("Add_ExactEmail_ShowsThePersonsCard", async () => {
    const requests: URLSearchParams[] = [];
    server.use(meAs(ciara), users([mehmet], requests));

    const dialog = await find("mehmet@northwind.example");

    expect(await dialog.findByText("Mehmet Kaya")).toBeVisible();
    expect(dialog.getByText("mehmet@northwind.example")).toBeVisible();
    expect(dialog.getByText("Member of 2 organisations")).toBeVisible();
    expect(dialog.getByRole("button", { name: "Continue" })).toBeEnabled();
    expect(requests.at(-1)?.get("q")).toBe("mehmet@northwind.example");
  });

  it("Add_EmailField_OffersNoSuggestions", async () => {
    server.use(meAs(ciara), users([]));

    const dialog = await openAdd();

    expect(dialog.getByRole("textbox", { name: "Email" })).toHaveAttribute(
      "autocomplete",
      "off",
    );
    expect(
      dialog.getByText(
        "Type the person’s full email address. There are no suggestions.",
      ),
    ).toBeVisible();
  });

  it("Add_PartialMatch_SaysNoUser", async () => {
    server.use(meAs(ciara), users([mehmet]));

    const dialog = await find("mehmet@northwind.ex");

    expect(
      await dialog.findByText(
        "No user has this email. They must sign in once first.",
      ),
    ).toBeVisible();
    expect(dialog.queryByText("Mehmet Kaya")).toBeNull();
    expect(dialog.getByRole("button", { name: "Continue" })).toBeDisabled();
  });

  it("Add_OtherCase_FindsTheUser", async () => {
    server.use(meAs(ciara), users([mehmet]));

    const dialog = await find("MEHMET@Northwind.Example");

    expect(await dialog.findByText("Mehmet Kaya")).toBeVisible();
    expect(dialog.getByRole("button", { name: "Continue" })).toBeEnabled();
  });

  it("Add_AlreadyASystemAdmin_SaysSoWithoutAStepTwo", async () => {
    server.use(meAs(ciara), users([tomas]));

    const dialog = await find("tomas@starter.example");

    expect(
      await dialog.findByText(
        sentence("Tomás Ryan is already a system admin."),
      ),
    ).toBeVisible();
    expect(dialog.getByRole("button", { name: "Continue" })).toBeDisabled();
  });

  it("Add_Continue_AsksToConfirmWithASecondaryButton", async () => {
    server.use(meAs(ciara), users([mehmet]));

    const dialog = await confirmMehmet();

    expect(
      dialog.getByText(
        "They can manage every organisation and user on the platform. They set up an authenticator app on their next sign-in.",
      ),
    ).toBeVisible();
    const action = dialog.getByRole("button", { name: "Make system admin" });
    expect(action).toHaveClass("bg-card");
    expect(action).not.toHaveClass("bg-primary", "bg-destructive");
  });

  it("Add_Back_ReturnsToTheEmail", async () => {
    server.use(meAs(ciara), users([mehmet]));

    const dialog = await confirmMehmet();
    await userEvent.click(dialog.getByRole("button", { name: "Back" }));

    const first = within(
      await screen.findByRole("alertdialog", { name: "Add system admin" }),
    );
    expect(first.getByRole("textbox", { name: "Email" })).toHaveValue(
      "mehmet@northwind.example",
    );
  });

  it("Add_Confirm_GrantsWithAKeyToastsAndReadsTheListAgain", async () => {
    const requests: URLSearchParams[] = [];
    const keys: (string | null)[] = [];
    server.use(meAs(ciara), users([mehmet], requests), grant([granted], keys));

    await makeSystemAdmin();

    expect(
      await screen.findByText(sentence("Mehmet Kaya is a system admin.")),
    ).toBeVisible();
    expect(keys).toHaveLength(1);
    expect(keys[0]).toMatch(/^[\da-f-]{36}$/);
    await vi.waitFor(() =>
      expect(
        requests.filter((params) => params.get("isSystemAdmin") === "true"),
      ).toHaveLength(2),
    );
    expect(screen.queryByRole("alertdialog")).toBeNull();
  });

  it.each([
    [
      ErrorCode.SystemAdminExists,
      409,
      "Mehmet Kaya is already a system admin. The page has been refreshed.",
    ],
    [ErrorCode.UserSuspended, 409, "Reactivate Mehmet Kaya first."],
    [ErrorCode.UserDeleted, 409, "This user was deleted."],
    [ErrorCode.UserNotFound, 404, "This user no longer exists."],
  ])("Add_Refused%s_ShowsItsText", async (code, status, text) => {
    server.use(
      meAs(ciara),
      users([mehmet]),
      grant([() => problem(status, code)], []),
    );

    const dialog = await makeSystemAdmin();

    expect(await dialog.findByText(sentence(text))).toBeVisible();
  });

  it("Add_BackAfterAFailedConnectionThenTheSamePerson_KeepsTheKey", async () => {
    const keys: (string | null)[] = [];
    server.use(
      meAs(ciara),
      users([mehmet]),
      grant([() => HttpResponse.error(), granted], keys),
    );

    const dialog = await makeSystemAdmin();
    await dialog.findByText(sentence("Mehmet Kaya did not change."));
    await userEvent.click(dialog.getByRole("button", { name: "Back" }));
    const first = within(
      await screen.findByRole("alertdialog", { name: "Add system admin" }),
    );
    await userEvent.click(first.getByRole("button", { name: "Continue" }));
    const again = within(
      await screen.findByRole("alertdialog", {
        name: "Make Mehmet Kaya a system admin?",
      }),
    );
    await userEvent.click(
      again.getByRole("button", { name: "Make system admin" }),
    );

    expect(
      await screen.findByText(sentence("Mehmet Kaya is a system admin.")),
    ).toBeVisible();
    expect(keys).toHaveLength(2);
    expect(keys[1]).toBe(keys[0]);
  });

  it("Add_Conflict_ReadsTheListAgain", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      meAs(ciara),
      users([mehmet], requests),
      grant([() => problem(409, ErrorCode.SystemAdminExists)], []),
    );

    const dialog = await makeSystemAdmin();
    await dialog.findByText(
      sentence(
        "Mehmet Kaya is already a system admin. The page has been refreshed.",
      ),
    );

    await vi.waitFor(() =>
      expect(
        requests.filter((params) => params.get("isSystemAdmin") === "true"),
      ).toHaveLength(2),
    );
  });

  it("Add_CallerSuspended_ReadsGetMeAgain", async () => {
    const reads = { count: 0 };
    server.use(
      http.get("*/v1/me", () => {
        reads.count += 1;
        return HttpResponse.json({
          ...ciara,
          isSystemAdmin: true,
          language: "en",
          timeZone: "Europe/Dublin",
        });
      }),
      users([mehmet]),
      grant([() => problem(403, ErrorCode.UserSuspended)], []),
    );

    await makeSystemAdmin();

    await vi.waitFor(() => expect(reads.count).toBe(2));
  });

  it("Add_RefusalThenAgain_SendsANewKey", async () => {
    const keys: (string | null)[] = [];
    server.use(
      meAs(ciara),
      users([mehmet]),
      grant([() => problem(409, ErrorCode.UserSuspended), granted], keys),
    );

    const dialog = await makeSystemAdmin();
    await dialog.findByText(sentence("Reactivate Mehmet Kaya first."));
    await userEvent.click(
      dialog.getByRole("button", { name: "Make system admin" }),
    );

    expect(
      await screen.findByText(sentence("Mehmet Kaya is a system admin.")),
    ).toBeVisible();
    expect(keys).toHaveLength(2);
    expect(keys[1]).not.toBe(keys[0]);
  });

  it("Add_OldSecondFactor_AsksForTheCode", async () => {
    clerk.session = fakeSession();
    server.use(
      meAs(ciara),
      users([mehmet]),
      grant(
        [() => problem(403, ErrorCode.SystemAdminReverificationRequired)],
        [],
      ),
    );

    await makeSystemAdmin();
    const step = await reverify();

    expect(
      step.getByText(
        "Enter the 6-digit code from your authenticator app. We ask again because your last check was a while ago.",
      ),
    ).toBeVisible();
    expect(step.getByRole("textbox", { name: "Code" })).toBeVisible();
    expect(
      step.getByRole("button", { name: "Verify and continue" }),
    ).toBeVisible();
    expect(
      step.getByRole("button", { name: "Use a backup code" }),
    ).toBeVisible();
  });

  it("Add_RightCode_RetriesWithTheSameKeyAndAFreshToken", async () => {
    const log: string[] = [];
    const session = fakeSession(log);
    clerk.session = session;
    const keys: (string | null)[] = [];
    server.use(
      meAs(ciara),
      users([mehmet]),
      grant(
        [
          () => problem(403, ErrorCode.SystemAdminReverificationRequired),
          granted,
        ],
        keys,
        log,
      ),
    );

    await makeSystemAdmin();
    const step = await reverify();
    await userEvent.type(step.getByRole("textbox", { name: "Code" }), "654321");
    await userEvent.click(
      step.getByRole("button", { name: "Verify and continue" }),
    );

    expect(
      await screen.findByText(sentence("Mehmet Kaya is a system admin.")),
    ).toBeVisible();
    expect(session.startVerification).toHaveBeenCalledWith({
      level: "second_factor",
    });
    expect(session.attemptSecondFactorVerification).toHaveBeenCalledWith({
      strategy: "totp",
      code: "654321",
    });
    expect(session.getToken).toHaveBeenCalledWith({ skipCache: true });
    expect(log).toEqual(["grant", "token", "grant"]);
    expect(keys).toHaveLength(2);
    expect(keys[1]).toBe(keys[0]);
  });

  it("Add_WrongCode_SaysToWaitForANewOneAndSendsNothing", async () => {
    const session = fakeSession();
    session.attemptSecondFactorVerification.mockRejectedValueOnce(wrongCode());
    clerk.session = session;
    const keys: (string | null)[] = [];
    server.use(
      meAs(ciara),
      users([mehmet]),
      grant(
        [() => problem(403, ErrorCode.SystemAdminReverificationRequired)],
        keys,
      ),
    );

    await makeSystemAdmin();
    const step = await reverify();
    await userEvent.type(step.getByRole("textbox", { name: "Code" }), "111111");
    await userEvent.click(
      step.getByRole("button", { name: "Verify and continue" }),
    );

    expect(
      await step.findByText(
        "That code does not match. Wait for the app to show a new one and try again.",
      ),
    ).toBeVisible();
    expect(keys).toHaveLength(1);
  });

  it("Add_BackupCode_VerifiesTheBackupCode", async () => {
    const session = fakeSession();
    clerk.session = session;
    server.use(
      meAs(ciara),
      users([mehmet]),
      grant(
        [
          () => problem(403, ErrorCode.SystemAdminReverificationRequired),
          granted,
        ],
        [],
      ),
    );

    await makeSystemAdmin();
    const step = await reverify();
    await userEvent.click(
      step.getByRole("button", { name: "Use a backup code" }),
    );
    await userEvent.type(
      step.getByRole("textbox", { name: "Backup code" }),
      "code3",
    );
    await userEvent.click(
      step.getByRole("button", { name: "Verify and continue" }),
    );

    expect(
      await screen.findByText(sentence("Mehmet Kaya is a system admin.")),
    ).toBeVisible();
    expect(session.attemptSecondFactorVerification).toHaveBeenCalledWith({
      strategy: "backup_code",
      code: "code3",
    });
  });

  it("Add_CancelAtTheCode_ClosesAndSendsNothingMore", async () => {
    const session = fakeSession();
    clerk.session = session;
    const keys: (string | null)[] = [];
    server.use(
      meAs(ciara),
      users([mehmet]),
      grant(
        [() => problem(403, ErrorCode.SystemAdminReverificationRequired)],
        keys,
      ),
    );

    await makeSystemAdmin();
    const step = await reverify();
    await userEvent.click(step.getByRole("button", { name: "Cancel" }));

    await vi.waitFor(() =>
      expect(screen.queryByRole("alertdialog")).toBeNull(),
    );
    expect(keys).toHaveLength(1);
    expect(session.attemptSecondFactorVerification).not.toHaveBeenCalled();
    expect(
      screen.queryByText(sentence("Mehmet Kaya is a system admin.")),
    ).toBeNull();
  });
});
