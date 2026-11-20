import { screen } from "@testing-library/react";
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
import { SignInScreen } from "./sign-in-screen";
import { SignUpScreen } from "./sign-up-screen";

const replace = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace, push: vi.fn(), refresh: vi.fn() }),
}));
vi.mock("@clerk/nextjs", () => ({
  useSignIn: () => ({ signIn: {}, fetchStatus: "idle" }),
  useSignUp: () => ({ signUp: {}, fetchStatus: "idle" }),
}));
vi.mock("@/error-tracking/report-error", () => ({ reportError: vi.fn() }));

const signUpOf = (enabled: boolean) =>
  http.get("*/v1/public/sign-up", () =>
    HttpResponse.json({ enabled, tenantLimit: 3, trial: null }),
  );
const noTerms = http.get(
  "*/v1/public/terms",
  () => new HttpResponse(null, { status: 204 }),
);

const server = setupServer(noTerms);
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  replace.mockClear();
});
afterAll(() => server.close());

const words = {
  brand: "Starter",
  onLanguageChange: async () => {},
  privacyUrl: "https://starter.example/privacy",
};

const renderSignUp = () =>
  renderWithProviders(
    <SignUpScreen
      title="Create an account"
      text="Use your work email. We send you a 6-digit code to confirm it."
      {...words}
    />,
  );

const renderSignIn = () =>
  renderWithProviders(
    <SignInScreen
      title="Sign in to your account"
      text="Use your work email. We send you a 6-digit code. No password."
      secondFactorTitle="Two-step verification"
      secondFactorText="Open your authenticator app."
      {...words}
    />,
  );

describe("SignUpScreen", () => {
  it("SignUpScreen_SignUpOn_ShowsCreateAnAccount", async () => {
    server.use(signUpOf(true));

    renderSignUp();

    expect(
      await screen.findByRole("heading", { name: "Create an account" }),
    ).toBeInTheDocument();
    expect(replace).not.toHaveBeenCalled();
  });

  it("SignUpScreen_SignUpOff_LeadsToSignIn", async () => {
    server.use(signUpOf(false));

    renderSignUp();

    await vi.waitFor(() => expect(replace).toHaveBeenCalledWith("/sign-in"));
    expect(
      screen.queryByRole("heading", { name: "Create an account" }),
    ).not.toBeInTheDocument();
  });

  it("SignUpScreen_GetSignUpFails_LeadsToSignIn", async () => {
    server.use(
      http.get("*/v1/public/sign-up", () =>
        HttpResponse.json(
          { status: 500, traceId: "t" },
          {
            status: 500,
            headers: { "Content-Type": "application/problem+json" },
          },
        ),
      ),
    );

    renderSignUp();

    await vi.waitFor(() => expect(replace).toHaveBeenCalledWith("/sign-in"));
  });
});

describe("SignInScreen", () => {
  it("SignInScreen_SignUpOn_OffersCreateAnAccount", async () => {
    server.use(signUpOf(true));

    renderSignIn();

    expect(
      await screen.findByRole("link", { name: "Create an account" }),
    ).toHaveAttribute("href", "/sign-up");
  });

  it("SignInScreen_SignUpOff_OffersNoAccount", async () => {
    server.use(signUpOf(false));

    renderSignIn();

    await screen.findByRole("heading", { name: "Sign in to your account" });
    await new Promise((done) => setTimeout(done, 50));
    expect(
      screen.queryByRole("link", { name: "Create an account" }),
    ).not.toBeInTheDocument();
  });
});
