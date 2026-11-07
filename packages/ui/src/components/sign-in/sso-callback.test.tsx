import { act, render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import en from "../../../messages/en.json";
import { SsoCallback } from "./sso-callback";

// Clerk's HandleSSOCallback is not ours: the fake calls the exit the test names, as Clerk would at the end of the callback.
const clerk = vi.hoisted(() => ({ exit: "navigateToApp" }));
const decorateUrl = vi.fn((url: string) => `#${url}`);
vi.mock("@clerk/nextjs", () => ({
  HandleSSOCallback: (props: Record<string, (args: unknown) => void>) => {
    props[clerk.exit]?.({ session: {}, decorateUrl });
    return null;
  },
}));

const renderCallback = () =>
  render(
    <NextIntlClientProvider locale="en" messages={en} timeZone="Europe/Dublin">
      <SsoCallback
        brand="Starter"
        onLanguageChange={async () => {}}
        privacyUrl="https://starter.example/privacy"
      />
    </NextIntlClientProvider>,
  );

beforeEach(() => {
  decorateUrl.mockClear();
});

afterEach(() => {
  vi.useRealTimers();
});

describe("SsoCallback", () => {
  it("SsoCallback_NewSession_GoesToTheRedirectOfThisApp", () => {
    clerk.exit = "navigateToApp";
    window.history.replaceState(
      {},
      "",
      "/sso-callback?redirect_url=%2Fnorthwind",
    );

    renderCallback();

    expect(decorateUrl).toHaveBeenCalledWith("/northwind");
  });

  it("SsoCallback_RedirectToAnotherOrigin_GoesHome", () => {
    clerk.exit = "navigateToApp";
    window.history.replaceState(
      {},
      "",
      "/sso-callback?redirect_url=https%3A%2F%2Fevil.example%2F",
    );

    renderCallback();

    expect(decorateUrl).toHaveBeenCalledWith("/");
  });

  // Frames 1.38 and platform 1.13: while Clerk ends the callback, the card says what happens.
  it("SsoCallback_WhileClerkFinishes_SaysItSignsYouIn", () => {
    clerk.exit = "none";

    renderCallback();

    expect(
      screen.getByRole("heading", { level: 1, name: "Signing you in…" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveTextContent(
      "Google or Microsoft sent you back. This takes a moment.",
    );
    expect(
      screen.queryByRole("link", { name: "Try again" }),
    ).not.toBeInTheDocument();
  });

  // Frames 1.52 and platform 1.14: after 10 seconds the card says so, and Try again goes back to sign-in with the same page to return to.
  it("SsoCallback_After10Seconds_SaysItTakesLongerAndOffersSignInAgain", () => {
    vi.useFakeTimers();
    clerk.exit = "none";
    window.history.replaceState(
      {},
      "",
      "/sso-callback?redirect_url=%2Fnorthwind",
    );

    renderCallback();
    act(() => vi.advanceTimersByTime(9_999));
    expect(
      screen.queryByRole("link", { name: "Try again" }),
    ).not.toBeInTheDocument();
    act(() => vi.advanceTimersByTime(1));

    expect(screen.getByRole("status")).toHaveTextContent(
      "This is taking longer than usual.",
    );
    expect(screen.getByRole("link", { name: "Try again" })).toHaveAttribute(
      "href",
      "/sign-in?redirect_url=%2Fnorthwind",
    );
  });
});
