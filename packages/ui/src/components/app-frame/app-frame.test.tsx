import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import type { Environment } from "../../lib/environment";
import { AppFrameProvider, useAppFrame } from "./app-frame";

function Version() {
  return <p>{useAppFrame().version}</p>;
}

const renderFrame = (environment: Environment) =>
  render(
    <NextIntlClientProvider locale="en" messages={en}>
      <AppFrameProvider value={{ environment, version: "a1b2c3d" }}>
        <Version />
      </AppFrameProvider>
    </NextIntlClientProvider>,
  );

describe("AppFrameProvider", () => {
  it("AppFrameProvider_Staging_ShowsTheStripAboveThePage", () => {
    renderFrame({ kind: "staging" });

    const strip = screen.getByText("Staging environment · test data only");
    expect(
      strip.compareDocumentPosition(screen.getByText("a1b2c3d")) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });

  it("AppFrameProvider_Production_ShowsNoStrip", () => {
    renderFrame({ kind: "production" });

    expect(
      document.querySelector("[data-slot=environment-strip]"),
    ).not.toBeInTheDocument();
    expect(screen.getByText("a1b2c3d")).toBeInTheDocument();
  });
});
