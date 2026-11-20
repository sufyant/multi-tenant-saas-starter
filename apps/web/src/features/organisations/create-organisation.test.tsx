import {
  BuiltInRole,
  type MyTenantResponse,
  type SignUpResponse,
} from "@repo/api-client";
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
import { businessPlan, freePlan, plans } from "../../../test/billing";
import { renderWithProviders } from "../../../test/render";
import {
  meOf,
  northwind,
  permissionsOf,
  problem,
  roleOf,
  rolesAnswer,
} from "../../../test/tenant";
import { CreateOrganisation } from "./create-organisation";

const push = vi.fn();
const replace = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push, replace, refresh: vi.fn() }),
}));

vi.mock("@clerk/nextjs", () => ({
  useClerk: () => ({
    on: (_: string, handler: (status: string) => void) => handler("ready"),
    off: vi.fn(),
    session: null,
  }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  push.mockClear();
  replace.mockClear();
});
afterAll(() => server.close());

/** Kestrel Analytics, the new organisation: Northwind's id, so its roles and plans answer for it. */
const kestrel: MyTenantResponse = {
  ...northwind,
  name: "Kestrel Analytics",
  slug: "kestrel-analytics",
  role: roleOf.owner,
};

const trialOn: SignUpResponse = {
  enabled: true,
  tenantLimit: 3,
  trial: { plan: { id: businessPlan.id, name: "Business" }, days: 14 },
};

const answers = (signUp: SignUpResponse = trialOn) => [
  http.get("*/v1/public/sign-up", () => HttpResponse.json(signUp)),
  http.get("*/v1/me", () =>
    HttpResponse.json({
      ...meOf(BuiltInRole.Owner),
      email: "elif@kestrel.example",
      createdTenantCount: 0,
    }),
  ),
  http.get(`*/v1/tenants/${kestrel.id}`, () =>
    HttpResponse.json({
      ...kestrel,
      createdAt: "2026-11-02T09:00:00Z",
      permissions: permissionsOf.owner,
      planName: "Business",
      entitlements: {},
      usage: {},
      billingEnabled: true,
      trial: {
        plan: { id: businessPlan.id, name: "Business" },
        startedAt: "2026-11-02T09:00:00Z",
        endsAt: "2026-11-16T09:00:00Z",
        returnsTo: { id: freePlan.id, name: freePlan.name },
      },
    }),
  ),
  http.get(`*/v1/tenants/${kestrel.id}/plans`, () =>
    HttpResponse.json({ plans }),
  ),
  rolesAnswer(),
];

const created = () =>
  http.post("*/v1/me/tenants", () =>
    HttpResponse.json(kestrel, { status: 201 }),
  );

const create = async (name = "Kestrel Analytics") => {
  await userEvent.type(await screen.findByLabelText("Name"), name);
  await userEvent.click(
    screen.getByRole("button", { name: "Create organisation" }),
  );
};

describe("CreateOrganisation", () => {
  // 1.28: the person becomes the Owner, and the trial starts with the organisation.
  it("CreateOrganisation_TrialOn_SaysTheTrialAndTheLimit", async () => {
    server.use(...answers());

    renderWithProviders(<CreateOrganisation />);

    expect(
      await screen.findByRole("heading", { name: "Create your organisation" }),
    ).toBeVisible();
    expect(
      await screen.findByText(
        "You become its Owner. Your Business trial of 14 days starts when you create it. No card needed.",
      ),
    ).toBeVisible();
    expect(
      screen.getByText("You can create up to 3 organisations."),
    ).toBeVisible();
  });

  it("CreateOrganisation_BillingOff_SaysOnlyTheOwner", async () => {
    server.use(...answers({ ...trialOn, trial: null }));

    renderWithProviders(<CreateOrganisation />);

    expect(await screen.findByText("You become its Owner.")).toBeVisible();
  });

  it("CreateOrganisation_SignUpOff_LeadsToChooseAnOrganisation", async () => {
    server.use(...answers({ ...trialOn, enabled: false }));

    renderWithProviders(<CreateOrganisation />);

    await vi.waitFor(() => expect(replace).toHaveBeenCalledWith("/"));
  });

  it("CreateOrganisation_NameTyped_FillsTheAddress", async () => {
    server.use(...answers());

    renderWithProviders(<CreateOrganisation />);
    await userEvent.type(await screen.findByLabelText("Name"), "Kestrel Ä 2");

    expect(screen.getByLabelText("Address")).toHaveValue("kestrel-a-2");
    expect(screen.getByText("app.example.test/kestrel-a-2")).toBeVisible();
  });

  // 1.29: a taken address says so under the field.
  it("CreateOrganisation_AddressTaken_SaysSoUnderTheAddress", async () => {
    server.use(
      ...answers(),
      http.post("*/v1/me/tenants", () => problem(409, "tenant.slug_taken")),
    );

    renderWithProviders(<CreateOrganisation />);
    await create();

    expect(
      await screen.findByText("This address is taken. Try another."),
    ).toBeVisible();
  });

  it("CreateOrganisation_LimitReached_SaysTheLimit", async () => {
    server.use(
      ...answers(),
      http.post("*/v1/me/tenants", () =>
        problem(409, "tenant.creation_limit_reached"),
      ),
    );

    renderWithProviders(<CreateOrganisation />);
    await create();

    expect(
      await screen.findByText(
        "You can create up to 3 organisations. You have created 3.",
      ),
    ).toBeVisible();
  });

  // 1.30: the trial has started; the team joins as Member, and the hint says what a seat costs after the trial.
  it("CreateOrganisation_CreatedInTrial_SaysTheTrialStartedAndItsPrice", async () => {
    server.use(...answers(), created());

    renderWithProviders(<CreateOrganisation />);
    await create();

    expect(
      await screen.findByRole("heading", {
        name: "Your Business trial of 14 days has started",
      }),
    ).toBeVisible();
    expect(
      await screen.findByText(
        "Kestrel Analytics has every Business feature until 16 Nov 2026. No card needed. You can choose a plan at any time in Settings › Billing.",
      ),
    ).toBeVisible();
    expect(
      await screen.findByText(
        "Separate addresses with commas. They join as Member. You can change roles later. Free during the trial. Then €8 a month per person, plus the €99 base fee, excl. VAT.",
      ),
    ).toBeVisible();
    expect(screen.getByLabelText("Invite your team")).toHaveAttribute(
      "placeholder",
      "name@kestrel.example, name@kestrel.example",
    );
  });

  it("CreateOrganisation_SendInvitations_InvitesEachAsMemberAndOpensHome", async () => {
    const invited: unknown[] = [];
    server.use(
      ...answers(),
      created(),
      http.post(
        `*/v1/tenants/${kestrel.id}/invitations`,
        async ({ request }) => {
          invited.push(await request.json());
          return HttpResponse.json({}, { status: 201 });
        },
      ),
    );

    renderWithProviders(<CreateOrganisation />);
    await create();
    await userEvent.type(
      await screen.findByLabelText("Invite your team"),
      "omar@kestrel.example, lena@kestrel.example",
    );
    await userEvent.click(
      await screen.findByRole("button", { name: "Send invitations" }),
    );

    await vi.waitFor(() =>
      expect(push).toHaveBeenCalledWith("/kestrel-analytics"),
    );
    expect(invited).toEqual([
      {
        email: "omar@kestrel.example",
        roleId: roleOf.member.id,
        language: "en",
      },
      {
        email: "lena@kestrel.example",
        roleId: roleOf.member.id,
        language: "en",
      },
    ]);
  });

  it("CreateOrganisation_AnAddressInvalid_SaysWhichOneAndSendsNothing", async () => {
    const invite = vi.fn();
    server.use(
      ...answers(),
      created(),
      http.post(`*/v1/tenants/${kestrel.id}/invitations`, () => {
        invite();
        return HttpResponse.json({}, { status: 201 });
      }),
    );

    renderWithProviders(<CreateOrganisation />);
    await create();
    await userEvent.type(
      await screen.findByLabelText("Invite your team"),
      "omar@kestrel.example, lena",
    );
    await userEvent.click(
      await screen.findByRole("button", { name: "Send invitations" }),
    );

    expect(
      await screen.findByText("lena is not an email address."),
    ).toBeVisible();
    expect(invite).not.toHaveBeenCalled();
  });

  it("CreateOrganisation_Skip_OpensHome", async () => {
    server.use(...answers(), created());

    renderWithProviders(<CreateOrganisation />);
    await create();
    await userEvent.click(
      await screen.findByRole("button", { name: "Skip for now" }),
    );

    expect(push).toHaveBeenCalledWith("/kestrel-analytics");
  });

  it("CreateOrganisation_CreatedWithBillingOff_OpensHome", async () => {
    server.use(...answers({ ...trialOn, trial: null }), created());

    renderWithProviders(<CreateOrganisation />);
    await create();

    await vi.waitFor(() =>
      expect(push).toHaveBeenCalledWith("/kestrel-analytics"),
    );
  });

  // F58: a reserved address is refused before the request, with the text of a taken one.
  it("CreateOrganisation_ReservedAddress_SaysTakenAndSendsNothing", async () => {
    const posted = vi.fn();
    server.use(
      ...answers(),
      http.post("*/v1/me/tenants", () => {
        posted();
        return HttpResponse.json(kestrel, { status: 201 });
      }),
    );

    renderWithProviders(<CreateOrganisation />);
    await userEvent.type(await screen.findByLabelText("Name"), "Settings");
    await userEvent.click(
      screen.getByRole("button", { name: "Create organisation" }),
    );

    expect(
      await screen.findByText("This address is taken. Try another."),
    ).toBeVisible();
    expect(posted).not.toHaveBeenCalled();
  });

  it("CreateOrganisation_ApiRefusesTheAddressShape_SaysTheRules", async () => {
    server.use(
      ...answers(),
      http.post("*/v1/me/tenants", () =>
        HttpResponse.json(
          {
            status: 400,
            title: "Invalid",
            code: "request.validation_failed",
            traceId: "00-trace-01",
            errors: { slug: ["slug_invalid"] },
          },
          { status: 400 },
        ),
      ),
    );

    renderWithProviders(<CreateOrganisation />);
    await create();

    expect(
      await screen.findByText(
        "Use lower-case letters, digits and single hyphens. Start and end with a letter or digit.",
      ),
    ).toBeVisible();
  });

  it("CreateOrganisation_ApiRefusesTheName_AsksForAName", async () => {
    server.use(
      ...answers(),
      http.post("*/v1/me/tenants", () => problem(422, "tenant.name_invalid")),
    );

    renderWithProviders(<CreateOrganisation />);
    await create();

    expect(await screen.findByText("Enter a name.")).toBeVisible();
  });

  // F48: the same key for a retry after a network error, a new key after a refusal the API saved.
  it("CreateOrganisation_RetryAfterANetworkErrorThenARefusal_KeepsThenRenewsTheKey", async () => {
    const keys: (string | null)[] = [];
    const replies = [
      () => HttpResponse.error(),
      () => problem(409, "tenant.slug_taken"),
      () => HttpResponse.json(kestrel, { status: 201 }),
    ];
    server.use(
      ...answers({ ...trialOn, trial: null }),
      http.post("*/v1/me/tenants", ({ request }) => {
        keys.push(request.headers.get("Idempotency-Key"));
        const reply = replies[keys.length - 1];
        return reply ? reply() : HttpResponse.json(kestrel, { status: 201 });
      }),
    );

    renderWithProviders(<CreateOrganisation />);
    await create();
    const button = screen.getByRole("button", { name: "Create organisation" });
    await vi.waitFor(() => expect(keys).toHaveLength(1));
    await vi.waitFor(() => expect(button).toBeEnabled());
    await userEvent.click(button);
    await screen.findByText("This address is taken. Try another.");
    await userEvent.click(button);
    await vi.waitFor(() => expect(keys).toHaveLength(3));

    expect(keys[1]).toBe(keys[0]);
    expect(keys[2]).not.toBe(keys[1]);
  });

  it("CreateOrganisation_InvitationRetriedAfterANetworkError_SendsTheSameKey", async () => {
    const keys: (string | null)[] = [];
    server.use(
      ...answers(),
      created(),
      http.post(`*/v1/tenants/${kestrel.id}/invitations`, ({ request }) => {
        keys.push(request.headers.get("Idempotency-Key"));
        return keys.length === 1
          ? HttpResponse.error()
          : HttpResponse.json({}, { status: 201 });
      }),
    );

    renderWithProviders(<CreateOrganisation />);
    await create();
    await userEvent.type(
      await screen.findByLabelText("Invite your team"),
      "omar@kestrel.example",
    );
    const send = await screen.findByRole("button", {
      name: "Send invitations",
    });
    await userEvent.click(send);
    await screen.findByText("Couldn’t invite omar@kestrel.example");
    await userEvent.click(send);

    await vi.waitFor(() =>
      expect(push).toHaveBeenCalledWith("/kestrel-analytics"),
    );
    expect(keys).toHaveLength(2);
    expect(keys[1]).toBe(keys[0]);
  });
});
