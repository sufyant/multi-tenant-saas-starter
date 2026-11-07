import { ClerkAPIResponseError } from "@clerk/nextjs/errors";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import {
  afterEach,
  beforeEach,
  describe,
  expect,
  it,
  type Mock,
  vi,
} from "vitest";
import ar from "../../../messages/ar.json";
import en from "../../../messages/en.json";
import { SignIn } from "./sign-in";

// Clerk is not ours, so the test fakes its hook (Khorikov, Chapter 9): a SignInFuture whose methods answer as the Frontend API
// does, and whose status changes in place, as Clerk's does.
const clerk = vi.hoisted(() => ({
  signIn: {} as unknown,
  fetchStatus: "idle",
}));
vi.mock("@clerk/nextjs", () => ({
  useSignIn: () => ({ signIn: clerk.signIn, fetchStatus: clerk.fetchStatus }),
}));

const ok = { error: null };
const refused = (code: string) => ({
  error: new ClerkAPIResponseError("refused", {
    data: [{ code, message: "Clerk's English", long_message: "" }],
    status: 422,
  }),
});

// A hash keeps jsdom on the page: it cannot load another document.
const decorateUrl = vi.fn((url: string) => `#${url}`);
const finalize = vi.fn(
  async ({ navigate }: { navigate: (args: unknown) => void }) => {
    navigate({ session: {}, decorateUrl });
    return ok;
  },
);

type Answer = Promise<{ error: unknown }>;
type FakeSignIn = {
  status: string;
  emailCode: {
    sendCode: Mock<(params?: unknown) => Answer>;
    verifyCode: Mock<(params: unknown) => Answer>;
  };
  mfa: {
    verifyTOTP: Mock<(params: unknown) => Answer>;
    verifyBackupCode: Mock<(params: unknown) => Answer>;
  };
  sso: Mock<(params: unknown) => Answer>;
  finalize: typeof finalize;
  reset: Mock<() => Answer>;
};

function fakeSignIn(status = "needs_identifier"): FakeSignIn {
  const complete = async () => {
    signIn.status = "complete";
    return ok;
  };
  const signIn: FakeSignIn = {
    status,
    emailCode: {
      sendCode: vi.fn(async () => ok),
      verifyCode: vi.fn(complete),
    },
    mfa: { verifyTOTP: vi.fn(complete), verifyBackupCode: vi.fn(complete) },
    sso: vi.fn(async () => ok),
    finalize,
    reset: vi.fn(async () => ok),
  };
  clerk.signIn = signIn;
  return signIn;
}

const languages = { en, ar };

function renderSignIn(
  locale: keyof typeof languages = "en",
  onUnexpectedError?: (error: Error) => void,
) {
  return render(
    <NextIntlClientProvider
      locale={locale}
      messages={languages[locale]}
      timeZone="Europe/Dublin"
    >
      <SignIn
        title="Sign in to Starter"
        text="Use your work email. We send you a 6-digit code. No password."
        secondFactorTitle="Two-step verification"
        secondFactorText="Open your authenticator app and enter the 6-digit code for this account."
        brand="Starter"
        onLanguageChange={async () => {}}
        privacyUrl="https://starter.example/privacy"
        onUnexpectedError={onUnexpectedError}
      />
    </NextIntlClientProvider>,
  );
}

async function sendCodeTo(email: string) {
  const user = userEvent.setup();
  await user.type(screen.getByRole("textbox", { name: "Email" }), email);
  await user.click(screen.getByRole("button", { name: "Continue" }));
  await screen.findByRole("heading", { name: "Check your email" });
  return user;
}

async function enterCode(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByRole("textbox", { name: "Code" }), "424242");
  await user.click(screen.getByRole("button", { name: "Sign in" }));
}

beforeEach(() => {
  window.history.replaceState({}, "", "/sign-in");
  vi.spyOn(console, "warn").mockImplementation(() => {});
});
afterEach(() => {
  vi.clearAllMocks();
});

