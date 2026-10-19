import { render } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import { beforeEach, describe, expect, it, vi } from "vitest";
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
      <SsoCallback />
    </NextIntlClientProvider>,
  );

beforeEach(() => {
  decorateUrl.mockClear();
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
});
