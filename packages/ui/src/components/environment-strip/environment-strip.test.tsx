import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import type { Environment } from "../../lib/environment";
import { EnvironmentStrip } from "./environment-strip";

const renderStrip = (environment: Environment) =>
  render(
    <NextIntlClientProvider locale="en" messages={en}>
      <EnvironmentStrip environment={environment} />
    </NextIntlClientProvider>,
  );

describe("EnvironmentStrip", () => {
  it.each([
    [{ kind: "staging" } as const, "Staging"],
    [{ kind: "preview", number: 7 } as const, "Preview · #7"],
    [{ kind: "local" } as const, "Local"],
  ])("EnvironmentStrip_%o_ShowsItsName", (environment, name) => {
    renderStrip(environment);

    expect(screen.getByText(name)).toBeInTheDocument();
  });

  it("EnvironmentStrip_Production_ShowsNothing", () => {
    const { container } = renderStrip({ kind: "production" });

    expect(container).toBeEmptyDOMElement();
  });
});
