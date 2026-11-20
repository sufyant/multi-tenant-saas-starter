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
import en from "../../../messages/en.json";
import { SignUp } from "./sign-up";

// Clerk is not ours, so the test fakes its hook (Khorikov, Chapter 9): a SignUpFuture whose methods answer as the Frontend API
// does, and whose status changes in place, as Clerk's does.
const clerk = vi.hoisted(() => ({
  signUp: {} as unknown,
  fetchStatus: "idle",
}));
vi.mock("@clerk/nextjs", () => ({
  useSignUp: () => ({ signUp: clerk.signUp, fetchStatus: clerk.fetchStatus }),
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

type Answer = Promise<{ error: unknown }>;
type FakeSignUp = {
  status: string;
  create: Mock<(params: unknown) => Answer>;
  verifications: {
    sendEmailCode: Mock<() => Answer>;
    verifyEmailCode: Mock<(params: unknown) => Answer>;
  };
  sso: Mock<(params: unknown) => Answer>;
  finalize: Mock<
    (params: { navigate: (args: unknown) => void }) => Promise<{
      error: null;
    }>
  >;
  reset: Mock<() => Answer>;
};

function fakeSignUp(): FakeSignUp {
  const signUp: FakeSignUp = {
    status: "missing_requirements",
    create: vi.fn(async () => ok),
    verifications: {
      sendEmailCode: vi.fn(async () => ok),
      verifyEmailCode: vi.fn(async () => {
        signUp.status = "complete";
        return ok;
      }),
    },
    sso: vi.fn(async () => ok),
    finalize: vi.fn(async ({ navigate }) => {
      navigate({ session: {}, decorateUrl });
      return ok;
    }),
    reset: vi.fn(async () => ok),
  };
  clerk.signUp = signUp;
  return signUp;
}

function renderSignUp() {
  return render(
    <NextIntlClientProvider locale="en" messages={en} timeZone="Europe/Dublin">
      <SignUp
        title="Create an account"
        text="Use your work email. We send you a 6-digit code to confirm it."
        brand="Starter"
        onLanguageChange={async () => {}}
        privacyUrl="https://starter.example/privacy"
        signInHref="/sign-in"
        after="/"
      />
    </NextIntlClientProvider>,
  );
}

async function sendCodeTo(email: string) {
  const user = userEvent.setup();
  await user.type(screen.getByRole("textbox", { name: "Email" }), email);
  await user.click(screen.getByRole("button", { name: "Continue" }));
  return user;
}

beforeEach(() => {
  window.history.replaceState({}, "", "/sign-up");
  vi.spyOn(console, "warn").mockImplementation(() => {});
});
afterEach(() => {
  vi.clearAllMocks();
});

describe("SignUp", () => {
  it("SignUp_FirstStep_ShowsTheProvidersTheEmailAndTheWayToSignIn", () => {
    fakeSignUp();

    const { container } = renderSignUp();

    expect(
      screen.getByRole("heading", { level: 1, name: "Create an account" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Continue with Google" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Sign in" })).toHaveAttribute(
      "href",
      "/sign-in",
    );
    expect(container.querySelector("#clerk-captcha")).not.toBeNull();
  });

  it("SignUp_Address_CreatesTheSignUpAndSendsTheCode", async () => {
    const signUp = fakeSignUp();
    renderSignUp();

    await sendCodeTo("elif@kestrel.example");

    expect(signUp.create).toHaveBeenCalledWith({
      emailAddress: "elif@kestrel.example",
    });
    expect(signUp.verifications.sendEmailCode).toHaveBeenCalled();
    expect(
      await screen.findByRole("heading", { name: "Check your email" }),
    ).toBeInTheDocument();
    expect(screen.getByText("elif@kestrel.example")).toBeInTheDocument();
  });

  it("SignUp_AddressRefused_SaysSoWithoutTellingWhetherItHasAnAccount", async () => {
    const signUp = fakeSignUp();
    signUp.create.mockResolvedValueOnce(refused("form_identifier_exists"));
    renderSignUp();

    await sendCodeTo("ayse@northwind.example");

    expect(
      await screen.findByText(
        "We could not create an account with this address. If you have one, sign in.",
      ),
    ).toBeInTheDocument();
    expect(signUp.verifications.sendEmailCode).not.toHaveBeenCalled();
  });

  it("SignUp_RightCode_VerifiesItAndFinalizesToThePageAfter", async () => {
    const signUp = fakeSignUp();
    renderSignUp();
    const user = await sendCodeTo("elif@kestrel.example");
    await screen.findByRole("heading", { name: "Check your email" });

    await user.type(screen.getByRole("textbox", { name: "Code" }), "424242");
    await user.click(screen.getByRole("button", { name: "Continue" }));

    expect(signUp.verifications.verifyEmailCode).toHaveBeenCalledWith({
      code: "424242",
    });
    expect(signUp.finalize).toHaveBeenCalled();
    expect(decorateUrl).toHaveBeenCalledWith("/");
  });

  it("SignUp_WrongCode_SaysSoAndDoesNotFinalize", async () => {
    const signUp = fakeSignUp();
    signUp.verifications.verifyEmailCode.mockResolvedValueOnce(
      refused("form_code_incorrect"),
    );
    renderSignUp();
    const user = await sendCodeTo("elif@kestrel.example");
    await screen.findByRole("heading", { name: "Check your email" });

    await user.type(screen.getByRole("textbox", { name: "Code" }), "111111");
    await user.click(screen.getByRole("button", { name: "Continue" }));

    expect(
      await screen.findByText(
        "This code is wrong or expired. Check it, or send a new one.",
      ),
    ).toBeInTheDocument();
    expect(signUp.finalize).not.toHaveBeenCalled();
  });

  it("SignUp_Google_StartsClerksSsoWithTheCallbackBackToThePageAfter", async () => {
    const signUp = fakeSignUp();
    renderSignUp();

    await userEvent
      .setup()
      .click(screen.getByRole("button", { name: "Continue with Google" }));

    expect(signUp.sso).toHaveBeenCalledWith({
      strategy: "oauth_google",
      redirectUrl: "/",
      redirectCallbackUrl: "/sso-callback?redirect_url=%2F",
    });
  });

  it("SignUp_UseAnotherEmail_ResetsAndGoesBackToTheFirstStep", async () => {
    const signUp = fakeSignUp();
    renderSignUp();
    const user = await sendCodeTo("elif@kestrel.example");
    await screen.findByRole("heading", { name: "Check your email" });

    await user.click(screen.getByRole("button", { name: "Use another email" }));

    expect(signUp.reset).toHaveBeenCalled();
    expect(
      await screen.findByRole("heading", { name: "Create an account" }),
    ).toBeInTheDocument();
  });

  it("SignUp_CreateBreaks_SaysTheCodeCouldNotGoOutAndKeepsTheAddress", async () => {
    const signUp = fakeSignUp();
    signUp.create.mockRejectedValueOnce(new Error("Failed to fetch"));
    renderSignUp();

    await sendCodeTo("elif@kestrel.example");

    expect(
      await screen.findByText("We could not send the code"),
    ).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "Email" })).toHaveValue(
      "elif@kestrel.example",
    );
  });

  it("SignUp_SignUpClosed_SaysTheSameAsATakenAddress", async () => {
    const signUp = fakeSignUp();
    signUp.create.mockResolvedValueOnce(refused("sign_up_mode_restricted"));
    renderSignUp();

    await sendCodeTo("elif@kestrel.example");

    expect(
      await screen.findByText(
        "We could not create an account with this address. If you have one, sign in.",
      ),
    ).toBeInTheDocument();
  });

  it("SignUp_CodeCouldNotBeSent_StaysOnTheFirstStepWithTheBanner", async () => {
    const signUp = fakeSignUp();
    signUp.verifications.sendEmailCode.mockRejectedValueOnce(
      new Error("Failed to fetch"),
    );
    renderSignUp();

    await sendCodeTo("elif@kestrel.example");

    expect(
      await screen.findByText("We could not send the code"),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("heading", { name: "Check your email" }),
    ).not.toBeInTheDocument();
  });

  it("SignUp_SendANewCode_SendsItAgainAndSaysSo", async () => {
    const signUp = fakeSignUp();
    renderSignUp();
    const user = await sendCodeTo("elif@kestrel.example");
    await screen.findByRole("heading", { name: "Check your email" });

    await user.click(screen.getByRole("button", { name: "Send a new code" }));

    expect(signUp.verifications.sendEmailCode).toHaveBeenCalledTimes(2);
    expect(
      await screen.findByText("We sent a new code. It replaces the old one."),
    ).toBeInTheDocument();
  });

  it("SignUp_CodeRightButSignUpNotComplete_ShowsTheGenericMessageAndDoesNotFinalize", async () => {
    const signUp = fakeSignUp();
    signUp.verifications.verifyEmailCode.mockResolvedValueOnce(ok);
    renderSignUp();
    const user = await sendCodeTo("elif@kestrel.example");
    await screen.findByRole("heading", { name: "Check your email" });

    await user.type(screen.getByRole("textbox", { name: "Code" }), "424242");
    await user.click(screen.getByRole("button", { name: "Continue" }));

    expect(
      await screen.findByText("Sign-in did not work. Try again."),
    ).toBeInTheDocument();
    expect(signUp.finalize).not.toHaveBeenCalled();
  });

  it("SignUp_FinalizeFails_SaysSo", async () => {
    const signUp = fakeSignUp();
    signUp.finalize.mockResolvedValueOnce(
      refused("verification_expired") as never,
    );
    renderSignUp();
    const user = await sendCodeTo("elif@kestrel.example");
    await screen.findByRole("heading", { name: "Check your email" });

    await user.type(screen.getByRole("textbox", { name: "Code" }), "424242");
    await user.click(screen.getByRole("button", { name: "Continue" }));

    expect(
      await screen.findByText("This code has expired. Send a new one."),
    ).toBeInTheDocument();
  });
});
