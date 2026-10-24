import {
  ErrorCode,
  type InvitationPreviewResponse,
  InvitationResponseState,
  InviterNameResponseKind,
} from "@repo/api-client";
import {
  getAcceptInvitationMockHandler,
  getGetInvitationPreviewMockHandler,
  getGetMeMockHandler,
  getGetMeResponseMock,
} from "@repo/api-client/msw";
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
import { roleOf } from "../../../test/tenant";
import { AcceptInvitationPage } from "./accept-invitation-page";

// Clerk is not ours, so the test fakes its hooks (Khorikov, Chapter 9). The API is faked at the network with the generated MSW
// handlers (F35).
const clerk = vi.hoisted(() => ({
  isSignedIn: true,
  signOut: (() => {}) as unknown,
  signUp: {} as unknown,
}));
const push = vi.hoisted(() => ({
  fn: (() => {}) as unknown,
  replace: (() => {}) as unknown,
}));
vi.mock("@clerk/nextjs", () => ({
  useAuth: () => ({ isLoaded: true, isSignedIn: clerk.isSignedIn }),
  useUser: () => ({
    user: { primaryEmailAddress: { emailAddress: "ali@northwind.io" } },
  }),
  useClerk: () => ({ signOut: clerk.signOut }),
  useSignUp: () => ({ signUp: clerk.signUp }),
}));
vi.mock("next/navigation", () => ({
  useRouter: () => ({
    push: push.fn,
    replace: push.replace,
    refresh: vi.fn(),
  }),
}));

const code = "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f.s3cr3t";

const pending: InvitationPreviewResponse = {
  tenantName: "Northwind Traders",
  inviterName: {
    kind: InviterNameResponseKind.User,
    firstName: "Ayşe",
    lastName: "Yılmaz",
    deleted: false,
  },
  role: roleOf.member,
  expiresAt: "2099-10-23T13:32:00Z",
  state: InvitationResponseState.Pending,
};

const problem = (status: number, code: string) =>
  HttpResponse.json(
    { status, title: "Refused", code, traceId: "00-trace-01" },
    { status },
  );

const notAUser = http.get("*/v1/me", () =>
  problem(404, ErrorCode.UserNotFound),
);
const knownUser = getGetMeMockHandler(
  getGetMeResponseMock({
    email: "ali@northwind.io",
    language: "en",
    timeZone: "Europe/Dublin",
  }),
);

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  vi.clearAllMocks();
  clerk.isSignedIn = true;
});
afterAll(() => server.close());

function setUp() {
  clerk.signOut = vi.fn();
  push.fn = vi.fn();
  push.replace = vi.fn();
  sessionStorage.clear();
}

const signInAndBack = "/sign-in?redirect_url=%2Finvitations%2Faccept";

