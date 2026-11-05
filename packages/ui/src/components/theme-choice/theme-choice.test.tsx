import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import { ThemeProvider } from "next-themes";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import { ThemeChoice } from "./theme-choice";

const renderChoice = () =>
  render(
    <ThemeProvider attribute="class" defaultTheme="system" enableSystem>
      <NextIntlClientProvider locale="en" messages={en}>
        <ThemeChoice />
      </NextIntlClientProvider>
    </ThemeProvider>,
  );

describe("ThemeChoice", () => {
  it("ThemeChoice_FirstVisit_OffersSystemLightDarkWithSystemOn", () => {
    renderChoice();
    const group = screen.getByRole("radiogroup", { name: "Theme" });

    expect(
      [...group.querySelectorAll("[role=radio]")].map(
        (radio) => radio.textContent,
      ),
    ).toEqual(["System", "Light", "Dark"]);
    expect(screen.getByRole("radio", { name: "System" })).toBeChecked();
  });

  it("ThemeChoice_PickDark_SavesItOnTheDevice", async () => {
    renderChoice();

    await userEvent.click(screen.getByRole("radio", { name: "Dark" }));

    expect(localStorage.getItem("theme")).toBe("dark");
    expect(screen.getByRole("radio", { name: "Dark" })).toBeChecked();
    expect(
      screen.getByText(
        "Saved on this device. System follows the device’s setting.",
      ),
    ).toBeInTheDocument();
  });
});
