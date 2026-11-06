import { BuiltInRole, ErrorCode } from "@repo/api-client";
import { screen, within } from "@testing-library/react";
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
import { northwind, renderInTenant, tenantAnswers } from "../../../test/tenant";
import { DeleteAccount } from "./delete-account";
import { deletedEmailKey } from "./deleted-account";

const clerk = vi.hoisted(() => ({ signOut: vi.fn(async () => {}) }));
vi.mock("@clerk/nextjs", () => ({
  useClerk: () => ({ signOut: clerk.signOut }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  sessionStorage.clear();
  vi.clearAllMocks();
});
afterAll(() => server.close());

const traceId = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
const url = "/northwind/settings/account";

/** RequestAccountDeletion and ConfirmAccountDeletion, with the answers given, recording the keys and the codes. */
function deletion(
  first: () => Response,
  confirm: () => Response = () => new HttpResponse(null, { status: 204 }),
) {
  const sent = { keys: [] as (string | null)[], codes: [] as unknown[] };
  return {
    sent,
    handlers: [
      http.post("*/v1/me/account-deletion", ({ request }) => {
        sent.keys.push(request.headers.get("Idempotency-Key"));
        return first();
      }),
      http.post("*/v1/me/account-deletion/confirm", async ({ request }) => {
        sent.codes.push(await request.json());
        return confirm();
      }),
      ...tenantAnswers(BuiltInRole.Owner),
    ],
  };
}

const codeSent = () =>
  HttpResponse.json(
    { expiresAt: new Date(Date.now() + 600_000).toISOString() },
    { status: 201 },
  );

async function start() {
  renderInTenant(<DeleteAccount email="lena@northwind.example" />, { url });
  await userEvent.click(
    await screen.findByRole("button", { name: "Delete my account" }),
  );
}

describe("DeleteAccount", () => {
  it("DeleteAccount_LastOwner_NamesTheOrganisationAndDeletesNothing", async () => {
    const { handlers, sent } = deletion(() =>
      HttpResponse.json(
        { code: ErrorCode.UserLastOwner, traceId, tenantIds: [northwind.id] },
        { status: 409 },
      ),
    );
    server.use(...handlers);

    await start();

    // The name sits in its own <bdi> (F15), so the sentence is read from the dialog's whole text.
    expect(
      await screen.findByRole("alertdialog", {
        name: "You can’t delete your account yet",
      }),
    ).toHaveTextContent("You are the last Owner of Northwind Traders.");
    expect(sent.codes).toEqual([]);
  });

  it("DeleteAccount_TheRightCode_DeletesAndOpensThePageOfTheDeletedAccount", async () => {
    const { handlers, sent } = deletion(codeSent);
    server.use(...handlers);

    await start();
    const step = within(
      await screen.findByRole("alertdialog", { name: "Delete your account?" }),
    );
    expect(step.getByText("lena@northwind.example")).toBeInTheDocument();
    await userEvent.type(step.getByRole("textbox", { name: "Code" }), "381906");
    await userEvent.click(
      step.getByRole("button", { name: "Delete my account" }),
    );

    expect(sent.keys).toEqual([expect.any(String)]);
    expect(sent.codes).toEqual([{ code: "381906" }]);
    await expect.poll(() => clerk.signOut.mock.calls.length).toBe(1);
    expect(clerk.signOut).toHaveBeenCalledWith({
      redirectUrl: "/account/deleted",
    });
    expect(sessionStorage.getItem(deletedEmailKey)).toBe(
      "lena@northwind.example",
    );
  });

  it("DeleteAccount_AWrongCode_SaysSoAndDeletesNothing", async () => {
    const { handlers } = deletion(codeSent, () =>
      HttpResponse.json(
        { code: ErrorCode.VerificationCodeInvalid, traceId },
        { status: 400 },
      ),
    );
    server.use(...handlers);

    await start();
    const step = within(
      await screen.findByRole("alertdialog", { name: "Delete your account?" }),
    );
    await userEvent.type(step.getByRole("textbox", { name: "Code" }), "000000");
    await userEvent.click(
      step.getByRole("button", { name: "Delete my account" }),
    );

    expect(
      await step.findByText(
        /That code is not right\. Check the email and try again\./,
      ),
    ).toBeInTheDocument();
    expect(clerk.signOut).not.toHaveBeenCalled();
  });

  // F15: a sentence names at most three organisations, then "n more".
  it("DeleteAccount_LastOwnerOfFour_NamesThreeThenOneMore", async () => {
    const others = ["Atlas Logistics", "Blue Harbour", "Cedar Works"].map(
      (name, index) => ({
        ...northwind,
        id: `0198f0a2-0000-7000-8000-00000000000${index + 1}`,
        name,
        slug: name.toLowerCase().replace(" ", "-"),
      }),
    );
    const mine = [northwind, ...others];
    const { handlers } = deletion(() =>
      HttpResponse.json(
        {
          code: ErrorCode.UserLastOwner,
          traceId,
          tenantIds: mine.map((tenant) => tenant.id),
        },
        { status: 409 },
      ),
    );
    server.use(
      http.get("*/v1/me/tenants", () =>
        HttpResponse.json({ items: mine, page: 1, pageSize: 100, total: 4 }),
      ),
      ...handlers,
    );

    await start();

    expect(
      await screen.findByRole("alertdialog", {
        name: "You can’t delete your account yet",
      }),
    ).toHaveTextContent(
      "You are the last Owner of Northwind Traders, Atlas Logistics, Blue Harbour and 1 more organisation.",
    );
  });

  // A failure that asks nothing of the person is no refusal: it says what went wrong in a toast and opens no dialog (F4).
  it("DeleteAccount_ServerFailure_SaysSoInAToastAndOpensNoDialog", async () => {
    const { handlers } = deletion(() =>
      HttpResponse.json({ code: "server.error", traceId }, { status: 500 }),
    );
    server.use(...handlers);

    await start();

    expect(await screen.findByText("That did not work")).toBeInTheDocument();
    expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument();
  });

  // "Send a new code" is a new intent, so it sends a new key (F48).
  it("DeleteAccount_SendANewCode_SendsANewKey", async () => {
    const { handlers, sent } = deletion(codeSent);
    server.use(...handlers);

    await start();
    const step = within(
      await screen.findByRole("alertdialog", { name: "Delete your account?" }),
    );
    await userEvent.click(
      step.getByRole("button", { name: "Send a new code" }),
    );

    await expect.poll(() => sent.keys.length).toBe(2);
    expect(sent.keys[0]).not.toBe(sent.keys[1]);
  });

  // A suspended account closes the step and reads GetMe again, so the shell shows its page (F47).
  it("DeleteAccount_ConfirmOfASuspendedAccount_ClosesTheStep", async () => {
    const { handlers } = deletion(codeSent, () =>
      HttpResponse.json(
        { code: ErrorCode.UserSuspended, traceId },
        { status: 403 },
      ),
    );
    server.use(...handlers);

    await start();
    const step = within(
      await screen.findByRole("alertdialog", { name: "Delete your account?" }),
    );
    await userEvent.type(step.getByRole("textbox", { name: "Code" }), "381906");
    await userEvent.click(
      step.getByRole("button", { name: "Delete my account" }),
    );

    await expect
      .poll(() => screen.queryByRole("alertdialog"))
      .not.toBeInTheDocument();
    expect(clerk.signOut).not.toHaveBeenCalled();
  });
});
