import { ClerkAPIResponseError } from "@clerk/nextjs/errors";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { renderWithProviders } from "../../../test/render";
import { TwoStepPage } from "./two-step-page";

// Clerk is not ours, so the test fakes its hooks (Khorikov, Chapter 9): a user and a session that answer as Clerk does.
const clerk = vi.hoisted(() => ({
  user: {} as unknown,
  session: {} as unknown,
  signOut: (() => {}) as unknown,
}));
vi.mock("@clerk/nextjs", () => ({
  useUser: () => ({ isLoaded: true, user: clerk.user }),
  useSession: () => ({ session: clerk.session }),
  useClerk: () => ({ signOut: clerk.signOut }),
}));

// The SDK is faked: a Clerk code without a message goes to error tracking, and nothing leaves the test (F41, F55).
const sdk = vi.hoisted(() => ({
  captureException: vi.fn(),
  captureRequestError: vi.fn(),
}));
vi.mock("@sentry/nextjs", () => sdk);

const unknownCode = (code: string) =>
  new ClerkAPIResponseError("refused", {
    data: [{ code, message: "Clerk's English", long_message: "" }],
    status: 422,
  });

const secret = "JBSWY3DPEHPK3PXPK7QN";
const backupCodes = Array.from({ length: 10 }, (_, index) => `code${index}`);

function fakeClerk({
  totpEnabled = false,
  secondFactorAge = -1,
  afterReload = -1,
} = {}) {
  const user = {
    totpEnabled,
    primaryEmailAddress: { emailAddress: "admin@starter.example" },
    createTOTP: vi.fn(async () => ({
      secret,
      uri: `otpauth://totp/Starter:admin?secret=${secret}`,
    })),
    verifyTOTP: vi.fn(
      async (): Promise<{ verified: boolean; backupCodes?: string[] }> => ({
        verified: true,
        backupCodes,
      }),
    ),
    createBackupCode: vi.fn(async () => ({ codes: backupCodes })),
  };
  const session = {
    factorVerificationAge: [0, secondFactorAge],
    reload: vi.fn(async () => ({ factorVerificationAge: [0, afterReload] })),
    getToken: vi.fn(async () => "token"),
    startVerification: vi.fn(async () => ({ status: "needs_second_factor" })),
    attemptSecondFactorVerification: vi.fn(async () => ({
      status: "complete",
    })),
  };
  clerk.user = user;
  clerk.session = session;
  clerk.signOut = vi.fn();
  return { user, session };
}

afterEach(() => {
  vi.clearAllMocks();
});

async function enrol(user: ReturnType<typeof userEvent.setup>) {
  await screen.findByDisplayValue("JBSW Y3DP EHPK 3PXP K7QN");
  await user.type(
    screen.getByRole("textbox", {
      name: "Enter the 6-digit code the app shows",
    }),
    "123456",
  );
  await user.click(
    screen.getByRole("button", { name: "Turn on two-step sign-in" }),
  );
}

