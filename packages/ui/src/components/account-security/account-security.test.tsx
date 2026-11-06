import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import axe from "axe-core";
import { NextIntlClientProvider } from "next-intl";
import { afterEach, describe, expect, it, vi } from "vitest";
import { z } from "zod";
import en from "../../../messages/en.json";
import { expectVisibleToast } from "../../../test/toast";
import { dismissToasts, Toaster } from "../toast";
import { AccountSecurity, type AccountSecurityProps } from "./account-security";

// Clerk is a system we do not own: its hooks are faked at their boundary, with the resources the page reads.
const clerk = vi.hoisted(() => ({
  user: null as unknown,
  session: { id: "sess_chrome" } as unknown,
  reverify: null as null | ((asked: unknown) => void),
}));
vi.mock("@clerk/nextjs", () => ({
  useUser: () => ({ user: clerk.user }),
  useSession: () => ({ session: clerk.session }),
  useReverification:
    (change: (...args: unknown[]) => Promise<unknown>, options: never) =>
    (...args: unknown[]) =>
      clerk.reverify
        ? new Promise((resolve, reject) =>
            clerk.reverify?.({
              ...(options as object),
              resolve,
              reject,
            }),
          )
        : change(...args),
}));

// Each app holds the locale's format; the page reads it as the apps do.
const messages = { ...en, locale: { format: "en-IE" } };
const now = new Date("2026-11-06T10:00:00Z");
const sessionOf = (
  id: string,
  browserName: string,
  deviceType: string,
  city: string,
  minutesAgo: number,
) => ({
  id,
  status: "active",
  lastActiveAt: new Date(now.getTime() - minutesAgo * 60_000),
  latestActivity: {
    id: `act_${id}`,
    browserName,
    deviceType,
    city,
    country: "TR",
  },
  revoke: vi.fn(async () => ({})),
});

function userOf({
  sessions = [
    sessionOf("sess_safari", "Safari", "iPhone", "Istanbul", 120),
    sessionOf("sess_chrome", "Chrome", "Macintosh", "Istanbul", 0),
    sessionOf("sess_firefox", "Firefox", "Windows", "Berlin", 3 * 24 * 60),
  ],
  accounts = [
    {
      id: "eac_google",
      provider: "google",
      emailAddress: "ayse.demir@gmail.example",
      verification: { status: "verified" },
      destroy: vi.fn(async () => {}),
    },
  ],
  getSessions = async () => sessions,
}: {
  sessions?: ReturnType<typeof sessionOf>[];
  accounts?: unknown[];
  getSessions?: () => Promise<unknown>;
} = {}) {
  return {
    externalAccounts: accounts,
    twoFactorEnabled: false,
    totpEnabled: false,
    getSessions: vi.fn(getSessions),
    createExternalAccount: vi.fn(async () => ({
      verification: {
        externalVerificationRedirectURL: new URL(
          "https://login.microsoftonline.example/authorize",
        ),
      },
    })),
    reload: vi.fn(async () => {}),
    createTOTP: vi.fn(async () => ({
      secret: "JBSWY3DPEHPK3PXP",
      uri: "otpauth://totp/Starter:ayse?secret=JBSWY3DPEHPK3PXP",
    })),
    verifyTOTP: vi.fn(async () => ({ backupCodes: tenCodes })),
    createBackupCode: vi.fn(async () => ({ codes: tenCodes.toReversed() })),
    disableTOTP: vi.fn(async () => ({})),
    sessions,
  };
}

const tenCodes = [
  "4f7k-2m9q",
  "b2hc-q7wv",
  "m3ty-8fjd",
  "k7vs-4dqe",
  "t2lc-7wzn",
  "x8pd-63tn",
  "9rze-5kla",
  "w6nu-2bpr",
  "e5ga-9hmx",
  "p8yf-3kre",
];

