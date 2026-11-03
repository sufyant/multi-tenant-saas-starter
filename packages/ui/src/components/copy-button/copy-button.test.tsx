import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import { CopyButton } from "./copy-button";

function renderButton() {
  const user = userEvent.setup();
  render(
    <NextIntlClientProvider locale="en" messages={en}>
      <CopyButton value="a1b2c3d" label="Copy the version" />
    </NextIntlClientProvider>,
  );
  return user;
}

describe("CopyButton", () => {
  it("CopyButton_Click_CopiesTheValueAndSaysCopied", async () => {
    const user = renderButton();

    await user.click(screen.getByRole("button", { name: "Copy the version" }));

    expect(await navigator.clipboard.readText()).toBe("a1b2c3d");
    expect(screen.getByRole("status")).toHaveTextContent("Copied");
  });

  it("CopyButton_TwoSecondsLater_SaysNothing", async () => {
    const user = renderButton();
    await user.click(screen.getByRole("button", { name: "Copy the version" }));

    await waitFor(() =>
      expect(screen.getByRole("status")).toHaveTextContent(""),
    );
  });
});