describe("SignIn", () => {
  it("SignIn_FirstStep_LinksThePrivacyPolicyInTheFooter", () => {
    renderSignIn();

    expect(screen.getByRole("link", { name: "Privacy" })).toHaveAttribute(
      "href",
      "https://starter.example/privacy",
    );
  });

  it("SignIn_SessionEnded_SaysSoAboveTheFirstStep", async () => {
    window.history.replaceState(
      null,
      "",
      `/sign-in?reason=session-ended&redirect_url=${encodeURIComponent("/northwind/settings/members")}`,
    );
    renderSignIn();

    expect(await screen.findByText("Your session ended")).toBeInTheDocument();
    window.history.replaceState(null, "", "/");
  });

  it("SignIn_FirstStep_ShowsTheProvidersThenTheEmailCode", () => {
    fakeSignIn();

    renderSignIn();

    expect(
      screen.getByRole("heading", { level: 1, name: "Sign in to Starter" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        "Use your work email. We send you a 6-digit code. No password.",
      ),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Continue with Google" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Continue with Microsoft" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "Email" })).toHaveAttribute(
      "dir",
      "ltr",
    );
    expect(screen.queryByLabelText(/password/i)).not.toBeInTheDocument();
  });

  it("SignIn_EmptyEmail_AsksForTheAddressAndCallsNothing", async () => {
    const signIn = fakeSignIn();
    const user = userEvent.setup();
    renderSignIn();

    await user.click(screen.getByRole("button", { name: "Continue" }));

    expect(
      screen.getByRole("textbox", { name: "Email" }),
    ).toHaveAccessibleDescription("Enter your email address.");
    expect(signIn.emailCode.sendCode).not.toHaveBeenCalled();
  });

  // Frame 1.02: an address without a domain's dot is refused before Clerk is called.
  it("SignIn_AddressWithoutADomain_SaysHowToWriteItAndCallsNothing", async () => {
    const signIn = fakeSignIn();
    const user = userEvent.setup();
    renderSignIn();

    await user.type(
      screen.getByRole("textbox", { name: "Email" }),
      "ayse@northwind",
    );
    await user.click(screen.getByRole("button", { name: "Continue" }));

    expect(
      screen.getByRole("textbox", { name: "Email" }),
    ).toHaveAccessibleDescription(
      "Enter a full email address, such as name@company.com.",
    );
    expect(signIn.emailCode.sendCode).not.toHaveBeenCalled();
  });

  // Frame 1.03: while the code goes out, the address and the button wait, and the button says why.
  it("SignIn_Sending_DisablesTheAddressAndSaysTheCodeIsGoingOut", () => {
    fakeSignIn();
    clerk.fetchStatus = "fetching";

    renderSignIn();

    expect(screen.getByRole("textbox", { name: "Email" })).toBeDisabled();
    expect(
      screen.getByRole("button", { name: "Sending the code…" }),
    ).toBeDisabled();
    clerk.fetchStatus = "idle";
  });

  // Frame 1.04: a call that gets no answer from Clerk is a network failure, shown as a banner that keeps the address.
  it("SignIn_CodeCouldNotBeSent_ShowsTheBannerAndKeepsTheAddress", async () => {
    const signIn = fakeSignIn();
    signIn.emailCode.sendCode.mockResolvedValueOnce({
      error: new Error("Failed to fetch"),
    });
    const onUnexpectedError = vi.fn();
    const user = userEvent.setup();
    renderSignIn("en", onUnexpectedError);

    await user.type(
      screen.getByRole("textbox", { name: "Email" }),
      "ayse@northwind.example",
    );
    await user.click(screen.getByRole("button", { name: "Continue" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "We could not send the code",
    );
    expect(screen.getByRole("textbox", { name: "Email" })).toHaveValue(
      "ayse@northwind.example",
    );
    expect(onUnexpectedError).not.toHaveBeenCalled();
  });

  it("SignIn_CodeSent_ShowsTheCodeStepWithTheAddress", async () => {
    const signIn = fakeSignIn();
    renderSignIn();

    await sendCodeTo("ali@northwind.io");

    expect(signIn.emailCode.sendCode).toHaveBeenCalledWith({
      emailAddress: "ali@northwind.io",
    });
    expect(screen.getByText("ali@northwind.io")).toHaveAttribute("dir", "ltr");
  });

  it("SignIn_AddressInAWrongFormat_SaysHowToWriteIt", async () => {
    const signIn = fakeSignIn();
    signIn.emailCode.sendCode.mockResolvedValueOnce(
      refused("form_param_format_invalid"),
    );
    const user = userEvent.setup();
    renderSignIn();

    await user.type(
      screen.getByRole("textbox", { name: "Email" }),
      "ali@northwind.io",
    );
    await user.click(screen.getByRole("button", { name: "Continue" }));

    expect(
      await screen.findByText(
        "Enter a full email address, such as name@company.com.",
      ),
    ).toBeInTheDocument();
  });

  it("SignIn_AddressWithoutAnAccount_ShowsTheCodeStepLikeAnyOther", async () => {
    // Strict enumeration protection: Clerk answers an unknown address as a known one, and the code then fails.
    const signIn = fakeSignIn();
    signIn.emailCode.verifyCode.mockResolvedValueOnce(
      refused("form_code_incorrect"),
    );
    renderSignIn();
    const user = await sendCodeTo("nobody@northwind.io");

    await enterCode(user);

    expect(
      await screen.findByText(
        "This code is wrong or expired. Check it, or send a new one.",
      ),
    ).toBeInTheDocument();
  });

  it("SignIn_ExpiredCode_SaysToSendANewOne", async () => {
    const signIn = fakeSignIn();
    signIn.emailCode.verifyCode.mockResolvedValueOnce(
      refused("verification_expired"),
    );
    renderSignIn();
    const user = await sendCodeTo("ali@northwind.io");

    await enterCode(user);

    expect(
      await screen.findByText("This code has expired. Send a new one."),
    ).toBeInTheDocument();
  });

  it("SignIn_TooManyAttempts_SaysToStartAgain", async () => {
    const signIn = fakeSignIn();
    signIn.emailCode.verifyCode.mockResolvedValueOnce(
      refused("verification_failed"),
    );
    renderSignIn();
    const user = await sendCodeTo("ali@northwind.io");

    await enterCode(user);

    expect(
      await screen.findByText(
        "Too many tries. Send a new code and start again.",
      ),
    ).toBeInTheDocument();
  });

  it("SignIn_UnknownErrorCode_ShowsTheGenericMessageAndLogsTheCodeOnce", async () => {
    const signIn = fakeSignIn();
    signIn.emailCode.verifyCode.mockResolvedValue(refused("brand_new_code"));
    renderSignIn();
    const user = await sendCodeTo("ali@northwind.io");

    await enterCode(user);
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(
      await screen.findByText("Sign-in did not work. Try again."),
    ).toBeInTheDocument();
    expect(console.warn).toHaveBeenCalledTimes(1);
    expect(screen.queryByText("Clerk's English")).not.toBeInTheDocument();
  });

  // The app's error tracking gets the code once, and never the address or the code the person typed (F41, F55).
  it("SignIn_UnknownErrorCode_GivesTheAppOnlyTheClerkCode", async () => {
    const signIn = fakeSignIn();
    signIn.emailCode.verifyCode.mockResolvedValue(refused("code_of_next_year"));
    const onUnexpectedError = vi.fn();
    renderSignIn("en", onUnexpectedError);
    const user = await sendCodeTo("ali@northwind.io");

    await enterCode(user);
    await screen.findByText("Sign-in did not work. Try again.");

    expect(onUnexpectedError).toHaveBeenCalledTimes(1);
    expect(onUnexpectedError.mock.calls[0]?.[0].message).toBe(
      'Sign-in: no message for the Clerk error code "code_of_next_year".',
    );
  });

  it("SignIn_RightCode_FinalizesToTheRedirectOfThisApp", async () => {
    window.history.replaceState(
      {},
      "",
      "/sign-in?redirect_url=%2Fnorthwind%2Fsettings%3Ftab%3Done",
    );
    fakeSignIn();
    renderSignIn();
    const user = await sendCodeTo("ali@northwind.io");

    await enterCode(user);

    expect(finalize).toHaveBeenCalledTimes(1);
    expect(decorateUrl).toHaveBeenCalledWith("/northwind/settings?tab=one");
  });

  it("SignIn_RedirectToAnotherOrigin_FinalizesToHome", async () => {
    window.history.replaceState(
      {},
      "",
      "/sign-in?redirect_url=https%3A%2F%2Fevil.example%2Fsteal",
    );
    fakeSignIn();
    renderSignIn();
    const user = await sendCodeTo("ali@northwind.io");

    await enterCode(user);

    expect(decorateUrl).toHaveBeenCalledWith("/");
  });

  it("SignIn_AccountWithASecondFactor_AsksForTheAuthenticatorCode", async () => {
    const signIn = fakeSignIn();
    signIn.emailCode.verifyCode.mockImplementationOnce(async () => {
      signIn.status = "needs_second_factor";
      return ok;
    });
    renderSignIn();
    const user = await sendCodeTo("admin@starter.example");

    await enterCode(user);

    expect(
      await screen.findByRole("heading", {
        name: "Two-step verification",
      }),
    ).toBeInTheDocument();
    expect(finalize).not.toHaveBeenCalled();
  });

  it("SignIn_RightTotp_VerifiesItAndFinalizes", async () => {
    const signIn = fakeSignIn("needs_second_factor");
    const user = userEvent.setup();
    renderSignIn();

    await user.type(
      await screen.findByRole("textbox", { name: "Code" }),
      "123456",
    );
    await user.click(screen.getByRole("button", { name: "Continue" }));

    expect(signIn.mfa.verifyTOTP).toHaveBeenCalledWith({ code: "123456" });
    expect(finalize).toHaveBeenCalledTimes(1);
  });

  it("SignIn_WrongTotp_SaysToWaitForANewCode", async () => {
    const signIn = fakeSignIn("needs_second_factor");
    signIn.mfa.verifyTOTP.mockResolvedValueOnce(refused("form_code_incorrect"));
    const user = userEvent.setup();
    renderSignIn();

    await user.type(
      await screen.findByRole("textbox", { name: "Code" }),
      "123456",
    );
    await user.click(screen.getByRole("button", { name: "Continue" }));

    expect(
      await screen.findByText(
        "That code does not match. Wait for the app to show a new one and try again.",
      ),
    ).toBeInTheDocument();
  });

  it("SignIn_LockedAccount_SaysToWaitOrUseABackupCode", async () => {
    const signIn = fakeSignIn("needs_second_factor");
    signIn.mfa.verifyTOTP.mockResolvedValueOnce(refused("user_locked"));
    const user = userEvent.setup();
    renderSignIn();

    await user.type(
      await screen.findByRole("textbox", { name: "Code" }),
      "123456",
    );
    await user.click(screen.getByRole("button", { name: "Continue" }));

    expect(
      await screen.findByText(
        "Too many wrong codes. Try again later, or use a backup code.",
      ),
    ).toBeInTheDocument();
  });

  it("SignIn_BackupCode_VerifiesTheBackupCode", async () => {
    const signIn = fakeSignIn("needs_second_factor");
    const user = userEvent.setup();
    renderSignIn();

    await user.click(
      await screen.findByRole("button", { name: "Use a backup code instead" }),
    );
    expect(
      screen.getByRole("heading", { name: "Use a backup code" }),
    ).toBeInTheDocument();
    await user.type(
      screen.getByRole("textbox", { name: "Backup code" }),
      "a1b2c3d4",
    );
    await user.click(screen.getByRole("button", { name: "Continue" }));

    expect(signIn.mfa.verifyBackupCode).toHaveBeenCalledWith({
      code: "a1b2c3d4",
    });
    expect(finalize).toHaveBeenCalledTimes(1);
  });

  it("SignIn_Google_StartsTheRedirectWithTheCallbackPage", async () => {
    window.history.replaceState({}, "", "/sign-in?redirect_url=%2Fnorthwind");
    const signIn = fakeSignIn();
    const user = userEvent.setup();
    renderSignIn();

    await user.click(
      screen.getByRole("button", { name: "Continue with Google" }),
    );

    expect(signIn.sso).toHaveBeenCalledWith({
      strategy: "oauth_google",
      redirectUrl: "/northwind",
      redirectCallbackUrl: "/sso-callback?redirect_url=%2Fnorthwind",
    });
  });

  it("SignIn_Microsoft_StartsItsRedirect", async () => {
    const signIn = fakeSignIn();
    const user = userEvent.setup();
    renderSignIn();

    await user.click(
      screen.getByRole("button", { name: "Continue with Microsoft" }),
    );

    expect(signIn.sso).toHaveBeenCalledWith(
      expect.objectContaining({ strategy: "oauth_microsoft" }),
    );
  });

  it("SignIn_SendANewCode_SendsAgainAndSaysSo", async () => {
    const signIn = fakeSignIn();
    renderSignIn();
    const user = await sendCodeTo("ali@northwind.io");

    await user.click(screen.getByRole("button", { name: "Send a new code" }));

    expect(signIn.emailCode.sendCode).toHaveBeenLastCalledWith();
    expect(await screen.findByRole("status")).toHaveTextContent(
      "We sent a new code. It replaces the old one.",
    );
  });

  it("SignIn_UseAnotherEmail_ResetsAndGoesBackToTheFirstStep", async () => {
    const signIn = fakeSignIn();
    renderSignIn();
    const user = await sendCodeTo("ali@northwind.io");

    await user.click(screen.getByRole("button", { name: "Use another email" }));

    expect(signIn.reset).toHaveBeenCalledTimes(1);
    expect(
      await screen.findByRole("heading", { name: "Sign in to Starter" }),
    ).toBeInTheDocument();
  });

  it("SignIn_SsoThatCouldNotFinish_ShowsTheNeutralMessage", async () => {
    window.history.replaceState({}, "", "/sign-in?sso=failed");
    fakeSignIn();

    renderSignIn();

    expect(
      await screen.findByText(
        "We could not sign you in with this account. Use the email address that received your invitation.",
      ),
    ).toBeInTheDocument();
  });

  it("SignIn_Arabic_SpeaksArabicAndKeepsTheCodeLeftToRight", async () => {
    const signIn = fakeSignIn("needs_second_factor");
    renderSignIn("ar");

    expect(
      await screen.findByRole("heading", { name: "Two-step verification" }),
    ).toBeInTheDocument();
    expect(
      document.querySelector("[data-slot='input-otp-group']"),
    ).toHaveAttribute("dir", "ltr");
    expect(signIn.mfa.verifyTOTP).not.toHaveBeenCalled();
  });

  // Frame 1.08 draws only "Use your app instead": the way out comes back with the app's code.
  it("SignIn_BackupCode_OffersOnlyTheWayBackToTheApp", async () => {
    fakeSignIn("needs_second_factor");
    const user = userEvent.setup();
    renderSignIn();

    await user.click(
      await screen.findByRole("button", { name: "Use a backup code instead" }),
    );

    expect(
      screen.getByRole("button", { name: "Use your app instead" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Use another email" }),
    ).not.toBeInTheDocument();
  });
});

