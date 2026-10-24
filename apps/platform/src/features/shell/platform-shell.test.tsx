import { ErrorCode } from "@repo/api-client";
import {
  getGetMeMockHandler,
  getGetMeResponseMock,
  getListTenantsMockHandler,
  getListTenantsResponseMock,
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

const refused = {
  status: 403,
  title: "Forbidden",
  code: ErrorCode.AuthForbidden,
  traceId: "00-trace-02",
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

  it("PlatformShell_SystemAdminFromTheStart_DoesNotAskTheDoor", async () => {
    const doorCalls = vi.fn();
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: true, language: "en" }),
      ),
      getListTenantsMockHandler(() => {
        doorCalls();
        return getListTenantsResponseMock();
      }),
    );

    renderWithProviders(
      <PlatformShell railExpanded={false}>Content</PlatformShell>,
    );

    expect(await screen.findByText("Content")).toBeInTheDocument();
    expect(doorCalls).not.toHaveBeenCalled();
  });

  it("PlatformShell_NoSystemAdminAndTheDoorRefuses_SaysYouDontHaveAccessAfterOneDoorCall", async () => {
    const consoleError = vi.spyOn(console, "error");
    const meCalls = vi.fn();
    const doorCalls = vi.fn();
    server.use(
      getGetMeMockHandler(() => {
        meCalls();
        return getGetMeResponseMock({ isSystemAdmin: false, language: "en" });
      }),
      http.get("*/v1/platform/tenants", () => {
        doorCalls();
        return HttpResponse.json(refused, { status: 403 });
      }),
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
    expect(doorCalls).toHaveBeenCalledOnce();
    expect(meCalls).toHaveBeenCalledOnce();
    expect(consoleError).not.toHaveBeenCalled();
    consoleError.mockRestore();
  });

  it("PlatformShell_RootSystemAdminAlreadyAUser_AsksTheDoorThenReadsGetMeAgain", async () => {
    // A root system admin who is already a user, for example a member of a tenant, has no grant until the door writes it
    // (SystemAdminDirectory), so GetMe answers isSystemAdmin false until then.
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: false, language: "en" }),
        { once: true },
      ),
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

  it("PlatformShell_DoorLetsInButGetMeStillNoSystemAdmin_SaysYouDontHaveAccessAfterOneDoorCall", async () => {
    const meCalls = vi.fn();
    const doorCalls = vi.fn();
    server.use(
      getGetMeMockHandler(() => {
        meCalls();
        return getGetMeResponseMock({ isSystemAdmin: false, language: "en" });
      }),
      getListTenantsMockHandler(() => {
        doorCalls();
        return getListTenantsResponseMock();
      }),
    );

    renderWithProviders(
      <PlatformShell railExpanded={false}>Content</PlatformShell>,
    );

    expect(
      await screen.findByRole("heading", { name: "You don’t have access" }),
    ).toBeInTheDocument();
    expect(doorCalls).toHaveBeenCalledOnce();
    expect(meCalls).toHaveBeenCalledTimes(2);
  });

  it("PlatformShell_RootSystemAdminNotAUserYet_AsksTheDoorThenReadsGetMeAgain", async () => {
    // The door writes the root system admin's user at their first platform request (SystemAdminDirectory), so GetMe answers
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
        HttpResponse.json(refused, { status: 403 }),
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