describe("TwoStepPage", () => {
  it("TwoStepPage_NoAuthenticatorYet_ShowsTheEnrolment", async () => {
    const { user } = fakeClerk();

    renderWithProviders(<TwoStepPage />);

    expect(
      await screen.findByRole("heading", {
        name: "Set up your authenticator app",
      }),
    ).toBeInTheDocument();
    expect(
      await screen.findByDisplayValue("JBSW Y3DP EHPK 3PXP K7QN"),
    ).toHaveAttribute("dir", "ltr");
    expect(
      screen.getByRole("img", { name: "QR code for your authenticator app" }),
    ).toBeInTheDocument();
    expect(screen.getByText("admin@starter.example")).toBeInTheDocument();
    expect(user.createTOTP).toHaveBeenCalledTimes(1);
  });

  it("TwoStepPage_RightCode_TurnsTheFactorOnAndShowsTenBackupCodes", async () => {
    const { user } = fakeClerk();
    const person = userEvent.setup();
    renderWithProviders(<TwoStepPage />);

    await enrol(person);

    expect(user.verifyTOTP).toHaveBeenCalledWith({ code: "123456" });
    const list = await screen.findByRole("list", { name: "Backup codes" });
    expect(list.querySelectorAll("li")).toHaveLength(10);
    expect(user.createBackupCode).not.toHaveBeenCalled();
  });

  it("TwoStepPage_VerificationWithoutBackupCodes_AsksClerkForThem", async () => {
    const { user } = fakeClerk();
    user.verifyTOTP.mockResolvedValueOnce({ verified: true });
    const person = userEvent.setup();
    renderWithProviders(<TwoStepPage />);

    await enrol(person);

    expect(
      await screen.findByRole("list", { name: "Backup codes" }),
    ).toBeInTheDocument();
    expect(user.createBackupCode).toHaveBeenCalledTimes(1);
  });

  it("TwoStepPage_WrongCodeAtEnrolment_SaysToWaitForANewOne", async () => {
    const { user } = fakeClerk();
    user.verifyTOTP.mockRejectedValueOnce(
      new ClerkAPIResponseError("refused", {
        data: [{ code: "form_code_incorrect", message: "", long_message: "" }],
        status: 422,
      }),
    );
    const person = userEvent.setup();
    renderWithProviders(<TwoStepPage />);

    await enrol(person);

    expect(
      await screen.findByText(
        "That code does not match. Wait for the app to show a new one and try again.",
      ),
    ).toBeInTheDocument();
    expect(user.createBackupCode).not.toHaveBeenCalled();
  });

  it("TwoStepPage_UnknownClerkCodeAtEnrolment_ReportsOnlyTheCode", async () => {
    const { user } = fakeClerk();
    user.verifyTOTP.mockRejectedValueOnce(
      unknownCode("enrolment_code_of_2027"),
    );
    const person = userEvent.setup();
    renderWithProviders(<TwoStepPage />);

    await enrol(person);

    await vi.waitFor(() =>
      expect(sdk.captureException).toHaveBeenCalledWith(
        new Error(
          'Sign-in: no message for the Clerk error code "enrolment_code_of_2027".',
        ),
      ),
    );
  });

  it("TwoStepPage_UnknownClerkCodeAtTheChallenge_ReportsOnlyTheCode", async () => {
    const { session } = fakeClerk({ totpEnabled: true });
    session.attemptSecondFactorVerification.mockRejectedValueOnce(
      unknownCode("challenge_code_of_2027"),
    );
    const person = userEvent.setup();
    renderWithProviders(<TwoStepPage />);

    await person.type(
      await screen.findByRole("textbox", { name: "Code" }),
      "654321",
    );
    await person.click(screen.getByRole("button", { name: "Continue" }));

    await vi.waitFor(() =>
      expect(sdk.captureException).toHaveBeenCalledWith(
        new Error(
          'Sign-in: no message for the Clerk error code "challenge_code_of_2027".',
        ),
      ),
    );
  });

  it("TwoStepPage_EnrolledButTheSessionHasNoSecondFactor_AsksForTheChallenge", async () => {
    const { session } = fakeClerk({ afterReload: -1 });
    const person = userEvent.setup();
    renderWithProviders(<TwoStepPage />);
    await enrol(person);

    await person.click(await screen.findByRole("checkbox"));
    await person.click(
      screen.getByRole("button", { name: "Continue to the platform" }),
    );

    expect(
      await screen.findByRole("heading", {
        name: "Enter your authenticator code",
      }),
    ).toBeInTheDocument();
    expect(session.startVerification).toHaveBeenCalledWith({
      level: "second_factor",
    });
  });

  it("TwoStepPage_EnrolledAndTheSessionHasTheSecondFactor_GoesToThePlatform", async () => {
    const { session } = fakeClerk({ afterReload: 0 });
    const person = userEvent.setup();
    renderWithProviders(<TwoStepPage />);
    await enrol(person);

    await person.click(await screen.findByRole("checkbox"));
    await person.click(
      screen.getByRole("button", { name: "Continue to the platform" }),
    );

    await vi.waitFor(() =>
      expect(session.getToken).toHaveBeenCalledWith({ skipCache: true }),
    );
    expect(session.startVerification).not.toHaveBeenCalled();
  });

  it("TwoStepPage_AuthenticatorButNoSecondFactorInTheSession_ShowsTheChallenge", async () => {
    const { session, user } = fakeClerk({ totpEnabled: true });
    const person = userEvent.setup();
    renderWithProviders(<TwoStepPage />);

    await person.type(
      await screen.findByRole("textbox", { name: "Code" }),
      "654321",
    );
    await person.click(screen.getByRole("button", { name: "Continue" }));

    expect(session.attemptSecondFactorVerification).toHaveBeenCalledWith({
      strategy: "totp",
      code: "654321",
    });
    await vi.waitFor(() =>
      expect(session.getToken).toHaveBeenCalledWith({ skipCache: true }),
    );
    expect(user.createTOTP).not.toHaveBeenCalled();
  });

  it("TwoStepPage_RightCodeAtTheChallenge_AsksForAFreshTokenOnce", async () => {
    const { session } = fakeClerk({ totpEnabled: true });
    const person = userEvent.setup();
    renderWithProviders(<TwoStepPage />);

    await person.type(
      await screen.findByRole("textbox", { name: "Code" }),
      "654321",
    );
    await person.click(screen.getByRole("button", { name: "Continue" }));

    await vi.waitFor(() =>
      expect(session.getToken).toHaveBeenCalledWith({ skipCache: true }),
    );
    expect(session.getToken).toHaveBeenCalledTimes(1);
  });

  it("TwoStepPage_BackupCodeAtTheChallenge_VerifiesTheBackupCode", async () => {
    const { session } = fakeClerk({ totpEnabled: true });
    const person = userEvent.setup();
    renderWithProviders(<TwoStepPage />);

    await person.click(
      await screen.findByRole("button", { name: "Use a backup code" }),
    );
    await person.type(
      screen.getByRole("textbox", { name: "Backup code" }),
      "code3",
    );
    await person.click(screen.getByRole("button", { name: "Continue" }));

    expect(session.attemptSecondFactorVerification).toHaveBeenCalledWith({
      strategy: "backup_code",
      code: "code3",
    });
  });

  it("TwoStepPage_SecondFactorAlreadyVerified_GoesToThePlatform", async () => {
    const { session, user } = fakeClerk({
      totpEnabled: true,
      secondFactorAge: 2,
    });

    renderWithProviders(<TwoStepPage />);

    await vi.waitFor(() =>
      expect(session.getToken).toHaveBeenCalledWith({ skipCache: true }),
    );
    expect(user.createTOTP).not.toHaveBeenCalled();
  });

  it("TwoStepPage_SignOut_SignsOutToSignIn", async () => {
    fakeClerk();
    const person = userEvent.setup();
    renderWithProviders(<TwoStepPage />);

    await person.click(await screen.findByRole("button", { name: "Sign out" }));

    expect(clerk.signOut).toHaveBeenCalledWith({ redirectUrl: "/sign-in" });
  });
});
