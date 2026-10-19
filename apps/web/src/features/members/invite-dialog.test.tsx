import {
  type InvitationResponse,
  InvitationResponseState,
  Language,
  MemberRole,
} from "@repo/api-client";
import { screen, waitFor, within } from "@testing-library/react";
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
import {
  ayse,
  membersAnswer,
  meOf,
  northwind,
  problem,
  renderInTenant,
  tenantAnswers,
} from "../../../test/tenant";
import { MembersPage } from "./members-page";

vi.mock("@clerk/nextjs", () => ({ useClerk: () => ({ signOut: vi.fn() }) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const day = 24 * 60 * 60 * 1000;
const sent = (email: string): InvitationResponse => ({
  id: "0198f0a2-0000-7000-8000-0000000000a1",
  email,
  role: { builtIn: MemberRole.Member, name: MemberRole.Member },
  invitedBy: ayse.userId,
  invitedByName: { firstName: "Ayşe", lastName: "Yılmaz", deleted: false },
  createdAt: new Date().toISOString(),
  expiresAt: new Date(Date.now() + 7 * day).toISOString(),
  state: InvitationResponseState.Pending,
});

type Call = { key: string | null; body: unknown };

/** InviteMember: each call's key and body, and the answers in order (the last one repeats). */
const inviteAnswer = (answers: (() => Response)[], calls: Call[] = []) =>
  http.post(`*/v1/tenants/${northwind.id}/invitations`, async ({ request }) => {
    calls.push({
      key: request.headers.get("Idempotency-Key"),
      body: await request.json(),
    });
    const answer = answers[Math.min(calls.length, answers.length) - 1];
    return answer ? answer() : HttpResponse.error();
  });

const openInvite = async () => {
  await userEvent.click(
    await screen.findByRole("button", { name: "Invite member" }),
  );
  return screen.findByRole("alertdialog", { name: "Invite a member" });
};

const send = (dialog: HTMLElement) =>
  userEvent.click(
    within(dialog).getByRole("button", { name: "Send invitation" }),
  );

const typeEmail = async (dialog: HTMLElement, email: string) => {
  const field = within(dialog).getByRole("textbox", { name: "Email" });
  await userEvent.clear(field);
  await userEvent.type(field, email);
};

describe("InviteDialog", () => {
  it("Invite_Success_SendsTheInvitationAndSaysHowLongItLasts", async () => {
    const calls: Call[] = [];
    server.use(
      ...tenantAnswers(MemberRole.Owner),
      membersAnswer(),
      inviteAnswer(
        [() => HttpResponse.json(sent("khalid@northwind.io"), { status: 201 })],
        calls,
      ),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openInvite();
    await typeEmail(dialog, "khalid@northwind.io");
    await send(dialog);

    expect(await screen.findByText("Invitation sent")).toBeInTheDocument();
    expect(
      screen.getByText("khalid@northwind.io has 7 days to join."),
    ).toBeInTheDocument();
    expect(calls.map((call) => call.body)).toEqual([
      {
        email: "khalid@northwind.io",
        role: MemberRole.Member,
        language: Language.En,
      },
    ]);
  });

  it("Invite_TheViewersLanguage_IsTheDefaultEmailLanguage", async () => {
    server.use(
      http.get("*/v1/me", () =>
        HttpResponse.json({ ...meOf(MemberRole.Owner), language: Language.Tr }),
      ),
      ...tenantAnswers(MemberRole.Owner),
      membersAnswer(),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openInvite();

    expect(dialog).toHaveTextContent("Türkçe");
  });

  it("Invite_Admin_OffersNoOwnerAndSaysWhy", async () => {
    server.use(...tenantAnswers(MemberRole.Admin), membersAnswer());

    renderInTenant(<MembersPage />);
    const dialog = await openInvite();
    await userEvent.click(
      within(dialog).getByRole("combobox", { name: "Role" }),
    );

    expect(
      await screen.findByRole("option", { name: "Admin" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("option", { name: "Owner" }),
    ).not.toBeInTheDocument();
    expect(dialog).toHaveTextContent("Only an Owner can invite an Owner.");
  });

  it("Invite_AlreadyAMember_SaysSoUnderTheEmail", async () => {
    server.use(
      ...tenantAnswers(MemberRole.Owner),
      membersAnswer(),
      inviteAnswer([() => problem(409, "member.exists")]),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openInvite();
    await typeEmail(dialog, "john@northwind.io");
    await send(dialog);

    expect(
      await within(dialog).findByText(
        "john@northwind.io is already a member of Northwind Traders.",
      ),
    ).toBeInTheDocument();
  });

  it("Invite_ApiRefusesTheEmail_ShowsTheFieldError", async () => {
    server.use(
      ...tenantAnswers(MemberRole.Owner),
      membersAnswer(),
      inviteAnswer([
        () =>
          HttpResponse.json(
            {
              status: 400,
              title: "Invalid",
              code: "request.validation_failed",
              traceId: "00-trace-01",
              errors: { email: ["email_invalid"] },
            },
            { status: 400 },
          ),
      ]),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openInvite();
    await typeEmail(dialog, "khalid@northwind.io");
    await send(dialog);

    expect(
      await within(dialog).findByText(
        "Enter an email address like name@company.com.",
      ),
    ).toBeInTheDocument();
  });

  it("Invite_NoEmail_ShowsTheZodErrorAndSendsNothing", async () => {
    const calls: Call[] = [];
    server.use(
      ...tenantAnswers(MemberRole.Owner),
      membersAnswer(),
      inviteAnswer([], calls),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openInvite();
    await send(dialog);

    expect(
      await within(dialog).findByText(
        "Enter an email address like name@company.com.",
      ),
    ).toBeInTheDocument();
    expect(calls).toEqual([]);
  });

  it.each([
    [
      429,
      "invitation.daily_limit_reached",
      "Daily limit reached",
      "Northwind Traders has sent the most invitations allowed in the last 24 hours. Try again later.",
    ],
    [
      403,
      "invitation.owner_requires_owner",
      "Only an Owner can invite an Owner.",
      "Choose Admin or Member, or ask an Owner to send this invitation.",
    ],
  ])(
    "Invite_Refused%s_%s_ShowsTheBannerInTheDialog",
    async (status, code, title, text) => {
      server.use(
        ...tenantAnswers(MemberRole.Owner),
        membersAnswer(),
        inviteAnswer([() => problem(status, code)]),
      );

      renderInTenant(<MembersPage />);
      const dialog = await openInvite();
      await typeEmail(dialog, "khalid@northwind.io");
      await send(dialog);

      const banner = await within(dialog).findByRole("status");
      expect(banner).toHaveTextContent(title);
      expect(banner).toHaveTextContent(text);
    },
  );

  it("IdempotencyKey_RetryAfterANetworkError_KeepsTheKey", async () => {
    const calls: Call[] = [];
    server.use(
      ...tenantAnswers(MemberRole.Owner),
      membersAnswer(),
      inviteAnswer(
        [
          () => HttpResponse.error(),
          () => HttpResponse.json(sent("khalid@northwind.io"), { status: 201 }),
        ],
        calls,
      ),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openInvite();
    await typeEmail(dialog, "khalid@northwind.io");
    await send(dialog);
    await within(dialog).findByRole("status");
    await send(dialog);

    await waitFor(() => expect(calls).toHaveLength(2));
    expect(calls[1]?.key).toBe(calls[0]?.key);
  });

  it("IdempotencyKey_AnEditAfterARefusal_GetsANewKey", async () => {
    const calls: Call[] = [];
    server.use(
      ...tenantAnswers(MemberRole.Owner),
      membersAnswer(),
      inviteAnswer([() => problem(409, "member.exists")], calls),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openInvite();
    await typeEmail(dialog, "john@northwind.io");
    await send(dialog);
    await within(dialog).findByText(/already a member/);
    await typeEmail(dialog, "khalid@northwind.io");
    await send(dialog);

    await waitFor(() => expect(calls).toHaveLength(2));
    expect(calls[1]?.key).not.toBe(calls[0]?.key);
  });

  it("IdempotencyKey_AfterASuccess_TheNextInvitationGetsANewKey", async () => {
    const calls: Call[] = [];
    server.use(
      ...tenantAnswers(MemberRole.Owner),
      membersAnswer(),
      inviteAnswer(
        [() => HttpResponse.json(sent("khalid@northwind.io"), { status: 201 })],
        calls,
      ),
    );

    renderInTenant(<MembersPage />);
    const first = await openInvite();
    await typeEmail(first, "khalid@northwind.io");
    await send(first);
    await waitFor(() =>
      expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument(),
    );
    const second = await openInvite();
    await typeEmail(second, "khalid@northwind.io");
    await send(second);

    await waitFor(() => expect(calls).toHaveLength(2));
    expect(calls[1]?.key).not.toBe(calls[0]?.key);
  });
});
