import {
  ErrorCode,
  FieldErrorCode,
  Language,
  TenantResponseStatus,
} from "@repo/api-client";
import { expectVisibleToast } from "@repo/ui/test/toast";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { setupServer } from "msw/node";
import {
  afterAll,
  afterEach,
  beforeAll,
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from "vitest";
import {
  listAnswer,
  meAnswer,
  northwind,
  pageOf,
  problem,
} from "../../../test/organisations";
import { free, plansAnswer, pro } from "../../../test/plans";
import { renderWithProviders, sentence } from "../../../test/render";
import { pageOf as organisationPageOf } from "./create-organisation-dialog";
import { OrganisationsPage } from "./organisations-page";

const router = vi.hoisted(() => ({ push: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => router }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
beforeEach(() => server.use(plansAnswer()));
afterEach(() => {
  server.resetHandlers();
  router.push.mockReset();
});
afterAll(() => server.close());

const newId = "0198f0a2-0000-7000-8000-0000000000f1";

type Call = { key: string | null; body: unknown };

/** CreateTenant: each call's key and body, and the answers in order (the last one repeats). */
const createAnswer = (answers: (() => Response)[], calls: Call[] = []) =>
  http.post("*/v1/platform/tenants", async ({ request }) => {
    calls.push({
      key: request.headers.get("Idempotency-Key"),
      body: await request.json(),
    });
    const answer = answers[Math.min(calls.length, answers.length) - 1];
    return answer ? answer() : HttpResponse.error();
  });

const created = () =>
  HttpResponse.json(
    {
      id: newId,
      name: "Liffey Design",
      slug: "liffey-design",
      status: TenantResponseStatus.Active,
      suspensionReason: null,
      closedAt: null,
      purgedAt: null,
    },
    {
      status: 201,
      headers: { Location: `/api/v1/platform/tenants/${newId}` },
    },
  );

const openCreate = async () => {
  await userEvent.click(
    await screen.findByRole("button", { name: "Create organisation" }),
  );
  return within(
    await screen.findByRole("alertdialog", { name: "Create organisation" }),
  );
};

const fill = async (
  dialog: ReturnType<typeof within>,
  values: { name?: string; slug?: string; email?: string },
) => {
  const fields = [
    ["Name", values.name ?? "Liffey Design"],
    ["Slug", values.slug ?? "liffey-design"],
    ["Owner’s email", values.email ?? "niamh@liffeydesign.example"],
  ] as const;
  for (const [label, value] of fields) {
    const field = dialog.getByRole("textbox", { name: label });
    await userEvent.clear(field);
    if (value) await userEvent.type(field, value);
  }
};

const send = (dialog: ReturnType<typeof within>) =>
  userEvent.click(
    dialog.getByRole("button", { name: "Create and invite Owner" }),
  );

describe("CreateOrganisationDialog", () => {
  it("Create_Success_ToastsAndOpensThePageFromLocation", async () => {
    const calls: Call[] = [];
    server.use(
      meAnswer(Language.Tr),
      listAnswer(pageOf([northwind])),
      createAnswer([created], calls),
    );

    renderWithProviders(<OrganisationsPage />);
    const dialog = await openCreate();
    await fill(dialog, {});
    await send(dialog);

    await expectVisibleToast(sentence("Liffey Design created"));
    expect(
      screen.getByText("niamh@liffeydesign.example was invited as Owner."),
    ).toBeVisible();
    expect(router.push).toHaveBeenCalledWith(`/organisations/${newId}`);
    expect(calls.map((call) => call.body)).toEqual([
      {
        name: "Liffey Design",
        slug: "liffey-design",
        ownerEmail: "niamh@liffeydesign.example",
        language: Language.Tr,
        planId: free.id,
      },
    ]);
    expect(calls[0]?.key).toMatch(/^[0-9a-f-]{36}$/);
  });

  it("PageOf_LocationOfGetTenant_IsTheOrganisationsPage", () => {
    expect(organisationPageOf(`/api/v1/platform/tenants/${newId}`)).toBe(
      `/organisations/${newId}`,
    );
  });

  it("Create_RetryAfterANetworkError_SendsTheSameKey", async () => {
    const calls: Call[] = [];
    server.use(
      meAnswer(),
      listAnswer(pageOf([northwind])),
      createAnswer([() => HttpResponse.error(), created], calls),
    );

    renderWithProviders(<OrganisationsPage />);
    const dialog = await openCreate();
    await fill(dialog, {});
    await send(dialog);
    await waitFor(() => expect(calls).toHaveLength(1));
    await send(dialog);

    await expectVisibleToast(sentence("Liffey Design created"));
    expect(new Set(calls.map((call) => call.key)).size).toBe(1);
  });

  it("Create_EmptyName_SaysEnterANameAndSendsNothing", async () => {
    const calls: Call[] = [];
    server.use(
      meAnswer(),
      listAnswer(pageOf([northwind])),
      createAnswer([created], calls),
    );

    renderWithProviders(<OrganisationsPage />);
    const dialog = await openCreate();
    await fill(dialog, { name: "   " });
    await send(dialog);

    expect(await dialog.findByText("Enter a name.")).toBeVisible();
    expect(calls).toEqual([]);
  });

  it.each([
    [
      "TenantNameInvalid",
      () => problem(400, ErrorCode.TenantNameInvalid),
      "Enter a name.",
    ],
    [
      "NameTooLong",
      () =>
        problem(400, ErrorCode.RequestValidationFailed, {
          name: [FieldErrorCode.TooLong],
        }),
      "Use 1 to 100 characters.",
    ],
    [
      "SlugInvalid",
      () =>
        problem(400, ErrorCode.RequestValidationFailed, {
          slug: [FieldErrorCode.SlugInvalid],
        }),
      "Use lower-case letters, digits and single hyphens. Start and end with a letter or digit.",
    ],
    [
      "EmailInvalid",
      () =>
        problem(400, ErrorCode.RequestValidationFailed, {
          ownerEmail: [FieldErrorCode.EmailInvalid],
        }),
      "Enter an email address like name@company.example.",
    ],
    [
      "SlugTaken",
      () => problem(409, ErrorCode.TenantSlugTaken),
      "This slug is taken. Try another.",
    ],
  ])("Create_%s_ShowsTheFieldsText", async (_, answer, text) => {
    server.use(
      meAnswer(),
      listAnswer(pageOf([northwind])),
      createAnswer([answer]),
    );

    renderWithProviders(<OrganisationsPage />);
    const dialog = await openCreate();
    await fill(dialog, {});
    await send(dialog);

    expect(await dialog.findByText(text)).toBeVisible();
    expect(router.push).not.toHaveBeenCalled();
  });

  it("Create_SuspendedUser_ReadsGetMeAgain", async () => {
    const reads = { count: 0 };
    server.use(
      http.get("*/v1/me", () => {
        reads.count += 1;
        return HttpResponse.json({
          id: "0198f0a2-0000-7000-8000-0000000000a9",
          email: "ciara@platform.example",
          firstName: "Ciara",
          lastName: "Byrne",
          isSystemAdmin: true,
          language: Language.En,
          timeZone: "Europe/Dublin",
        });
      }),
      listAnswer(pageOf([northwind])),
      createAnswer([() => problem(403, ErrorCode.UserSuspended)]),
    );

    renderWithProviders(<OrganisationsPage />);
    const dialog = await openCreate();
    await fill(dialog, {});
    const before = reads.count;
    await send(dialog);

    await waitFor(() => expect(reads.count).toBe(before + 1));
  });

  it("Create_SlugTakenThenAnotherSlug_SendsANewKey", async () => {
    const calls: Call[] = [];
    server.use(
      meAnswer(),
      listAnswer(pageOf([northwind])),
      createAnswer(
        [() => problem(409, ErrorCode.TenantSlugTaken), created],
        calls,
      ),
    );

    renderWithProviders(<OrganisationsPage />);
    const dialog = await openCreate();
    await fill(dialog, {});
    await send(dialog);
    await dialog.findByText("This slug is taken. Try another.");
    await fill(dialog, { slug: "liffey" });
    await send(dialog);

    await expectVisibleToast(sentence("Liffey Design created"));
    expect(new Set(calls.map((call) => call.key)).size).toBe(2);
  });
  it("Create_Plan_PreselectsTheDefaultPlanAndMarksIt", async () => {
    server.use(meAnswer(), listAnswer(pageOf([northwind])));

    renderWithProviders(<OrganisationsPage />);
    const dialog = await openCreate();

    const field = dialog.getByRole("combobox", { name: "Plan" });
    await waitFor(() => expect(field).toHaveTextContent("Free"));
    expect(field).toHaveAccessibleDescription(
      "New organisations get the default plan. You can change it later.",
    );
    await userEvent.click(field);
    expect(
      await screen.findByRole("option", { name: /Free.*Default/ }),
    ).toBeVisible();
  });

  it("Create_AnotherPlan_SendsItsId", async () => {
    const calls: Call[] = [];
    server.use(
      meAnswer(),
      listAnswer(pageOf([northwind])),
      createAnswer([created], calls),
    );

    renderWithProviders(<OrganisationsPage />);
    const dialog = await openCreate();
    await fill(dialog, {});
    await userEvent.click(dialog.getByRole("combobox", { name: "Plan" }));
    await userEvent.click(await screen.findByRole("option", { name: /^Pro/ }));
    await send(dialog);

    await waitFor(() => expect(calls).toHaveLength(1));
    expect(calls[0]?.body).toMatchObject({ planId: pro.id });
  });

  it("Create_PlanNotFound_ShowsItUnderThePlanAndReadsThePlansAgain", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      meAnswer(),
      listAnswer(pageOf([northwind])),
      plansAnswer(undefined, requests),
      createAnswer([() => problem(404, ErrorCode.PlanNotFound)]),
    );

    renderWithProviders(<OrganisationsPage />);
    const dialog = await openCreate();
    await fill(dialog, {});
    await waitFor(() =>
      expect(dialog.getByRole("combobox", { name: "Plan" })).toHaveTextContent(
        "Free",
      ),
    );
    const before = requests.length;
    await send(dialog);

    expect(
      await dialog.findByText("This plan no longer exists."),
    ).toBeVisible();
    await waitFor(() => expect(requests.length).toBeGreaterThan(before));
  });
});
