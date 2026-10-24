import {
  ErrorCode,
  FieldErrorCode,
  InvitationResponseState,
  Language,
} from "@repo/api-client";
import { screen, waitFor } from "@testing-library/react";
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
import { openAction } from "../../../test/organisation-page";
import {
  dateOf,
  detailAnswer,
  detailOf,
  inDays,
  liffey,
  meAnswer,
  problem,
} from "../../../test/organisations";
import { sentence } from "../../../test/render";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const open = () =>
  openAction(liffey, "Invite Owner", "Invite Owner for Liffey Design");

type Call = { key: string | null; body: unknown };

const expiresAt = inDays(7);
const sent = () =>
  HttpResponse.json(
    {
      id: "0198f0a2-0000-7000-8000-0000000000b1",
      email: "aoife@liffeydesign.example",
      role: {
        id: "0198f0a2-0000-7000-8000-0000000000c1",
        name: "Owner",
        builtIn: "owner",
      },
      invitedBy: "0198f0a2-0000-7000-8000-0000000000a9",
      invitedByName: { firstName: "Ciara", lastName: "Byrne", deleted: false },
      createdAt: new Date().toISOString(),
      expiresAt,
      state: InvitationResponseState.Pending,
      language: Language.Tr,
    },
    { status: 201 },
  );

/** InviteOwner: each call's key and body, and the answers in order (the last one repeats). */
const inviteAnswer = (answers: (() => Response)[], calls: Call[] = []) =>
  http.post(
    `*/v1/platform/tenants/${liffey.id}/invitations`,
    async ({ request }) => {
      calls.push({
        key: request.headers.get("Idempotency-Key"),
        body: await request.json(),
      });
      const answer = answers[Math.min(calls.length, answers.length) - 1];
      return answer ? answer() : HttpResponse.error();
    },
  );

const send = (dialog: Awaited<ReturnType<typeof open>>) =>
  userEvent.click(dialog.getByRole("button", { name: "Send invitation" }));

const typeEmail = async (
  dialog: Awaited<ReturnType<typeof open>>,
  email: string,
) => {
  const field = dialog.getByRole("textbox", { name: "Email" });
  await userEvent.clear(field);
  await userEvent.type(field, email);
};

describe("InviteOwnerDialog", () => {
  it("InviteOwner_LastInvitation_IsTheDefaultEmailAndLanguage", async () => {
    server.use(meAnswer(), detailAnswer(detailOf(liffey)));

    const dialog = await open();

    expect(dialog.getByRole("textbox", { name: "Email" })).toHaveValue(
      "niamh@liffeydesign.example",
    );
    expect(
      dialog.getByText(
        "The last invited address. Change it to invite someone else.",
      ),
    ).toBeVisible();
    expect(dialog.getByText("Türkçe")).toBeVisible();
  });

  it("InviteOwner_Success_ToastsTheApisDateAndReadsAgain", async () => {
    const reads = { count: 0 };
    const calls: Call[] = [];
    server.use(
      meAnswer(),
      detailAnswer(detailOf(liffey), reads),
      inviteAnswer([sent], calls),
    );

    const dialog = await open();
    await typeEmail(dialog, "aoife@liffeydesign.example");
    await send(dialog);

    expect(await screen.findByText("Invitation sent")).toBeVisible();
    expect(
      screen.getByText(
        `aoife@liffeydesign.example can join as Owner until ${dateOf(expiresAt)}.`,
      ),
    ).toBeVisible();
    expect(calls.map((call) => call.body)).toEqual([
      { email: "aoife@liffeydesign.example", language: Language.Tr },
    ]);
    expect(calls[0]?.key).toMatch(/^[0-9a-f-]{36}$/);
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("InviteOwner_RetryAfterANetworkError_SendsTheSameKey", async () => {
    const calls: Call[] = [];
    server.use(
      meAnswer(),
      detailAnswer(detailOf(liffey)),
      inviteAnswer([() => HttpResponse.error(), sent], calls),
    );

    const dialog = await open();
    await send(dialog);
    await waitFor(() => expect(calls).toHaveLength(1));
    await send(dialog);

    expect(await screen.findByText("Invitation sent")).toBeVisible();
    expect(new Set(calls.map((call) => call.key)).size).toBe(1);
  });

  it("InviteOwner_SentAgainAfterARefusal_SendsANewKey", async () => {
    const calls: Call[] = [];
    server.use(
      meAnswer(),
      detailAnswer(detailOf(liffey)),
      inviteAnswer(
        [() => problem(409, ErrorCode.TenantNotActive), sent],
        calls,
      ),
    );

    const dialog = await open();
    await send(dialog);
    await dialog.findByText(sentence("Liffey Design is not active."));
    await send(dialog);

    expect(await screen.findByText("Invitation sent")).toBeVisible();
    expect(new Set(calls.map((call) => call.key)).size).toBe(2);
  });

  it.each([
    ["KeyInProgress", () => problem(409, ErrorCode.IdempotencyKeyInProgress)],
    ["ServerError", () => problem(500, ErrorCode.ServerError)],
  ])("InviteOwner_SentAgainAfter%s_SendsTheSameKey", async (_, answer) => {
    const calls: Call[] = [];
    server.use(
      meAnswer(),
      detailAnswer(detailOf(liffey)),
      inviteAnswer([answer, sent], calls),
    );

    const dialog = await open();
    await send(dialog);
    await waitFor(() => expect(calls).toHaveLength(1));
    await dialog.findByText(sentence("Liffey Design did not change."));
    await send(dialog);

    expect(await screen.findByText("Invitation sent")).toBeVisible();
    expect(new Set(calls.map((call) => call.key)).size).toBe(1);
  });

  it("InviteOwner_HasOwner_SaysSoAndReadsAgain", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      detailAnswer(detailOf(liffey), reads),
      inviteAnswer([() => problem(409, ErrorCode.TenantHasOwner)]),
    );

    const dialog = await open();
    await send(dialog);

    expect(
      await dialog.findByText(sentence("Liffey Design already has an Owner.")),
    ).toBeVisible();
    expect(
      dialog.getByText(
        "The Owner joined just now. The page has been refreshed.",
      ),
    ).toBeVisible();
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("InviteOwner_NotActive_SaysSoAndReadsAgain", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      detailAnswer(detailOf(liffey), reads),
      inviteAnswer([() => problem(409, ErrorCode.TenantNotActive)]),
    );

    const dialog = await open();
    await send(dialog);

    expect(
      await dialog.findByText(sentence("Liffey Design is not active.")),
    ).toBeVisible();
    expect(
      dialog.getByText(sentence("Reactivate or reopen it first.")),
    ).toBeVisible();
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it.each([
    FieldErrorCode.Required,
    FieldErrorCode.EmailInvalid,
    FieldErrorCode.TooLong,
  ])("InviteOwner_%s_ShowsTheEmailsText", async (code) => {
    server.use(
      meAnswer(),
      detailAnswer(detailOf(liffey)),
      inviteAnswer([
        () =>
          problem(400, ErrorCode.RequestValidationFailed, { email: [code] }),
      ]),
    );

    const dialog = await open();
    await send(dialog);

    expect(
      await dialog.findByText(
        "Enter an email address like name@company.example.",
      ),
    ).toBeVisible();
  });
});
