import { ErrorCode } from "@repo/api-client";
import { getGetMeResponseMock } from "@repo/api-client/msw";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
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
import { renderWithProviders } from "../../../test/render";
import { CreateAccount } from "./create-account";

const push = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push, replace: vi.fn(), refresh: vi.fn() }),
}));
const signOut = vi.fn();
const getToken = vi.hoisted(() => vi.fn(async () => "token"));
vi.mock("@clerk/nextjs", () => ({
  useClerk: () => ({
    signOut,
    on: (_: string, handler: (status: string) => void) => handler("ready"),
    off: vi.fn(),
    session: { getToken },
  }),
}));
vi.mock("@/i18n/set-locale", () => ({ setLocale: vi.fn(async () => {}) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  push.mockClear();
  signOut.mockClear();
  getToken.mockClear();
});
afterAll(() => server.close());

const version = {
  id: "0b9b8f1e-6a3c-4a0a-9a51-3f2b7d1c2e10",
  effectiveDate: "2026-01-01",
  url: "https://starter.example/terms/2026",
};
const inForce = (answer: typeof version | null = version) =>
  http.get("*/v1/public/terms", () =>
    answer
      ? HttpResponse.json(answer)
      : new HttpResponse(null, { status: 204 }),
  );

function signingUp() {
  const bodies: unknown[] = [];
  server.use(
    http.post("*/v1/me/sign-up", async ({ request }) => {
      bodies.push(await request.json());
      return HttpResponse.json(getGetMeResponseMock({ language: "en" }));
    }),
  );
  return bodies;
}

describe("CreateAccount", () => {
  it("CreateAccount_TermsInForce_LinksThemAndEnablesAcceptOnlyWithTheBox", async () => {
    server.use(inForce());

    renderWithProviders(<CreateAccount />);

    expect(
      await screen.findByRole("heading", { name: "Accept the Terms of use" }),
    ).toBeInTheDocument();
    expect(
      await screen.findByRole("button", { name: "Read the terms ↗" }),
    ).toHaveAttribute("href", version.url);
    expect(
      screen.getByText(
        "Read the Terms of use, then accept them to create your account.",
      ),
    ).toBeInTheDocument();
    const accept = screen.getByRole("button", { name: "Accept" });
    expect(accept).toBeDisabled();
    await userEvent.setup().click(
      screen.getByRole("checkbox", {
        name: "I have read and agree to the Terms of use",
      }),
    );
    expect(accept).toBeEnabled();
  });

  it("CreateAccount_Accept_SignsUpWithTheLanguageAndTheVersionThenOpensCreateYourOrganisation", async () => {
    server.use(inForce());
    const bodies = signingUp();
    renderWithProviders(<CreateAccount />);
    const user = userEvent.setup();

    await user.click(
      await screen.findByRole("checkbox", {
        name: "I have read and agree to the Terms of use",
      }),
    );
    await user.click(screen.getByRole("button", { name: "Accept" }));

    await vi.waitFor(() => expect(push).toHaveBeenCalledWith("/new"));
    expect(bodies).toEqual([{ language: "en", termsVersionId: version.id }]);
    // F63: the new user's token comes before the page that reads as them.
    expect(getToken).toHaveBeenCalledBefore(push);
  });

  it("CreateAccount_NoVersionYet_AcceptsWithoutTheBoxAndSendsNoVersion", async () => {
    server.use(inForce(null));
    const bodies = signingUp();
    renderWithProviders(<CreateAccount />);

    expect(
      await screen.findByText("Accept to create your account."),
    ).toBeInTheDocument();
    expect(screen.queryByRole("checkbox")).not.toBeInTheDocument();
    await userEvent
      .setup()
      .click(screen.getByRole("button", { name: "Accept" }));

    await vi.waitFor(() =>
      expect(bodies).toEqual([{ language: "en", termsVersionId: null }]),
    );
  });

  it("CreateAccount_VersionChanged_EmptiesTheBoxAndReadsTheTermsAgain", async () => {
    let reads = 0;
    server.use(
      http.get("*/v1/public/terms", () => {
        reads += 1;
        return HttpResponse.json(version);
      }),
      http.post("*/v1/me/sign-up", () =>
        HttpResponse.json(
          { status: 409, code: ErrorCode.TermsVersionChanged, traceId: "t" },
          {
            status: 409,
            headers: { "Content-Type": "application/problem+json" },
          },
        ),
      ),
    );
    renderWithProviders(<CreateAccount />);
    const user = userEvent.setup();
    const box = await screen.findByRole("checkbox", {
      name: "I have read and agree to the Terms of use",
    });

    await user.click(box);
    await user.click(screen.getByRole("button", { name: "Accept" }));

    expect(
      await screen.findByText("Your account was not created"),
    ).toBeInTheDocument();
    expect(box).not.toBeChecked();
    await vi.waitFor(() => expect(reads).toBe(2));
    expect(push).not.toHaveBeenCalled();
  });

  it("CreateAccount_SignOut_SignsOutToSignIn", async () => {
    server.use(inForce());
    renderWithProviders(<CreateAccount />);

    await userEvent
      .setup()
      .click(await screen.findByRole("button", { name: "Sign out" }));

    expect(signOut).toHaveBeenCalledWith({ redirectUrl: "/sign-in" });
  });
});
