import { ErrorCode } from "@repo/api-client";
import {
  getGetMeMockHandler,
  getGetMeResponseMock,
  getListTenantsMockHandler,
} from "@repo/api-client/msw";
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
import { PlatformShell } from "./platform-shell";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
  usePathname: () => "/",
}));
vi.mock("@clerk/nextjs", () => ({
  useClerk: () => ({ signOut: vi.fn() }),
  useUser: () => ({
    user: { primaryEmailAddress: { emailAddress: "ali.kaya@gmail.com" } },
  }),
}));
vi.mock("@/i18n/set-locale", () => ({ setLocale: vi.fn(async () => {}) }));

const notAUser = {
  status: 404,
  title: "Not Found",
  code: ErrorCode.UserNotFound,
  traceId: "00-trace-01",
};

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

describe("PlatformShell", () => {
  it("PlatformShell_SystemAdmin_StartsTheTopBarWithPlatform", async () => {
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: true, language: "en" }),
      ),
    );

    renderWithProviders(
      <PlatformShell railExpanded={false}>Content</PlatformShell>,
    );

    expect(await screen.findByRole("banner")).toHaveTextContent(/^Platform/);
  });

  it("PlatformShell_NoSystemAdmin_SaysYouDontHaveAccess", async () => {
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: false, language: "en" }),
      ),
    );

    renderWithProviders(
      <PlatformShell railExpanded={false}>Content</PlatformShell>,
    );

    expect(
      await screen.findByRole("heading", { name: "You don’t have access" }),
    ).toBeInTheDocument();
    expect(screen.getByText("ali.kaya@gmail.com")).toHaveAttribute(
      "dir",
      "ltr",
    );
    expect(screen.queryByText("Content")).not.toBeInTheDocument();
  });

  it("PlatformShell_FirstSystemAdminAtTheFirstVisit_AsksTheDoorThenReadsGetMeAgain", async () => {
    // The door writes the first system admin's user at their first platform request (SystemAdminDirectory), so GetMe answers
    // 404 until then.
    server.use(
      http.get("*/v1/me", () => HttpResponse.json(notAUser, { status: 404 }), {
        once: true,
      }),
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: true, language: "en" }),
      ),
      getListTenantsMockHandler(),
    );

    renderWithProviders(
      <PlatformShell railExpanded={false}>Content</PlatformShell>,
    );

    expect(await screen.findByText("Content")).toBeInTheDocument();
  });

  it("PlatformShell_DoorLetsInButGetMeStill404_SaysYouDontHaveAccess", async () => {
    server.use(
      http.get("*/v1/me", () => HttpResponse.json(notAUser, { status: 404 })),
      getListTenantsMockHandler(),
    );

    renderWithProviders(
      <PlatformShell railExpanded={false}>Content</PlatformShell>,
    );

    expect(
      await screen.findByRole("heading", { name: "You don’t have access" }),
    ).toBeInTheDocument();
  });

  it("PlatformShell_NotAUserAndTheDoorRefuses_SaysYouDontHaveAccess", async () => {
    server.use(
      http.get("*/v1/me", () => HttpResponse.json(notAUser, { status: 404 })),
      http.get("*/v1/platform/tenants", () =>
        HttpResponse.json(
          {
            status: 403,
            title: "Forbidden",
            code: ErrorCode.AuthForbidden,
            traceId: "00-trace-02",
          },
          { status: 403 },
        ),
      ),
    );

    renderWithProviders(
      <PlatformShell railExpanded={false}>Content</PlatformShell>,
    );

    expect(
      await screen.findByRole("heading", { name: "You don’t have access" }),
    ).toBeInTheDocument();
  });
});
