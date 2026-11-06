import { ErrorCode, schemas } from "@repo/api-client";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import axe from "axe-core";
import { NextIntlClientProvider } from "next-intl";
import { afterEach, describe, expect, it, vi } from "vitest";
import { z } from "zod";
import en from "../../../messages/en.json";
import { expectVisibleToast } from "../../../test/toast";
import { dismissToasts, Toaster } from "../toast";
import { EmailChange, type EmailChangeProps } from "./email-change";

const messages = { ...en, locale: { format: "en-IE" } };
const now = new Date("2026-11-06T10:00:00Z");
const inTenMinutes = "2026-11-06T10:10:00Z";

/** A failure as the app reads it: the API's code, its field codes, and whether the API saved it under the key. */
// The app's schema: the generated body of RequestEmailChange, trimmed and required (F11).
const schema = schemas.RequestEmailChangeBody.extend({
  email: z
    .string()
    .trim()
    .min(1)
    .max(schemas.requestEmailChangeBodyEmailMax)
    .pipe(z.email()),
});

const refusal = (
  code: ErrorCode,
  {
    fields = null,
    saved = true,
  }: { fields?: Record<string, string[]> | null; saved?: boolean } = {},
) => ({
  problem: { code, reference: "4bf92f3577b34da6a3ce929d0e0e4736" },
  fields,
  saved,
});

function renderChange(overrides: Partial<EmailChangeProps> = {}) {
  const props: EmailChangeProps = {
    send: vi.fn(async () => ({ expiresAt: inTenMinutes })),
    verify: vi.fn(async () => {}),
    readFailure: vi.fn(() => refusal(ErrorCode.ServerError)),
    onChanged: vi.fn(),
    schema,
    now,
    ...overrides,
  };
  render(
    <NextIntlClientProvider
      locale="en"
      messages={messages}
      timeZone="Europe/Istanbul"
    >
      <EmailChange {...props} />
      <Toaster dir="ltr" closeLabel="Close" regionLabel="Notifications" />
    </NextIntlClientProvider>,
  );
  return props;
}

async function sendTo(address: string) {
  await userEvent.click(screen.getByRole("button", { name: "Change email" }));
  const dialog = within(
    await screen.findByRole("alertdialog", { name: "Change your email" }),
  );
  await userEvent.type(
    dialog.getByRole("textbox", { name: "New email" }),
    address,
  );
  await userEvent.click(dialog.getByRole("button", { name: "Send code" }));
}

afterEach(() => dismissToasts());

