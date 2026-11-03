import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import { describe, expect, it, vi } from "vitest";
import en from "../../../messages/en.json";
import { AppError, type AppErrorProps } from "./app-error";

const renderError = (props: Omit<AppErrorProps, "onRetry">) => {
  const onRetry = vi.fn();
  render(
    <NextIntlClientProvider locale="en" messages={en}>
      <AppError {...props} onRetry={onRetry} />
    </NextIntlClientProvider>,
  );
  return onRetry;
};

describe("AppError", () => {
  it("AppError_Section_StaysLocalAndTriesAgain", async () => {
    const onRetry = renderError({ kind: "section" });

    expect(
      screen.getByText(
        "This part of the page didn’t load. The rest of the page still works.",
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole("main")).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Try again" }));
    expect(onRetry).toHaveBeenCalledOnce();
  });

  it("AppError_Start_FillsThePageWithReloadAndContactSupport", () => {
    renderError({ kind: "start", version: "a1b2c3d" });

    expect(
      screen.getByRole("heading", { name: "Starter couldn’t start" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("main")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Contact support" }),
    ).toHaveAttribute(
      "href",
      "mailto:support@starter.example?body=Version%3A%20a1b2c3d",
    );
  });
});
