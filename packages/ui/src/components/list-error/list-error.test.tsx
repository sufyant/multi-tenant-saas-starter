import { ErrorCode } from "@repo/api-client";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";
import en from "../../../messages/en.json";
import { ListError } from "./list-error";

// Each app holds the locale's format; the list error reads it as the app's pages do.
const messages = { ...en, locale: { format: "en-IE" } };
const at = Date.parse("2026-10-16T13:32:00Z");
const reference = "4bf92f3577b34da6a3ce929d0e0e4736";

function renderError(
  props: Partial<Parameters<typeof ListError>[0]> & { leave?: ReactNode } = {},
) {
  const onRetry = vi.fn();
  render(
    <NextIntlClientProvider
      locale="en"
      messages={messages}
      timeZone="Europe/Dublin"
    >
      <ListError
        title="Couldn’t load members"
        problem={{ code: ErrorCode.TenantNotFound, reference }}
        at={at}
        onRetry={onRetry}
        {...props}
      />
    </NextIntlClientProvider>,
  );
  return onRetry;
}

describe("ListError", () => {
  it("ListError_Problem_ShowsItsCodesMessageAndItsReference", () => {
    renderError();

    expect(screen.getByText("Couldn’t load members")).toBeInTheDocument();
    expect(
      screen.getByText(
        "This organisation does not exist, or you are not a member of it.",
      ),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Ref 4bf92f3577b34da6a3ce929d0e0e4736 · 13:32 UTC"),
    ).toBeInTheDocument();
  });

  it("ListError_NoProblem_ShowsTheGenericTextWithoutAReference", () => {
    renderError({ problem: null });

    expect(
      screen.getByText(
        "The page could not load. Your data is safe. Try again.",
      ),
    ).toBeInTheDocument();
    expect(screen.queryByText(/^Ref /)).not.toBeInTheDocument();
  });

  it("ListError_TryAgain_CallsOnRetry", async () => {
    const onRetry = renderError();

    await userEvent.click(screen.getByRole("button", { name: "Try again" }));

    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it("ListError_Leave_ShowsItInPlaceOfTheError", () => {
    renderError({ leave: <p>Your account is suspended</p> });

    expect(screen.getByText("Your account is suspended")).toBeInTheDocument();
    expect(screen.queryByText("Couldn’t load members")).not.toBeInTheDocument();
  });
});
