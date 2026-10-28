import {
  BuiltInRole,
  ErrorCode,
  type InvitationResponse,
  InvitationResponseState,
  Language,
} from "@repo/api-client";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { delay, HttpResponse, http } from "msw";
import { setupServer } from "msw/node";
import { createTranslator } from "next-intl";
import {
  afterAll,
  afterEach,
  beforeAll,
  describe,
  expect,
  it,
  vi,
} from "vitest";
import { messages } from "@/i18n/messages";
import {
  ayse,
  billingLead,
  countedMyTenant,
  mehmet,
  membersAnswer,
  meOf,
  northwind,
  permissionsOf,
  planProblem,
  planWith,
  problem,
  renderInTenant,
  roleOf,
  supportLead,
  tenantAnswers,
  viewerAnswers,
} from "../../../test/tenant";
import { MembersPage } from "./members-page";

// The texts of a sent invitation and of the daily limit in each language (the Messages boards). The limit's window is the API's
// setting, so no text names it; the expiry is the date the API answered.
describe("InviteDialog texts", () => {
  it.each([
    [
      "en",
      "2 Nov 2026",
      "ali@northwind.io can join until 2 Nov 2026.",
      "You have reached today’s invitation limit.",
    ],
    [
      "tr",
      "2 Kas 2026",
      "ali@northwind.io, 2 Kas 2026 tarihine kadar katılabilir.",
      "Bugünkü davet sınırına ulaştınız.",
    ],
    [
      "ar",
      "2 نوفمبر 2026",
      "يمكن لـ ali@northwind.io الانضمام حتى 2 نوفمبر 2026.",
      "لقد بلغت حد الدعوات لهذا اليوم.",
    ],
  ] as const)(
    "Texts_InLanguage_%s_SayTheDateAndNoWindow",
    (locale, date, sentText, dailyLimitText) => {
      const t = createTranslator({ locale, messages: messages[locale] });

      expect(t("members.sentText", { email: "ali@northwind.io", date })).toBe(
        sentText,
      );
      expect(t("members.dailyLimitText")).toBe(dailyLimitText);
    },
  );
});