const props: AccountSecurityProps = {
  intro: "Ayşe Demir · these settings are the same in every organisation.",
  email: "ayse@northwind.example",
  // The email change has its own tests (email-change.test.tsx).
  emailChange: {
    schema: z.object({ email: z.string() }),
    send: async () => ({ expiresAt: "2026-11-06T10:10:00Z" }),
    verify: async () => {},
    readFailure: () => ({ problem: null, fields: null, saved: false }),
    onChanged: () => {},
  },
  twoStep: { text: "Optional.", required: false },
  returnPath: "/northwind/settings/account",
  now,
};

function renderPage(overrides: Partial<AccountSecurityProps> = {}) {
  render(
    <NextIntlClientProvider
      locale="en"
      messages={messages}
      timeZone="Europe/Istanbul"
      now={now}
    >
      <AccountSecurity {...props} {...overrides} />
      <Toaster dir="ltr" closeLabel="Close" regionLabel="Notifications" />
    </NextIntlClientProvider>,
  );
}

afterEach(() => {
  // sonner shows the toasts still open to the next Toaster that mounts, as the apps' test setup says.
  dismissToasts();
  clerk.reverify = null;
  clerk.session = { id: "sess_chrome" };
  window.history.replaceState(null, "", "/");
  vi.restoreAllMocks();
});

// jsdom draws nothing, so the rules of colour and of page regions stay out; jsdom counts Base UI's focus guards as focusable
// inside aria-hidden, and Chromium's axe does not (F37).
const violations = async (within: Element = document.body) =>
  (
    await axe.run(
      { include: [within], exclude: [["[data-base-ui-focus-guard]"]] },
      {
        rules: {
          "color-contrast": { enabled: false },
          region: { enabled: false },
        },
      },
    )
  ).violations.map((violation) => violation.id);