describe("AcceptInvitationPage", () => {
  it("AcceptInvitationPage_KnownPerson_JoinsInOneClickAndOpensTheOrganisation", async () => {
    setUp();
    const accepted = vi.fn();
    server.use(
      knownUser,
      getGetInvitationPreviewMockHandler(pending),
      getAcceptInvitationMockHandler(async ({ request }) => {
        accepted(await request.json());
        return {
          id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f",
          name: "Northwind Traders",
          slug: "northwind",
          status: "active",
          role: roleOf.member,
        };
      }),
    );
    const user = userEvent.setup();
    renderWithProviders(<AcceptInvitationPage code={code} />);

    expect(
      await screen.findByRole("heading", {
        name: "Ayşe Yılmaz invited you to join Northwind Traders",
      }),
    ).toBeInTheDocument();
    await user.click(
      screen.getByRole("button", { name: "Join Northwind Traders" }),
    );

    await vi.waitFor(() => expect(push.fn).toHaveBeenCalledWith("/northwind"));
    expect(accepted).toHaveBeenCalledWith({ code });
  });

  it("AcceptInvitationPage_NewPerson_AsksForBothNamesAndSendsThemTrimmed", async () => {
    setUp();
    const accepted = vi.fn();
    server.use(
      notAUser,
      getGetInvitationPreviewMockHandler(pending),
      getAcceptInvitationMockHandler(async ({ request }) => {
        accepted(await request.json());
        return {
          id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f",
          name: "Northwind Traders",
          slug: "northwind",
          status: "active",
          role: roleOf.member,
        };
      }),
    );
    const user = userEvent.setup();
    renderWithProviders(<AcceptInvitationPage code={code} />);

    expect(
      await screen.findByRole("heading", {
        name: "Ayşe Yılmaz invited you to join Northwind Traders as a Member",
      }),
    ).toBeInTheDocument();
    await user.type(
      screen.getByRole("textbox", { name: "First name" }),
      "  Ali ",
    );
    await user.type(screen.getByRole("textbox", { name: "Last name" }), "Kaya");
    await user.click(
      screen.getByRole("button", { name: "Join Northwind Traders" }),
    );

    await vi.waitFor(() => expect(push.fn).toHaveBeenCalledWith("/northwind"));
    expect(accepted).toHaveBeenCalledWith({
      code,
      firstName: "Ali",
      lastName: "Kaya",
    });
  });

  it("AcceptInvitationPage_NewPersonWithoutAFirstName_SaysFillItInAndSendsNothing", async () => {
    setUp();
    const accepted = vi.fn();
    server.use(
      notAUser,
      getGetInvitationPreviewMockHandler(pending),
      http.post("*/v1/me/invitations/accept", () => {
        accepted();
        return HttpResponse.json({});
      }),
    );
    const user = userEvent.setup();
    renderWithProviders(<AcceptInvitationPage code={code} />);

    await user.type(
      await screen.findByRole("textbox", { name: "Last name" }),
      "Kaya",
    );
    await user.click(
      screen.getByRole("button", { name: "Join Northwind Traders" }),
    );

    expect(
      await screen.findByRole("textbox", { name: "First name" }),
    ).toHaveAccessibleDescription("Fill in this field.");
    expect(accepted).not.toHaveBeenCalled();
  });

  it("AcceptInvitationPage_NameTooLongForTheApi_ShowsTheFieldError", async () => {
    setUp();
    server.use(
      notAUser,
      getGetInvitationPreviewMockHandler(pending),
      http.post("*/v1/me/invitations/accept", () =>
        HttpResponse.json(
          {
            status: 400,
            title: "Invalid",
            code: ErrorCode.RequestValidationFailed,
            traceId: "00-trace-02",
            errors: { lastName: ["too_long"] },
          },
          { status: 400 },
        ),
      ),
    );
    const user = userEvent.setup();
    renderWithProviders(<AcceptInvitationPage code={code} />);

    await user.type(
      await screen.findByRole("textbox", { name: "First name" }),
      "Ali",
    );
    await user.type(screen.getByRole("textbox", { name: "Last name" }), "Kaya");
    await user.click(
      screen.getByRole("button", { name: "Join Northwind Traders" }),
    );

    expect(
      await screen.findByText("This is too long. Shorten it."),
    ).toBeInTheDocument();
  });

  it.each([
    [InvitationResponseState.Expired, "This invitation expired"],
    [InvitationResponseState.Cancelled, "This invitation was withdrawn"],
    [InvitationResponseState.Accepted, "You already accepted this invitation"],
  ])("AcceptInvitationPage_State_%s_SaysItEnded", async (state, title) => {
    setUp();
    server.use(
      knownUser,
      getGetInvitationPreviewMockHandler({ ...pending, state }),
    );

    renderWithProviders(<AcceptInvitationPage code={code} />);

    expect(
      await screen.findByRole("heading", { name: title }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Join Northwind Traders" }),
    ).not.toBeInTheDocument();
  });

  // A finished invitation whose custom role was deleted has no role (the API's step r4b).
  it.each([
    [InvitationResponseState.Expired, "This invitation expired"],
    [InvitationResponseState.Cancelled, "This invitation was withdrawn"],
    [InvitationResponseState.Accepted, "You already accepted this invitation"],
  ])(
    "AcceptInvitationPage_State_%s_WithoutARole_SaysItEndedWithNoRoleRow",
    async (state, title) => {
      setUp();
      server.use(
        knownUser,
        getGetInvitationPreviewMockHandler({ ...pending, state, role: null }),
      );

      renderWithProviders(<AcceptInvitationPage code={code} />);

      expect(
        await screen.findByRole("heading", { name: title }),
      ).toBeInTheDocument();
      expect(screen.queryByText("Your role")).not.toBeInTheDocument();
    },
  );

  it("AcceptInvitationPage_PendingWithoutARole_ShowsNoRoleRow", async () => {
    setUp();
    server.use(
      knownUser,
      getGetInvitationPreviewMockHandler({ ...pending, role: null }),
    );

    renderWithProviders(<AcceptInvitationPage code={code} />);

    expect(
      await screen.findByRole("button", { name: "Join Northwind Traders" }),
    ).toBeInTheDocument();
    expect(screen.queryByText("Your role")).not.toBeInTheDocument();
  });

  it("AcceptInvitationPage_Pending_ShowsTheExpiryRelativeToExpiresAt", async () => {
    setUp();
    vi.useFakeTimers({
      now: new Date("2026-10-16T13:32:00Z"),
      toFake: ["Date"],
    });
    server.use(
      knownUser,
      getGetInvitationPreviewMockHandler({
        ...pending,
        expiresAt: "2026-10-19T13:32:00Z",
      }),
    );

    renderWithProviders(<AcceptInvitationPage code={code} />);

    expect(await screen.findByText("in 3 days")).toBeInTheDocument();
    vi.useRealTimers();
  });

  it.each([
    [
      {
        kind: InviterNameResponseKind.Platform,
        firstName: null,
        lastName: null,
        deleted: null,
      },
      "Ask the platform to invite you again.",
    ],
    [
      {
        kind: InviterNameResponseKind.User,
        firstName: null,
        lastName: null,
        deleted: true,
      },
      "Ask the person who invited you to invite you again.",
    ],
    [pending.inviterName, "Ask Ayşe Yılmaz to invite you again."],
  ])(
    "AcceptInvitationPage_ExpiredFromInviter_%j_NamesWhoCanInviteAgain",
    async (inviterName, ask) => {
      setUp();
      server.use(
        knownUser,
        getGetInvitationPreviewMockHandler({
          ...pending,
          inviterName,
          state: InvitationResponseState.Expired,
          expiresAt: "2026-10-09T10:00:00Z",
        }),
      );

      renderWithProviders(<AcceptInvitationPage code={code} />);

      expect(
        await screen.findByText(
          `The link to join Northwind Traders expired on 9 Oct 2026. ${ask}`,
        ),
      ).toBeInTheDocument();
    },
  );

  it("AcceptInvitationPage_UnknownCode_SaysOnlyThatTheLinkDoesNotWork", async () => {
    setUp();
    server.use(
      knownUser,
      http.post("*/v1/me/invitations/preview", () =>
        problem(404, ErrorCode.InvitationNotFound),
      ),
    );

    renderWithProviders(<AcceptInvitationPage code={code} />);

    expect(
      await screen.findByRole("heading", { name: "This link does not work" }),
    ).toBeInTheDocument();
    expect(screen.queryByText("Northwind Traders")).not.toBeInTheDocument();
  });

  it("AcceptInvitationPage_AnotherEmail_ShowsOnlyTheCallersAddressAndSwitchesAccount", async () => {
    setUp();
    server.use(
      knownUser,
      http.post("*/v1/me/invitations/preview", () =>
        problem(403, ErrorCode.InvitationEmailMismatch),
      ),
    );
    const user = userEvent.setup();
    renderWithProviders(<AcceptInvitationPage code={code} />);

    expect(
      await screen.findByRole("heading", {
        name: "This invitation is for a different email",
      }),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Switch account" }));

    expect(clerk.signOut).toHaveBeenCalledWith({ redirectUrl: signInAndBack });
    expect(sessionStorage.getItem("invitation-code")).toBe(code);
  });

  it("AcceptInvitationPage_PreviewOfADeletedUser_ShowsTheDeletedAccountPage", async () => {
    setUp();
    server.use(
      knownUser,
      http.post("*/v1/me/invitations/preview", () =>
        problem(403, ErrorCode.UserDeleted),
      ),
    );

    renderWithProviders(<AcceptInvitationPage code={code} />);

    expect(
      await screen.findByRole("heading", { name: "This account was deleted" }),
    ).toBeInTheDocument();
  });

  it("AcceptInvitationPage_JoinAsASystemAdmin_SaysItIsNotPossibleAndSwitchesAccount", async () => {
    setUp();
    server.use(
      knownUser,
      getGetInvitationPreviewMockHandler(pending),
      http.post("*/v1/me/invitations/accept", () =>
        problem(422, ErrorCode.InvitationSystemAdminCannotJoin),
      ),
    );
    const user = userEvent.setup();
    renderWithProviders(<AcceptInvitationPage code={code} />);

    await user.click(
      await screen.findByRole("button", { name: "Join Northwind Traders" }),
    );

    expect(
      await screen.findByRole("heading", {
        name: "System admins can’t join an organisation",
      }),
    ).toBeInTheDocument();
    expect(screen.getByText("Use another account.")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Switch account" }));
    expect(clerk.signOut).toHaveBeenCalledWith({ redirectUrl: signInAndBack });
    expect(sessionStorage.getItem("invitation-code")).toBe(code);
  });

  it("AcceptInvitationPage_JoinWhileTheOrganisationIsSuspended_SaysItCanJoinLater", async () => {
    setUp();
    server.use(
      knownUser,
      getGetInvitationPreviewMockHandler(pending),
      http.post("*/v1/me/invitations/accept", () =>
        problem(409, ErrorCode.TenantSuspended),
      ),
    );
    const user = userEvent.setup();
    renderWithProviders(<AcceptInvitationPage code={code} />);

    await user.click(
      await screen.findByRole("button", { name: "Join Northwind Traders" }),
    );

    expect(
      await screen.findByRole("heading", {
        name: "This organisation is suspended",
      }),
    ).toBeInTheDocument();
    expect(
      screen.getByText("You can join when it is active again."),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Join Northwind Traders" }),
    ).not.toBeInTheDocument();
  });

  it("AcceptInvitationPage_TicketWhileSignedOut_SignsUpWithTheTicketAndComesBack", async () => {
    setUp();
    clerk.isSignedIn = false;
    const signUp = {
      status: "missing_requirements",
      ticket: vi.fn(async () => {
        signUp.status = "complete";
        return { error: null };
      }),
      finalize: vi.fn(
        async ({ navigate }: { navigate: (args: unknown) => void }) => {
          navigate({ decorateUrl });
          return { error: null };
        },
      ),
    };
    const decorateUrl = vi.fn((url: string) => `#${url}`);
    clerk.signUp = signUp;

    const { container } = renderWithProviders(
      <AcceptInvitationPage code={code} ticket="ticket_abc" />,
    );

    await vi.waitFor(() => expect(signUp.finalize).toHaveBeenCalledTimes(1));
    expect(signUp.ticket).toHaveBeenCalledWith({ ticket: "ticket_abc" });
    expect(container.querySelector("#clerk-captcha")).toBeInTheDocument();
    expect(decorateUrl).toHaveBeenCalledWith("/invitations/accept");
  });

  it("AcceptInvitationPage_SignedOutWithoutATicket_KeepsTheCodeAndGoesToSignIn", async () => {
    setUp();
    clerk.isSignedIn = false;

    renderWithProviders(<AcceptInvitationPage code={code} />);

    await vi.waitFor(() =>
      expect(push.replace).toHaveBeenCalledWith(signInAndBack),
    );
    expect(sessionStorage.getItem("invitation-code")).toBe(code);
  });

  it("AcceptInvitationPage_BackFromSignInWithoutTheCode_UsesTheKeptCode", async () => {
    setUp();
    sessionStorage.setItem("invitation-code", code);
    const previewed = vi.fn();
    server.use(
      knownUser,
      getGetInvitationPreviewMockHandler(async ({ request }) => {
        previewed(await request.json());
        return pending;
      }),
    );

    renderWithProviders(<AcceptInvitationPage code="" />);

    expect(
      await screen.findByRole("button", { name: "Join Northwind Traders" }),
    ).toBeInTheDocument();
    expect(previewed).toHaveBeenCalledWith({ code });
  });

  it("AcceptInvitationPage_JoinWhileSuspended_ShowsTheSuspendedScreen", async () => {
    setUp();
    server.use(
      knownUser,
      getGetInvitationPreviewMockHandler(pending),
      http.post("*/v1/me/invitations/accept", () =>
        problem(403, ErrorCode.UserSuspended),
      ),
    );
    const user = userEvent.setup();
    renderWithProviders(<AcceptInvitationPage code={code} />);

    await user.click(
      await screen.findByRole("button", { name: "Join Northwind Traders" }),
    );

    expect(
      await screen.findByRole("heading", { name: "Your account is suspended" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText("You cannot sign in right now. Contact support."),
    ).toBeInTheDocument();
  });

  it("AcceptInvitationPage_ExpiredTicket_SaysToAskForANewInvitation", async () => {
    setUp();
    clerk.isSignedIn = false;
    clerk.signUp = {
      status: "missing_requirements",
      ticket: vi.fn(async () => ({
        error: { code: "ticket_expired_code" },
      })),
    };

    renderWithProviders(
      <AcceptInvitationPage code={code} ticket="ticket_abc" />,
    );

    expect(
      await screen.findByText(
        "This invitation link has expired. Ask for a new invitation.",
      ),
    ).toBeInTheDocument();
  });
});