vi.mock("@clerk/nextjs", () => ({ useClerk: () => ({ signOut: vi.fn() }) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const day = 24 * 60 * 60 * 1000;
const sent = (email: string): InvitationResponse => ({
  id: "0198f0a2-0000-7000-8000-0000000000a1",
  email,
  role: roleOf.member,
  invitedBy: ayse.userId,
  invitedByName: { firstName: "Ayşe", lastName: "Yılmaz", deleted: false },
  language: Language.En,
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
  it("Invite_Success_SendsTheInvitationAndSaysUntilWhenItWorks", async () => {
    const calls: Call[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      inviteAnswer(
        [
          () =>
            HttpResponse.json(
              {
                ...sent("khalid@northwind.io"),
                expiresAt: "2026-11-02T10:00:00Z",
              },
              { status: 201 },
            ),
        ],
        calls,
      ),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openInvite();
    await typeEmail(dialog, "khalid@northwind.io");
    await send(dialog);

    expect(await screen.findByText("Invitation sent")).toBeInTheDocument();
    expect(
      screen.getByText("khalid@northwind.io can join until 2 Nov 2026."),
    ).toBeInTheDocument();
    expect(calls.map((call) => call.body)).toEqual([
      {
        email: "khalid@northwind.io",
        roleId: roleOf.member.id,
        language: Language.En,
      },
    ]);
  });

  it("Invite_TheViewersLanguage_IsTheDefaultEmailLanguage", async () => {
    server.use(
      http.get("*/v1/me", () =>
        HttpResponse.json({
          ...meOf(BuiltInRole.Owner),
          language: Language.Tr,
        }),
      ),
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openInvite();

    expect(dialog).toHaveTextContent("Türkçe");
  });

  it("Invite_Admin_ListsTheOwnerDisabledAndSaysWhy", async () => {
    server.use(...tenantAnswers(BuiltInRole.Admin), membersAnswer());

    renderInTenant(<MembersPage />);
    const dialog = await openInvite();

    const owner = await within(dialog).findByRole("radio", { name: "Owner" });
    expect(owner).toBeDisabled();
    expect(owner).toHaveAccessibleDescription(
      expect.stringContaining("Only an Owner can invite an Owner."),
    );
    expect(within(dialog).getByRole("radio", { name: "Member" })).toBeChecked();
  });

  it("Invite_CustomRole_SendsItsIdAndShowsItsPermissionCount", async () => {
    const calls: Call[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner, permissionsOf.owner, [billingLead]),
      membersAnswer(),
      inviteAnswer(
        [() => HttpResponse.json(sent("khalid@northwind.io"), { status: 201 })],
        calls,
      ),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openInvite();
    const radio = await within(dialog).findByRole("radio", {
      name: "Billing lead",
    });
    expect(radio).toHaveAccessibleDescription("3 permissions");
    await typeEmail(dialog, "khalid@northwind.io");
    await userEvent.click(radio);
    await send(dialog);

    await waitFor(() =>
      expect(calls.map((call) => call.body)).toEqual([
        {
          email: "khalid@northwind.io",
          roleId: billingLead.id,
          language: Language.En,
        },
      ]),
    );
  });

  it("Invite_CustomRolesOff_DisablesEveryCustomRoleWithTheReasonAndKeepsTheBuiltIns", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        permissionsOf.owner,
        [billingLead],
        planWith({ "roles.custom": false, "roles.custom.max": 0 }),
      ),
      membersAnswer(),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openInvite();

    const custom = await within(dialog).findByRole("radio", {
      name: "Billing lead",
    });
    expect(custom).toBeDisabled();
    expect(custom).toHaveAccessibleDescription(
      expect.stringContaining(
        "Your organisation’s plan doesn’t include custom roles.",
      ),
    );
    for (const name of ["Owner", "Admin", "Member"])
      expect(within(dialog).getByRole("radio", { name })).not.toBeDisabled();
    expect(within(dialog).getByRole("radio", { name: "Member" })).toBeChecked();
  });

  it("Invite_RefusedLimitReached_ShowsTheReasonOfTheLimitInTheDialog", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [],
        planWith({ "members.max": 10 }, { "members.max": 9 }),
      ),
      membersAnswer(),
      inviteAnswer([
        () => planProblem(409, ErrorCode.PlanLimitReached, "members.max"),
      ]),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openInvite();
    await typeEmail(dialog, "khalid@northwind.io");
    await send(dialog);

    expect(await within(dialog).findByRole("status")).toHaveTextContent(
      "Your organisation’s plan allows 10 members, invitations included. Remove a member or cancel an invitation to invite someone.",
    );
  });

  it("Invite_RefusedFeatureUnavailable_ShowsTheReasonOfTheFeatureInTheDialog", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner, permissionsOf.owner, [billingLead]),
      membersAnswer(),
      inviteAnswer([
        () =>
          planProblem(403, ErrorCode.PlanFeatureUnavailable, "roles.custom"),
      ]),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openInvite();
    await typeEmail(dialog, "khalid@northwind.io");
    await userEvent.click(
      await within(dialog).findByRole("radio", { name: "Billing lead" }),
    );
    await send(dialog);

    const banner = await within(dialog).findByRole("status");
    expect(banner).toHaveTextContent(
      "Your organisation’s plan doesn’t include custom roles.",
    );
    expect(banner).not.toHaveTextContent("Invitation not sent");
  });

  it("Invite_RefusedLimitReachedOfAnUnknownKey_ShowsTheNeutralText", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      inviteAnswer([
        () => planProblem(409, ErrorCode.PlanLimitReached, "seats.max"),
      ]),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openInvite();
    await typeEmail(dialog, "khalid@northwind.io");
    await send(dialog);

    expect(await within(dialog).findByRole("status")).toHaveTextContent(
      "This organisation has reached a limit of its plan.",
    );
  });

  it("Invite_RefusedByThePlan_ReadsGetMyTenantAgain", async () => {
    const reads = { count: 0 };
    server.use(
      countedMyTenant(reads),
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      inviteAnswer([
        () => planProblem(409, ErrorCode.PlanLimitReached, "members.max"),
      ]),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openInvite();
    const before = reads.count;
    await typeEmail(dialog, "khalid@northwind.io");
    await send(dialog);

    await within(dialog).findByRole("status");
    await waitFor(() => expect(reads.count).toBeGreaterThan(before));
  });

  it("Invite_CustomViewerWithoutInviting_SeesNoInviteButton", async () => {
    server.use(
      ...viewerAnswers(
        supportLead,
        supportLead.permissions,
        { ...mehmet, role: supportLead },
        [supportLead, billingLead],
      ),
      membersAnswer(),
    );

    renderInTenant(<MembersPage />);

    expect(
      await screen.findByRole("table", { name: "Members" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Invite member" }),
    ).not.toBeInTheDocument();
  });

  it("Invite_AlreadyAMember_SaysSoUnderTheEmail", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      inviteAnswer([() => problem(409, ErrorCode.MemberExists)]),
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
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      inviteAnswer([
        () =>
          HttpResponse.json(
            {
              status: 400,
              title: "Invalid",
              code: ErrorCode.RequestValidationFailed,
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
      ...tenantAnswers(BuiltInRole.Owner),
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
      ErrorCode.InvitationDailyLimitReached,
      "Daily limit reached",
      "You have reached today’s invitation limit.",
    ],
    [
      403,
      ErrorCode.InvitationOwnerRequiresOwner,
      "Only an Owner can invite an Owner.",
      "Choose Admin or Member, or ask an Owner to send this invitation.",
    ],
    [
      403,
      ErrorCode.RoleEscalation,
      "This role has permissions your role doesn’t have.",
      "Choose another role, or ask an Owner.",
    ],
    [
      404,
      ErrorCode.RoleNotFound,
      "This role no longer exists.",
      "The list has been refreshed.",
    ],
  ])(
    "Invite_Refused%s_%s_ShowsTheBannerInTheDialog",
    async (status, code, title, text) => {
      server.use(
        ...tenantAnswers(BuiltInRole.Owner),
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
      ...tenantAnswers(BuiltInRole.Owner),
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
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      inviteAnswer([() => problem(409, ErrorCode.MemberExists)], calls),
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
      ...tenantAnswers(BuiltInRole.Owner),
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

  it("Invite_EnterTwiceWhileSending_SendsOnce", async () => {
    const calls: Call[] = [];
    server.use(
      // The answer takes a moment, so the second Enter comes while the first request runs.
      http.post(`*/v1/tenants/${northwind.id}/invitations`, async () => {
        await delay(200);
      }),
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      inviteAnswer(
        [() => HttpResponse.json(sent("khalid@northwind.io"), { status: 201 })],
        calls,
      ),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openInvite();
    await typeEmail(dialog, "khalid@northwind.io");
    await userEvent.keyboard("{Enter}{Enter}");

    await waitFor(() =>
      expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument(),
    );
    expect(calls).toHaveLength(1);
  });

  it("IdempotencyKey_SendAgainAfterARefusal_GetsANewKey", async () => {
    const calls: Call[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      inviteAnswer(
        [() => problem(429, ErrorCode.InvitationDailyLimitReached)],
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
    expect(calls[1]?.key).not.toBe(calls[0]?.key);
  });

  it("Invite_SuspendedUser_ReadsGetMeAgain", async () => {
    // The order of the calls: GetMe must come after the refused invitation, whatever GetMe the page read before it.
    const calls: string[] = [];
    server.use(
      http.get("*/v1/me", () => {
        calls.push("GetMe");
        return HttpResponse.json(meOf(BuiltInRole.Owner));
      }),
      http.post(`*/v1/tenants/${northwind.id}/invitations`, () => {
        calls.push("InviteMember");
        return problem(403, ErrorCode.UserSuspended);
      }),
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openInvite();
    await typeEmail(dialog, "khalid@northwind.io");
    await send(dialog);

    await waitFor(() =>
      expect(calls.slice(calls.indexOf("InviteMember"))).toEqual([
        "InviteMember",
        "GetMe",
      ]),
    );
  });
});