describe("AccountSecurity", () => {
  // 6.07: on a phone the top bar holds the page's title, so the page leaves its own out; the address and Copy share a line
  // and Verified goes under them. jsdom draws no breakpoint, so the classes say it.
  it("AccountSecurity_Phone_LeavesTheTitleToTheTopBarAndPutsVerifiedUnderTheAddress", () => {
    clerk.user = userOf();
    renderPage();

    expect(
      screen.getByRole("heading", { level: 1, name: "Account and security" }),
    ).toHaveClass("max-tablet:hidden");
    expect(
      screen.getByRole("button", { name: "Copy the email" }).parentElement,
    ).toHaveClass("max-tablet:basis-full");
  });

  it("AccountSecurity_Loaded_ShowsTheEmailTheMethodsAndTheSessions", async () => {
    clerk.user = userOf();
    renderPage();

    expect(screen.getByText("ayse@northwind.example")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Copy the email" }),
    ).toHaveTextContent("Copy");
    expect(screen.getByText("Verified")).toBeInTheDocument();
    expect(screen.getByText("ayse.demir@gmail.example")).toBeInTheDocument();
    expect(screen.getByText("Not connected")).toBeInTheDocument();
    const sessions = within(screen.getByRole("region", { name: "Sessions" }));
    expect(
      (await sessions.findAllByRole("listitem")).map(
        (row) => row.querySelector("p")?.textContent,
      ),
    ).toEqual([
      "Chrome on MacintoshThis device",
      "Safari on iPhone",
      "Firefox on Windows",
    ]);
    expect(
      sessions.getByText(/Istanbul, Türkiye · active now/),
    ).toBeInTheDocument();
    expect(sessions.getAllByRole("button", { name: "Sign out" })).toHaveLength(
      2,
    );
  });

  // Each provider keeps its own logo (6.01); a phone leaves the tiles out.
  it("AccountSecurity_Methods_ShowEachProvidersLogo", () => {
    clerk.user = userOf();
    renderPage();

    const methods = screen.getByRole("region", { name: "Sign-in methods" });
    expect(
      [...methods.querySelectorAll("img")].map((logo) =>
        logo.getAttribute("src"),
      ),
    ).toEqual(["/brands/google.svg", "/brands/microsoft.svg"]);
  });

  it("AccountSecurity_DisconnectGoogle_AsksThenDisconnectsAndSaysSo", async () => {
    const user = userOf();
    clerk.user = user;
    renderPage();

    await userEvent.click(screen.getByRole("button", { name: "Disconnect" }));
    const dialog = within(
      await screen.findByRole("alertdialog", { name: "Disconnect Google?" }),
    );
    await userEvent.click(dialog.getByRole("button", { name: "Disconnect" }));

    expect(
      (user.externalAccounts[0] as { destroy: () => void }).destroy,
    ).toHaveBeenCalledOnce();
    await expectVisibleToast("Google disconnected");
  });

  it("AccountSecurity_ConnectMicrosoft_GoesToTheProviderAndComesBackHere", async () => {
    const user = userOf();
    clerk.user = user;
    const assign = vi.fn();
    vi.spyOn(window, "location", "get").mockReturnValue({
      ...window.location,
      origin: "https://web.example.test",
      assign,
    });
    renderPage();

    await userEvent.click(screen.getByRole("button", { name: "Connect" }));

    expect(user.createExternalAccount).toHaveBeenCalledWith({
      strategy: "oauth_microsoft",
      redirectUrl:
        "https://web.example.test/northwind/settings/account?connected=microsoft",
    });
    expect(assign).toHaveBeenCalledWith(
      "https://login.microsoftonline.example/authorize",
    );
  });

  it("AccountSecurity_BackFromAProvider_SaysConnected", async () => {
    window.history.replaceState(null, "", "/settings/account?connected=google");
    clerk.user = userOf();
    renderPage();

    await expectVisibleToast("Google connected");
    expect(window.location.search).toBe("");
  });

  it("AccountSecurity_BackFromARefusal_SaysNotConnectedAndRemovesTheAttempt", async () => {
    window.history.replaceState(
      null,
      "",
      "/settings/account?connected=microsoft",
    );
    const attempt = {
      id: "eac_microsoft",
      provider: "microsoft",
      emailAddress: "",
      verification: { status: "unverified" },
      destroy: vi.fn(async () => {}),
    };
    clerk.user = userOf({ accounts: [attempt] });
    renderPage();

    await expectVisibleToast("Microsoft not connected");
    expect(attempt.destroy).toHaveBeenCalledOnce();
  });

  it("AccountSecurity_SignOutASession_SignsItOutAndSaysSo", async () => {
    const user = userOf();
    clerk.user = user;
    renderPage();

    const safari = (await screen.findByText("Safari on iPhone")).closest("li");
    await userEvent.click(
      within(safari as HTMLElement).getByRole("button", { name: "Sign out" }),
    );

    expect(user.sessions[0]?.revoke).toHaveBeenCalledOnce();
    await expectVisibleToast("Signed out of Safari on iPhone");
    // Clerk's list of sessions is cached: the row leaves the page without a new read.
    expect(
      within(screen.getByRole("region", { name: "Sessions" })).queryByText(
        "Safari on iPhone",
      ),
    ).toBeNull();
    expect(user.getSessions).toHaveBeenCalledOnce();
  });

  it("AccountSecurity_SignOutOthers_AsksThenSignsOutEveryOtherSession", async () => {
    const user = userOf();
    clerk.user = user;
    renderPage();

    await userEvent.click(
      await screen.findByRole("button", {
        name: "Sign out of all other sessions",
      }),
    );
    const box = await screen.findByRole("alertdialog", {
      name: "Sign out of 2 other sessions?",
    });
    const dialog = within(box);
    expect(box).toHaveTextContent(
      "Safari on iPhone and Firefox on Windows sign out at once.",
    );
    await userEvent.click(dialog.getByRole("button", { name: "Sign out" }));

    expect(user.sessions.map((each) => each.revoke.mock.calls.length)).toEqual([
      1, 0, 1,
    ]);
    await expectVisibleToast("Signed out of 2 other sessions");
  });

  it("AccountSecurity_SignOutOfFiveOthers_NamesThreeThenTheRest", async () => {
    clerk.user = userOf({
      sessions: [
        sessionOf("sess_chrome", "Chrome", "Macintosh", "Istanbul", 0),
        ...["Safari", "Firefox", "Edge", "Opera", "Brave"].map(
          (browser, index) =>
            sessionOf(
              `sess_${browser}`,
              browser,
              "Windows",
              "Berlin",
              index + 1,
            ),
        ),
      ],
    });
    renderPage();

    await userEvent.click(
      await screen.findByRole("button", {
        name: "Sign out of all other sessions",
      }),
    );
    const dialog = await screen.findByRole("alertdialog", {
      name: "Sign out of 5 other sessions?",
    });

    expect(dialog).toHaveTextContent(
      "Safari on Windows, Firefox on Windows, Edge on Windows and 2 more sign out at once.",
    );
    expect(dialog.querySelectorAll("bdi")).toHaveLength(4);
  });

  it("AccountSecurity_PageAndItsDialogs_HaveNoAccessibilityViolation", async () => {
    clerk.user = userOf();
    renderPage();
    await screen.findByText("Safari on iPhone");
    expect(await violations()).toEqual([]);

    await userEvent.click(screen.getByRole("button", { name: "Disconnect" }));
    // An open dialog is checked on its own: the page behind it is hidden from assistive technology then.
    expect(
      await violations(
        await screen.findByRole("alertdialog", { name: "Disconnect Google?" }),
      ),
    ).toEqual([]);
    await userEvent.click(screen.getByRole("button", { name: "Cancel" }));

    await userEvent.click(
      screen.getByRole("button", { name: "Sign out of all other sessions" }),
    );
    expect(
      await violations(
        await screen.findByRole("alertdialog", {
          name: "Sign out of 2 other sessions?",
        }),
      ),
    ).toEqual([]);
  });

  it("AccountSecurity_OnlyThisDevice_SaysSoWithoutSignOutButtons", async () => {
    clerk.user = userOf({
      sessions: [
        sessionOf("sess_chrome", "Chrome", "Macintosh", "Istanbul", 0),
      ],
    });
    renderPage();

    expect(
      await screen.findByText("You’re signed in only on this device."),
    ).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Sign out" })).toBeNull();
  });

  it("AccountSecurity_SessionsFail_OffersToTryAgain", async () => {
    const user = userOf({
      getSessions: async () => {
        throw new Error("offline");
      },
    });
    clerk.user = user;
    renderPage();

    expect(
      await screen.findByText(
        "Couldn’t load your sessions. Check your connection and try again.",
      ),
    ).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Try again" }));
    expect(user.getSessions).toHaveBeenCalledTimes(2);
  });

  it("AccountSecurity_ClerkAsksForACheck_ShowsConfirmItsYou", async () => {
    clerk.user = userOf();
    clerk.session = {
      id: "sess_chrome",
      startVerification: vi.fn(async () => ({
        status: "needs_first_factor",
        supportedFirstFactors: [
          {
            strategy: "email_code",
            emailAddressId: "idn_ayse",
            safeIdentifier: "ayse@northwind.example",
          },
        ],
      })),
      prepareFirstFactorVerification: vi.fn(async () => ({})),
    };
    clerk.reverify = (asked) =>
      (
        asked as {
          onNeedsReverification: (properties: unknown) => void;
        }
      ).onNeedsReverification({
        level: "first_factor",
        complete: vi.fn(),
        cancel: vi.fn(),
      });
    renderPage();

    const safari = (await screen.findByText("Safari on iPhone")).closest("li");
    await userEvent.click(
      within(safari as HTMLElement).getByRole("button", { name: "Sign out" }),
    );

    expect(
      await screen.findByRole("alertdialog", { name: "Confirm it’s you" }),
    ).toBeInTheDocument();
  });

  it("AccountSecurity_SetUpTwoStep_ShowsTheKeyThenTheCodesAndSaysItIsOn", async () => {
    const user = userOf();
    clerk.user = user;
    renderPage();

    await userEvent.click(screen.getByRole("button", { name: "Set up" }));
    const setUp = within(
      await screen.findByRole("alertdialog", {
        name: "Set up two-step verification",
      }),
    );
    expect(
      await setUp.findByDisplayValue("JBSW Y3DP EHPK 3PXP"),
    ).toBeInTheDocument();
    await userEvent.type(
      setUp.getByRole("textbox", {
        name: "Enter the 6-digit code the app shows.",
      }),
      "123456",
    );
    await userEvent.click(setUp.getByRole("button", { name: "Turn on" }));

    expect(user.verifyTOTP).toHaveBeenCalledWith({ code: "123456" });
    const codes = within(
      await screen.findByRole("alertdialog", {
        name: "Save your backup codes",
      }),
    );
    expect(
      within(codes.getByRole("list", { name: "Backup codes" }))
        .getAllByRole("listitem")
        .map((item) => item.textContent),
    ).toEqual(tenCodes);
    await userEvent.click(codes.getByRole("button", { name: "I saved them" }));
    await expectVisibleToast("Two-step verification is on");
  });

  it("AccountSecurity_NewBackupCodes_ShowsTheNewCodes", async () => {
    const user = { ...userOf(), totpEnabled: true };
    clerk.user = user;
    renderPage();

    await userEvent.click(
      screen.getByRole("button", { name: "New backup codes" }),
    );

    expect(user.createBackupCode).toHaveBeenCalledOnce();
    const codes = within(
      await screen.findByRole("alertdialog", {
        name: "Save your backup codes",
      }),
    );
    expect(codes.getByText("p8yf-3kre")).toBeInTheDocument();
  });

  it("AccountSecurity_TurnOffTwoStep_ChecksACodeThenTurnsItOff", async () => {
    const user = { ...userOf(), totpEnabled: true };
    clerk.user = user;
    clerk.session = {
      id: "sess_chrome",
      startVerification: vi.fn(async () => ({ status: "needs_second_factor" })),
      attemptSecondFactorVerification: vi.fn(async () => ({
        status: "complete",
      })),
      getToken: vi.fn(async () => "token"),
    };
    renderPage();

    await userEvent.click(screen.getByRole("button", { name: "Turn off" }));
    const dialog = within(
      await screen.findByRole("alertdialog", {
        name: "Turn off two-step verification?",
      }),
    );
    await userEvent.type(
      dialog.getByRole("textbox", { name: "Code" }),
      "654321",
    );
    await userEvent.click(dialog.getByRole("button", { name: "Turn off" }));

    expect(user.disableTOTP).toHaveBeenCalledOnce();
    await expectVisibleToast("Two-step verification is off");
  });

  it("AccountSecurity_SetUpCancelledAtTheCheck_ClosesAndSaysNothing", async () => {
    clerk.user = userOf();
    clerk.session = {
      id: "sess_chrome",
      startVerification: vi.fn(async () => ({
        status: "needs_first_factor",
        supportedFirstFactors: [
          {
            strategy: "email_code",
            emailAddressId: "idn_ayse",
            safeIdentifier: "ayse@northwind.example",
          },
        ],
      })),
      prepareFirstFactorVerification: vi.fn(async () => ({})),
    };
    // Clerk rejects the change with this code when the person cancels the check.
    clerk.reverify = (asked) => {
      const { onNeedsReverification, reject } = asked as {
        onNeedsReverification: (properties: unknown) => void;
        reject: (reason: unknown) => void;
      };
      onNeedsReverification({
        level: "first_factor",
        complete: vi.fn(),
        cancel: () => reject({ code: "reverification_cancelled" }),
      });
    };
    const onUnexpectedError = vi.fn();
    renderPage({ onUnexpectedError });

    await userEvent.click(screen.getByRole("button", { name: "Set up" }));
    const check = within(
      await screen.findByRole("alertdialog", { name: "Confirm it’s you" }),
    );
    await userEvent.click(check.getByRole("button", { name: "Cancel" }));

    expect(
      screen.queryByRole("alertdialog", {
        name: "Set up two-step verification",
      }),
    ).toBeNull();
    expect(screen.queryByText("That did not work")).toBeNull();
    expect(onUnexpectedError).not.toHaveBeenCalled();
  });

  // The check opens inside the question's dialog, so the question does not hide it from the person (Base UI's nested dialogs).
  it("AccountSecurity_DisconnectAskedForACheck_ShowsConfirmOverTheQuestion", async () => {
    clerk.user = userOf();
    clerk.session = {
      id: "sess_chrome",
      startVerification: vi.fn(async () => ({ status: "needs_second_factor" })),
    };
    clerk.reverify = (asked) =>
      (
        asked as { onNeedsReverification: (properties: unknown) => void }
      ).onNeedsReverification({
        level: "second_factor",
        complete: vi.fn(),
        cancel: vi.fn(),
      });
    renderPage();

    await userEvent.click(screen.getByRole("button", { name: "Disconnect" }));
    const question = within(
      await screen.findByRole("alertdialog", { name: "Disconnect Google?" }),
    );
    await userEvent.click(question.getByRole("button", { name: "Disconnect" }));

    expect(
      await screen.findByRole("alertdialog", { name: "Confirm it’s you" }),
    ).toBeInTheDocument();
  });

  it("AccountSecurity_NewBackupCodesFail_SaysItDidNotWork", async () => {
    const user = {
      ...userOf(),
      totpEnabled: true,
      createBackupCode: vi.fn(async () => {
        throw new Error("Clerk is down");
      }),
    };
    clerk.user = user;
    renderPage();

    await userEvent.click(
      screen.getByRole("button", { name: "New backup codes" }),
    );

    await expectVisibleToast("That did not work");
  });

  it("AccountSecurity_BackupCodesClosedWithEscape_ReadsThePersonAgainAndSaysItIsOn", async () => {
    const user = userOf();
    clerk.user = user;
    renderPage();

    await userEvent.click(screen.getByRole("button", { name: "Set up" }));
    const setUp = within(
      await screen.findByRole("alertdialog", {
        name: "Set up two-step verification",
      }),
    );
    await setUp.findByDisplayValue("JBSW Y3DP EHPK 3PXP");
    await userEvent.type(
      setUp.getByRole("textbox", {
        name: "Enter the 6-digit code the app shows.",
      }),
      "123456",
    );
    await userEvent.click(setUp.getByRole("button", { name: "Turn on" }));
    await screen.findByRole("alertdialog", { name: "Save your backup codes" });
    await userEvent.keyboard("{Escape}");

    expect(user.reload).toHaveBeenCalled();
    await expectVisibleToast("Two-step verification is on");
  });

  it("AccountSecurity_TwoStepDialogs_HaveNoAccessibilityViolation", async () => {
    clerk.user = userOf();
    renderPage();

    await userEvent.click(screen.getByRole("button", { name: "Set up" }));
    const setUp = await screen.findByRole("alertdialog", {
      name: "Set up two-step verification",
    });
    await within(setUp).findByDisplayValue("JBSW Y3DP EHPK 3PXP");
    expect(await violations(setUp)).toEqual([]);
    await userEvent.type(
      within(setUp).getByRole("textbox", {
        name: "Enter the 6-digit code the app shows.",
      }),
      "123456",
    );
    await userEvent.click(
      within(setUp).getByRole("button", { name: "Turn on" }),
    );

    expect(
      await violations(
        await screen.findByRole("alertdialog", {
          name: "Save your backup codes",
        }),
      ),
    ).toEqual([]);
  });

  it("AccountSecurity_TurnOffDialog_HasNoAccessibilityViolation", async () => {
    clerk.user = { ...userOf(), totpEnabled: true };
    clerk.session = {
      id: "sess_chrome",
      startVerification: vi.fn(async () => ({ status: "needs_second_factor" })),
    };
    renderPage();

    await userEvent.click(screen.getByRole("button", { name: "Turn off" }));

    expect(
      await violations(
        await screen.findByRole("alertdialog", {
          name: "Turn off two-step verification?",
        }),
      ),
    ).toEqual([]);
  });

  it("AccountSecurity_TwoStepRequired_SaysOnWithoutActions", () => {
    clerk.user = { ...userOf(), twoFactorEnabled: true, totpEnabled: true };
    renderPage({
      twoStep: { text: "Required on the platform.", required: true },
    });

    const twoStep = within(
      screen.getByRole("region", { name: "Two-step verification" }),
    );
    expect(twoStep.getByText("On")).toBeInTheDocument();
    expect(twoStep.queryByRole("button")).toBeNull();
  });
});
