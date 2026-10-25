import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import { describe, expect, it, vi } from "vitest";
import ar from "../../../messages/ar.json";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";
import { CodeStep } from "./code-step";

const languages = { en, tr, ar };

describe("CodeStep", () => {
  // The code's lifetime is Clerk's setting, not ours, so the screen writes no number for it (the sign-in boards).
  it.each([
    ["en", "We sent a 6-digit code to ali@northwind.io."],
    ["tr", "ali@northwind.io adresine 6 haneli bir kod gönderdik."],
    ["ar", "أرسلنا رمزًا من 6 أرقام إلى ali@northwind.io."],
  ] as const)(
    "CodeStep_InLanguage_%s_SaysOnlyWhereTheCodeWent",
    (locale, sentence) => {
      render(
        <NextIntlClientProvider
          locale={locale}
          messages={languages[locale]}
          timeZone="Europe/Dublin"
        >
          <CodeStep
            email="ali@northwind.io"
            onSubmit={vi.fn()}
            onResend={vi.fn()}
            onUseAnotherEmail={vi.fn()}
          />
        </NextIntlClientProvider>,
      );

      expect(
        screen.getByText("ali@northwind.io").parentElement?.textContent,
      ).toBe(sentence);
    },
  );

  // Clerk's rate limit decides how long to wait, so the text names no time (the Messages boards).
  it.each([
    ["en", "Too many requests. Wait a moment and try again."],
    ["tr", "Çok fazla istek. Biraz bekleyip tekrar deneyin."],
    ["ar", "طلبات كثيرة جدًا. انتظر قليلًا وحاول مرة أخرى."],
  ] as const)(
    "CodeStep_TooManyRequestsInLanguage_%s_SaysToWaitWithoutATime",
    (locale, text) => {
      render(
        <NextIntlClientProvider
          locale={locale}
          messages={languages[locale]}
          timeZone="Europe/Dublin"
        >
          <CodeStep
            email="ali@northwind.io"
            onSubmit={vi.fn()}
            onResend={vi.fn()}
            onUseAnotherEmail={vi.fn()}
            error="tooManyRequests"
          />
        </NextIntlClientProvider>,
      );

      expect(screen.getByRole("alert")).toHaveTextContent(text);
    },
  );
});
