import {
  FeatureFlagResponseType,
  TenantFeatureFlagResponseReason,
} from "@repo/api-client";
import { screen, within } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { setupServer } from "msw/node";
import { afterAll, afterEach, beforeAll, describe, expect, it } from "vitest";
import { northwindId } from "../../../test/feature-flags";
import { meAnswer } from "../../../test/organisations";
import { renderWithProviders } from "../../../test/render";
import { FeatureFlagsCard } from "./feature-flags-card";

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const flagsOf = (flags: unknown[]) =>
  http.get(`*/v1/platform/tenants/${northwindId}/feature-flags`, () =>
    HttpResponse.json(flags),
  );

describe("FeatureFlagsCard", () => {
  it("FeatureFlagsCard_FlagsOn_SaysEachFlagsReason", async () => {
    server.use(
      meAnswer(),
      flagsOf([
        {
          key: "roles.bulk_edit",
          type: FeatureFlagResponseType.Release,
          reason: TenantFeatureFlagResponseReason.Listed,
          percentage: 10,
        },
        {
          key: "members.new_table",
          type: FeatureFlagResponseType.Release,
          reason: TenantFeatureFlagResponseReason.Percentage,
          percentage: 25,
        },
        {
          key: "emails.send",
          type: FeatureFlagResponseType.KillSwitch,
          reason: TenantFeatureFlagResponseReason.Everyone,
          percentage: 0,
        },
      ]),
    );

    renderWithProviders(<FeatureFlagsCard tenantId={northwindId} />);

    const card = within(
      await screen.findByRole("region", { name: "Feature flags" }),
    );
    expect(await card.findByText("Listed")).toBeVisible();
    expect(card.getByText("Always on for this organisation.")).toBeVisible();
    expect(card.getByText("In the 25%")).toBeVisible();
    expect(card.getByText("On for everyone")).toBeVisible();
    expect(card.getByText("Kill switch.")).toBeVisible();
    expect(card.getByRole("link", { name: "emails.send" })).toHaveAttribute(
      "href",
      "/settings/feature-flags/emails.send",
    );
  });

  it("FeatureFlagsCard_NoFlagOn_SaysSo", async () => {
    server.use(meAnswer(), flagsOf([]));

    renderWithProviders(<FeatureFlagsCard tenantId={northwindId} />);

    expect(
      await screen.findByText("No flag is on for this organisation."),
    ).toBeVisible();
  });
});
