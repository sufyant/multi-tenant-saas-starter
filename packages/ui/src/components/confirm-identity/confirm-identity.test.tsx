import { ClerkAPIResponseError } from "@clerk/nextjs/errors";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import { describe, expect, it, vi } from "vitest";
import en from "../../../messages/en.json";
import { ConfirmIdentity, type VerifyingSession } from "./confirm-identity";

// Clerk's session reverification, faked at its boundary: each call answers the next state of the check.
function sessionOf(
  first: "needs_first_factor" | "needs_second_factor",
  answers: Partial<Record<"first" | "second", () => Promise<unknown>>> = {},
) {
  const emailFactor = {
    strategy: "email_code",
    emailAddressId: "idn_ayse",
    safeIdentifier: "ayse@northwind.example",
  };
  return {
    startVerification: vi.fn(async () => ({
      status: first,
      supportedFirstFactors: [emailFactor],
    })),
    prepareFirstFactorVerification: vi.fn(async () => ({
      status: "needs_first_factor",
    })),
    attemptFirstFactorVerification: vi.fn(
      answers.first ?? (async () => ({ status: "complete" })),
    ),
    attemptSecondFactorVerification: vi.fn(
      answers.second ?? (async () => ({ status: "complete" })),
    ),
    getToken: vi.fn(async () => "token"),
  };
}

function renderStep(
  session: ReturnType<typeof sessionOf>,
  level: "first_factor" | "second_factor" | "multi_factor" = "first_factor",
) {
  const onVerified = vi.fn();
  const onCancel = vi.fn();
  render(
    <NextIntlClientProvider locale="en" messages={en}>
      <ConfirmIdentity
        session={session as unknown as VerifyingSession}
        level={level}
        onVerified={onVerified}
        onCancel={onCancel}
      />
    </NextIntlClientProvider>,
  );
  return { onVerified, onCancel };
}

describe("ConfirmIdentity", () => {
  it("ConfirmIdentity_FirstFactor_EmailsACodeAndChecksIt", async () => {
    const session = sessionOf("needs_first_factor");
    const { onVerified } = renderStep(session);

    const step = within(
      await screen.findByRole("alertdialog", { name: "Confirm it’s you" }),
    );
    expect(
      await step.findByText(/We sent a 6-digit code to/),
    ).toBeInTheDocument();
    expect(step.getByText("ayse@northwind.example")).toBeInTheDocument();
    expect(session.startVerification).toHaveBeenCalledWith({
      level: "first_factor",
    });
    expect(session.prepareFirstFactorVerification).toHaveBeenCalledWith({
      strategy: "email_code",
      emailAddressId: "idn_ayse",
    });
    await userEvent.type(step.getByRole("textbox", { name: "Code" }), "381906");
    await userEvent.click(
      step.getByRole("button", { name: "Verify and continue" }),
    );

    expect(session.attemptFirstFactorVerification).toHaveBeenCalledWith({
      strategy: "email_code",
      code: "381906",
    });
    expect(session.getToken).toHaveBeenCalledWith({ skipCache: true });
    expect(onVerified).toHaveBeenCalledOnce();
  });

  it("ConfirmIdentity_SecondFactor_ChecksTheAuthenticatorsCode", async () => {
    const session = sessionOf("needs_second_factor");
    const { onVerified } = renderStep(session, "second_factor");

    const step = within(
      await screen.findByRole("alertdialog", { name: "Confirm it’s you" }),
    );
    expect(
      await step.findByText(/code from your authenticator app/),
    ).toBeInTheDocument();
    await userEvent.type(step.getByRole("textbox", { name: "Code" }), "654321");
    await userEvent.click(
      step.getByRole("button", { name: "Verify and continue" }),
    );

    expect(session.prepareFirstFactorVerification).not.toHaveBeenCalled();
    expect(session.attemptSecondFactorVerification).toHaveBeenCalledWith({
      strategy: "totp",
      code: "654321",
    });
    expect(onVerified).toHaveBeenCalledOnce();
  });

  it("ConfirmIdentity_MultiFactor_AsksTheAppAfterTheEmail", async () => {
    const session = sessionOf("needs_first_factor", {
      first: async () => ({ status: "needs_second_factor" }),
    });
    const { onVerified } = renderStep(session, "multi_factor");

    const step = within(
      await screen.findByRole("alertdialog", { name: "Confirm it’s you" }),
    );
    await step.findByText(/We sent a 6-digit code/);
    await userEvent.type(step.getByRole("textbox", { name: "Code" }), "381906");
    await userEvent.click(
      step.getByRole("button", { name: "Verify and continue" }),
    );

    expect(
      await step.findByText(/code from your authenticator app/),
    ).toBeInTheDocument();
    expect(onVerified).not.toHaveBeenCalled();
  });

  it("ConfirmIdentity_WrongCode_SaysSoAndStaysOpen", async () => {
    const session = sessionOf("needs_first_factor", {
      first: async () => {
        throw new ClerkAPIResponseError("refused", {
          data: [
            { code: "form_code_incorrect", message: "", long_message: "" },
          ],
          status: 422,
        });
      },
    });
    const { onVerified } = renderStep(session);

    const step = within(
      await screen.findByRole("alertdialog", { name: "Confirm it’s you" }),
    );
    await step.findByText(/We sent a 6-digit code/);
    await userEvent.type(step.getByRole("textbox", { name: "Code" }), "000000");
    await userEvent.click(
      step.getByRole("button", { name: "Verify and continue" }),
    );

    expect(
      await step.findByText(
        "That code is not right. Check the email and try again.",
      ),
    ).toBeInTheDocument();
    expect(onVerified).not.toHaveBeenCalled();
  });

  it("ConfirmIdentity_SendANewCode_EmailsAnotherOne", async () => {
    const session = sessionOf("needs_first_factor");
    renderStep(session);

    const step = within(
      await screen.findByRole("alertdialog", { name: "Confirm it’s you" }),
    );
    await step.findByText(/We sent a 6-digit code/);
    await userEvent.click(
      step.getByRole("button", { name: "Send a new code" }),
    );

    expect(session.prepareFirstFactorVerification).toHaveBeenCalledTimes(2);
    expect(await step.findByText("We sent a new code.")).toBeInTheDocument();
  });

  it("ConfirmIdentity_Cancel_SendsNothing", async () => {
    const session = sessionOf("needs_first_factor");
    const { onCancel, onVerified } = renderStep(session);

    const step = within(
      await screen.findByRole("alertdialog", { name: "Confirm it’s you" }),
    );
    await userEvent.click(step.getByRole("button", { name: "Cancel" }));

    expect(onCancel).toHaveBeenCalledOnce();
    expect(onVerified).not.toHaveBeenCalled();
    expect(session.attemptFirstFactorVerification).not.toHaveBeenCalled();
  });
});
