import { render, screen, within } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import { describe, expect, it, vi } from "vitest";
import ar from "../../../messages/ar.json";
import en from "../../../messages/en.json";
import { AuthFooter } from "./auth-footer";

const renderFooter = (
  locale: "en" | "ar",
  messages: typeof en,
  termsUrl?: string,
) =>
  render(
    <NextIntlClientProvider locale={locale} messages={messages}>
      <AuthFooter
        onLanguageChange={vi.fn()}
        privacyUrl="https://starter.example/privacy"
        termsUrl={termsUrl}
      />
    </NextIntlClientProvider>,
  );

describe("AuthFooter", () => {
  it("AuthFooter_English_ShowsTheLanguageAndPrivacy", () => {
    renderFooter("en", en);
    const footer = screen.getByRole("navigation", {
      name: "Language and legal",
    });

    expect(
      within(footer).getByRole("link", { name: "Privacy" }),
    ).toHaveAttribute("href", "https://starter.example/privacy");
    expect(within(footer).getByRole("combobox")).toBeInTheDocument();
  });

  it("AuthFooter_Arabic_ShowsTheArabicLink", () => {
    renderFooter("ar", ar);

    expect(screen.getByRole("link", { name: "الخصوصية" })).toBeInTheDocument();
  });

  it("AuthFooter_TermsInForce_LinksTheTermsBeforePrivacy", () => {
    renderFooter("en", en, "https://starter.example/terms/2026-12");
    const links = within(
      screen.getByRole("navigation", { name: "Language and legal" }),
    ).getAllByRole("link");

    expect(links.map((link) => link.textContent)).toEqual(["Terms", "Privacy"]);
    expect(links[0]).toHaveAttribute(
      "href",
      "https://starter.example/terms/2026-12",
    );
  });

  it("AuthFooter_NoTermsInForce_HidesTerms", () => {
    renderFooter("en", en);

    expect(
      screen.queryByRole("link", { name: "Terms" }),
    ).not.toBeInTheDocument();
  });
});
