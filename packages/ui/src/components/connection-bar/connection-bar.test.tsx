import { act, fireEvent, render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import en from "../../../messages/en.json";
import { ConnectionBar } from "./connection-bar";

function renderBar(serverDown: boolean, statusUrl?: string) {
  const onRetry = vi.fn();
  const view = (down: boolean) => (
    <NextIntlClientProvider locale="en" messages={en}>
      <ConnectionBar
        serverDown={down}
        onRetry={onRetry}
        product="Starter"
        statusUrl={statusUrl}
      />
    </NextIntlClientProvider>
  );
  const { rerender } = render(view(serverDown));
  return { onRetry, rerender: (down: boolean) => rerender(view(down)) };
}

const seconds = (count: number) =>
  act(() => vi.advanceTimersByTime(count * 1000));

describe("ConnectionBar", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("ConnectionBar_Connected_ShowsNothing", () => {
    renderBar(false);

    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });

  it("ConnectionBar_ServerDown_TriesAfterFiveThenTenThenThirtySeconds", () => {
    const { onRetry } = renderBar(true);
    expect(screen.getByRole("status")).toHaveTextContent(
      "Starter isn’t answering. Trying again in 5 s",
    );

    seconds(5);
    expect(onRetry).toHaveBeenCalledTimes(1);
    expect(screen.getByRole("status")).toHaveTextContent(
      "Trying again in 10 s",
    );

    seconds(10);
    expect(onRetry).toHaveBeenCalledTimes(2);
    expect(screen.getByRole("status")).toHaveTextContent(
      "Trying again in 30 s",
    );

    seconds(30);
    expect(onRetry).toHaveBeenCalledTimes(3);
    expect(screen.getByRole("status")).toHaveTextContent(
      "Trying again in 30 s",
    );
  });

  it("ConnectionBar_TryNow_TriesAtOnce", () => {
    const { onRetry } = renderBar(true);

    fireEvent.click(screen.getByRole("button", { name: "Try now" }));

    expect(onRetry).toHaveBeenCalledOnce();
    expect(screen.getByRole("status")).toHaveTextContent(
      "Trying again in 10 s",
    );
  });

  it("ConnectionBar_Offline_SaysSo", () => {
    vi.spyOn(navigator, "onLine", "get").mockReturnValue(false);
    renderBar(false);

    expect(screen.getByRole("status")).toHaveTextContent(
      "You’re offline. Trying again in 5 s",
    );
  });

  it("ConnectionBar_StatusUrl_LinksTheStatusPageWhileTheServerIsDown", () => {
    renderBar(true, "https://status.starter.example");

    expect(
      screen.getByRole("link", { name: "Check the status page" }),
    ).toHaveAttribute("href", "https://status.starter.example");
  });

  it("ConnectionBar_NoStatusUrl_ShowsNoLink", () => {
    renderBar(true);

    expect(screen.queryByRole("link")).not.toBeInTheDocument();
  });

  it("ConnectionBar_AnswerComes_SaysBackOnlineInGreenForTwoSeconds", () => {
    const { rerender } = renderBar(true);

    rerender(false);
    expect(screen.getByRole("status")).toHaveTextContent("Back online");
    expect(screen.getByRole("status").parentElement).toHaveClass(
      "bg-success-subtle",
    );

    act(() => vi.advanceTimersByTime(1999));
    expect(screen.getByRole("status")).toHaveTextContent("Back online");
    act(() => vi.advanceTimersByTime(1));
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });
});
