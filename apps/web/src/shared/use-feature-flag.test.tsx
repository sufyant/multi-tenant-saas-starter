import { BuiltInRole, FeatureFlagKeys } from "@repo/api-client";
import { getGetMyTenantFeatureFlagsMockHandler } from "@repo/api-client/msw";
import { focusManager } from "@tanstack/react-query";
import { act, screen } from "@testing-library/react";
import { setupServer } from "msw/node";
import {
  afterAll,
  afterEach,
  beforeAll,
  describe,
  expect,
  it,
  vi,
} from "vitest";
import { renderInTenant, tenantAnswers } from "../../test/tenant";
import { useFeatureFlag } from "./use-feature-flag";

vi.mock("@clerk/nextjs", () => ({ useClerk: () => ({ signOut: vi.fn() }) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  vi.useRealTimers();
});
afterAll(() => server.close());

const [emailsSend] = FeatureFlagKeys;

function Probe() {
  const on = useFeatureFlag(emailsSend);
  return <p>{`emails.send ${String(on)}`}</p>;
}

/** GetMyTenantFeatureFlags: it counts the reads and answers each one from `answers` in order (the last one repeats). */
const flagsAnswer = (answers: boolean[], reads = { count: 0 }) =>
  getGetMyTenantFeatureFlagsMockHandler(() => {
    const on = answers[Math.min(reads.count, answers.length - 1)] ?? false;
    reads.count += 1;
    return { flags: { [emailsSend]: on } };
  });

describe("useFeatureFlag", () => {
  it("FeatureFlag_OnForTheOrganisation_True", async () => {
    server.use(...tenantAnswers(BuiltInRole.Member), flagsAnswer([true]));

    renderInTenant(<Probe />);

    expect(await screen.findByText("emails.send true")).toBeInTheDocument();
  });

  it("FeatureFlag_OffForTheOrganisation_False", async () => {
    server.use(...tenantAnswers(BuiltInRole.Member), flagsAnswer([false]));

    renderInTenant(<Probe />);

    expect(await screen.findByText("emails.send false")).toBeInTheDocument();
  });

  // The flags re-read on window focus once they are a minute old, so a change reaches an open page within a minute.
  it("FeatureFlag_FocusAfterAMinute_ReadsTheChange", async () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    const reads = { count: 0 };
    server.use(
      ...tenantAnswers(BuiltInRole.Member),
      flagsAnswer([false, true], reads),
    );
    renderInTenant(<Probe />);
    await screen.findByText("emails.send false");

    vi.setSystemTime(Date.now() + 61_000);
    act(() => {
      focusManager.setFocused(false);
      focusManager.setFocused(true);
    });

    expect(await screen.findByText("emails.send true")).toBeInTheDocument();
    expect(reads.count).toBe(2);
  });

  it("FeatureFlag_FocusWithinAMinute_DoesNotReadAgain", async () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    const reads = { count: 0 };
    server.use(
      ...tenantAnswers(BuiltInRole.Member),
      flagsAnswer([false, true], reads),
    );
    renderInTenant(<Probe />);
    await screen.findByText("emails.send false");

    vi.setSystemTime(Date.now() + 30_000);
    act(() => {
      focusManager.setFocused(false);
      focusManager.setFocused(true);
    });

    expect(await screen.findByText("emails.send false")).toBeInTheDocument();
    expect(reads.count).toBe(1);
  });
});