describe("EmailChange", () => {
  it("EmailChange_SendThenTheCode_ChangesTheEmailAndSaysSo", async () => {
    const props = renderChange();

    await sendTo("ayse.demir@northwind-traders.example");
    const step = within(
      await screen.findByRole("alertdialog", { name: "Check your email" }),
    );
    expect(
      step.getByText("ayse.demir@northwind-traders.example"),
    ).toBeInTheDocument();
    expect(step.getByText(/It works for 10 minutes\./)).toBeInTheDocument();
    await userEvent.type(step.getByRole("textbox", { name: "Code" }), "381906");
    await userEvent.click(step.getByRole("button", { name: "Verify" }));

    expect(props.send).toHaveBeenCalledWith(
      "ayse.demir@northwind-traders.example",
      expect.any(String),
    );
    expect(props.verify).toHaveBeenCalledWith("381906");
    expect(props.onChanged).toHaveBeenCalledOnce();
    await expectVisibleToast("Email changed");
  });

  it("EmailChange_AddressTaken_SaysSoUnderTheFieldAndTakesANewKey", async () => {
    const send = vi
      .fn()
      .mockRejectedValueOnce(new Error("refused"))
      .mockResolvedValueOnce({ expiresAt: inTenMinutes });
    renderChange({
      send,
      readFailure: () => refusal(ErrorCode.UserEmailTaken),
    });

    await sendTo("omar@northwind.example");
    expect(
      await screen.findByText(/Another account uses this email address\./),
    ).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Send code" }));

    expect(send.mock.calls[1]?.[1]).not.toBe(send.mock.calls[0]?.[1]);
  });

  it("EmailChange_InvalidAddress_ShowsTheFieldsCode", async () => {
    renderChange({
      send: vi.fn().mockRejectedValue(new Error("refused")),
      readFailure: () =>
        refusal(ErrorCode.RequestValidationFailed, {
          fields: { email: ["email_invalid"] },
        }),
    });

    await sendTo("not-an-address");

    expect(
      await screen.findByText("Enter an email address like name@company.com."),
    ).toBeInTheDocument();
  });

  it("EmailChange_ServerDown_KeepsTheKeyForTheRetry", async () => {
    const send = vi
      .fn()
      .mockRejectedValueOnce(new Error("down"))
      .mockResolvedValueOnce({ expiresAt: inTenMinutes });
    renderChange({
      send,
      readFailure: () => ({ problem: null, fields: null, saved: false }),
    });

    await sendTo("ayse.demir@northwind-traders.example");
    await screen.findByText(/^Something went wrong on our side\./);
    await userEvent.click(screen.getByRole("button", { name: "Send code" }));

    expect(send.mock.calls[1]?.[1]).toBe(send.mock.calls[0]?.[1]);
  });

  it("EmailChange_WrongCode_SaysSoUnderTheCode", async () => {
    const props = renderChange({
      verify: vi.fn().mockRejectedValue(new Error("refused")),
      readFailure: () => refusal(ErrorCode.VerificationCodeInvalid),
    });

    await sendTo("ayse.demir@northwind-traders.example");
    const step = within(
      await screen.findByRole("alertdialog", { name: "Check your email" }),
    );
    await userEvent.type(step.getByRole("textbox", { name: "Code" }), "000000");
    await userEvent.click(step.getByRole("button", { name: "Verify" }));

    expect(
      await step.findByText(
        /That code is not right\. Check the email and try again\./,
      ),
    ).toBeInTheDocument();
    expect(props.onChanged).not.toHaveBeenCalled();
  });

  it("EmailChange_SendANewCode_SendsAgainWithANewKey", async () => {
    const props = renderChange();

    await sendTo("ayse.demir@northwind-traders.example");
    const step = within(
      await screen.findByRole("alertdialog", { name: "Check your email" }),
    );
    await userEvent.click(
      step.getByRole("button", { name: "Send a new code" }),
    );

    const calls = (props.send as ReturnType<typeof vi.fn>).mock.calls;
    expect(calls).toHaveLength(2);
    expect(calls[1]?.[1]).not.toBe(calls[0]?.[1]);
    expect(
      await step.findByText("We sent a new code. It replaces the old one."),
    ).toBeInTheDocument();
  });

  it("EmailChange_EmptyAddress_SaysFillItInAndSendsNothing", async () => {
    const props = renderChange();

    await sendTo(" ");

    expect(await screen.findByText("Fill in this field.")).toBeInTheDocument();
    expect(props.send).not.toHaveBeenCalled();
  });

  it("EmailChange_MalformedAddress_SaysSoAndSendsNothing", async () => {
    const props = renderChange();

    await sendTo("ayse-at-northwind");

    expect(
      await screen.findByText("Enter an email address like name@company.com."),
    ).toBeInTheDocument();
    expect(props.send).not.toHaveBeenCalled();
  });

  it("EmailChange_AFailureTheAppHandled_ClosesWithoutAText", async () => {
    renderChange({
      send: vi.fn().mockRejectedValue(new Error("signed out")),
      readFailure: () => ({ ...refusal(ErrorCode.ServerError), handled: true }),
    });

    await sendTo("ayse.demir@northwind-traders.example");

    await expect.poll(() => screen.queryByRole("alertdialog")).toBeNull();
    expect(screen.queryByText(/Something went wrong/)).toBeNull();
  });

  it("EmailChange_SendANewCodeTwice_SendsOnceAtATime", async () => {
    let answer = () => {};
    const send = vi
      .fn()
      .mockResolvedValueOnce({ expiresAt: inTenMinutes })
      .mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            answer = () => resolve({ expiresAt: inTenMinutes });
          }),
      );
    renderChange({ send });

    await sendTo("ayse.demir@northwind-traders.example");
    const step = within(
      await screen.findByRole("alertdialog", { name: "Check your email" }),
    );
    await userEvent.click(
      step.getByRole("button", { name: "Send a new code" }),
    );
    await userEvent.click(
      step.getByRole("button", { name: "Send a new code" }),
    );
    answer();

    expect(send).toHaveBeenCalledTimes(2);
  });

  it("EmailChange_BothSteps_HaveNoAccessibilityViolation", async () => {
    renderChange();
    const violations = async (within: Element) =>
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

    await userEvent.click(screen.getByRole("button", { name: "Change email" }));
    expect(
      await violations(
        await screen.findByRole("alertdialog", { name: "Change your email" }),
      ),
    ).toEqual([]);
    await userEvent.type(
      screen.getByRole("textbox", { name: "New email" }),
      "ayse.demir@northwind-traders.example",
    );
    await userEvent.click(screen.getByRole("button", { name: "Send code" }));
    expect(
      await violations(
        await screen.findByRole("alertdialog", { name: "Check your email" }),
      ),
    ).toEqual([]);
  });
});
