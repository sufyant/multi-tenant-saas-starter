import { ErrorCode } from "@repo/api-client";
import { expectVisibleToast } from "@repo/ui/test/toast";
import { screen } from "@testing-library/react";
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
import { meAnswer, problem } from "../../../test/organisations";
import { renderWithProviders } from "../../../test/render";
import {
  inForce,
  scheduled,
  termsAnswer,
  versionAnswer,
} from "../../../test/terms";
import { EditVersionPage, NewVersionPage } from "./version-form";

const router = vi.hoisted(() => ({ push: vi.fn(), replace: vi.fn() }));
vi.mock("next/navigation", () => ({
  useRouter: () => router,
  usePathname: () => "/settings/terms/new",
}));
vi.mock("@clerk/nextjs", () => ({
  useClerk: () => ({
    on: (_: string, handler: (status: string) => void) => handler("ready"),
    off: vi.fn(),
    session: { getToken: async () => "token" },
  }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
beforeEach(() => server.use(meAnswer(), termsAnswer()));
afterEach(() => {
  server.resetHandlers();
  router.push.mockReset();
});
afterAll(() => server.close());

type Call = { key: string | null; body: unknown };

const createAnswer = (calls: Call[], answer: () => Response) =>
  http.post("*/v1/platform/terms/versions", async ({ request }) => {
    calls.push({
      key: request.headers.get("Idempotency-Key"),
      body: await request.json(),
    });
    return answer();
  });

/** A day of the next year, which is always today or later. */
const nextYear = `${new Date().getUTCFullYear() + 1}-12-01`;

const fill = async (url = "https://starter.example/terms/2027-12") => {
  await userEvent.type(screen.getByLabelText("Applies from"), nextYear);
  await userEvent.type(screen.getByRole("textbox", { name: "Link" }), url);
};

describe("NewVersionPage", () => {
  // 11.02: both switches start on.
  it("NewVersion_Opens_WithBothSwitchesOn", async () => {
    renderWithProviders(<NewVersionPage />);

    expect(
      await screen.findByRole("heading", { level: 1, name: "New version" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("checkbox", { name: "Requires acceptance" }),
    ).toBeChecked();
    expect(
      screen.getByRole("checkbox", { name: "Send an email" }),
    ).toBeChecked();
    expect(
      screen.getByRole("checkbox", { name: "Send an email" }),
    ).toHaveAccessibleDescription(
      "Everyone gets an email. Without acceptance, they also see a notice on Home. Turn it off for a small change.",
    );
  });

  // 11.04: Add version sends the values with a key, then the list opens with the toast.
  it("NewVersion_Add_SendsTheValuesAndOpensTheListWithTheToast", async () => {
    const calls: Call[] = [];
    server.use(
      createAnswer(calls, () =>
        HttpResponse.json({ ...scheduled, effectiveDate: nextYear }),
      ),
    );
    renderWithProviders(<NewVersionPage />);
    await fill();
    await userEvent.click(
      screen.getByRole("checkbox", { name: "Send an email" }),
    );

    await userEvent.click(screen.getByRole("button", { name: "Add version" }));

    await expectVisibleToast(/^Terms of use from 1 Dec \d{4} added$/);
    expect(
      screen.getByText("People are asked to accept them from today."),
    ).toBeVisible();
    expect(calls).toEqual([
      {
        key: expect.any(String),
        body: {
          effectiveDate: nextYear,
          url: "https://starter.example/terms/2027-12",
          summary: null,
          requiresAcceptance: true,
          sendsEmail: false,
        },
      },
    ]);
    expect(router.push).toHaveBeenCalledWith("/settings/terms");
  });

  // 11.03: a past day and a link without https are refused before they are sent.
  it("NewVersion_PastDayAndPlainLink_ShowTheFramesErrors", async () => {
    const calls: Call[] = [];
    server.use(createAnswer(calls, () => HttpResponse.json(scheduled)));
    renderWithProviders(<NewVersionPage />);
    await userEvent.type(screen.getByLabelText("Applies from"), "2020-01-01");
    await userEvent.type(
      screen.getByRole("textbox", { name: "Link" }),
      "starter.example/terms",
    );

    await userEvent.click(screen.getByRole("button", { name: "Add version" }));

    expect(
      await screen.findByText("Pick today or a later date."),
    ).toBeVisible();
    expect(
      screen.getByText("Enter a full link that starts with https://."),
    ).toBeVisible();
    expect(calls).toEqual([]);
  });

  // F48: a retry of the same values sends the same key; another value is another intent, with a new key.
  it("NewVersion_RetryThenEdit_KeepsTheKeyThenTakesANewOne", async () => {
    const calls: Call[] = [];
    let answers = 0;
    server.use(
      createAnswer(calls, () => {
        answers += 1;
        return answers === 3
          ? HttpResponse.json(scheduled)
          : problem(500, ErrorCode.ServerError);
      }),
    );
    renderWithProviders(<NewVersionPage />);
    await fill();
    const add = screen.getByRole("button", { name: "Add version" });

    await userEvent.click(add);
    await screen.findByText("Version not saved");
    await userEvent.click(add);
    await userEvent.type(
      screen.getByRole("textbox", { name: "Summary" }),
      "Clearer rules",
    );
    await userEvent.click(add);

    await expectVisibleToast("Terms of use from 1 Dec 2026 added");
    expect(calls).toHaveLength(3);
    expect(calls[1]?.key).toBe(calls[0]?.key);
    expect(calls[2]?.key).not.toBe(calls[1]?.key);
  });

  it("NewVersion_AnotherVersionScheduledMeanwhile_SaysSo", async () => {
    server.use(
      createAnswer([], () =>
        problem(409, ErrorCode.TermsVersionScheduledExists),
      ),
    );
    renderWithProviders(<NewVersionPage />);
    await fill();

    await userEvent.click(screen.getByRole("button", { name: "Add version" }));

    expect(
      await screen.findByText(
        "A version is already scheduled. Edit or cancel it first.",
      ),
    ).toBeVisible();
    expect(router.push).not.toHaveBeenCalled();
  });
});

describe("EditVersionPage", () => {
  // 11.10: after an acceptance the link and the acceptance lock; after the email, its switch locks; each says why (F52).
  it("EditVersion_AcceptedAndEmailed_LocksTheirFieldsWithTheReasons", async () => {
    server.use(versionAnswer(scheduled));

    renderWithProviders(<EditVersionPage versionId={scheduled.id} />);

    const link = await screen.findByRole("textbox", { name: "Link" });
    expect(link).toBeDisabled();
    expect(link).toHaveAccessibleDescription(
      "18 people accepted this version. To change this, cancel it and add a new version.",
    );
    expect(
      screen.getByRole("checkbox", { name: "Requires acceptance" }),
    ).toBeDisabled();
    const email = screen.getByRole("checkbox", { name: "Send an email" });
    expect(email).toBeDisabled();
    expect(email).toHaveAccessibleDescription(
      expect.stringContaining(
        "The email went out on 2 Nov 2026. It can’t be sent again.",
      ),
    );
    expect(screen.getByRole("button", { name: "Save changes" })).toBeDisabled();
    expect(
      screen.getByText(
        "You can change this version until it takes effect on 1 Dec 2026. From then on it never changes.",
      ),
    ).toBeVisible();
  });

  it("EditVersion_Save_SendsTheValuesAndOpensTheVersion", async () => {
    const bodies: unknown[] = [];
    server.use(
      versionAnswer(scheduled),
      http.patch(
        `*/v1/platform/terms/versions/${scheduled.id}`,
        async ({ request }) => {
          bodies.push(await request.json());
          return HttpResponse.json({ ...scheduled, summary: "Shorter." });
        },
      ),
    );
    renderWithProviders(<EditVersionPage versionId={scheduled.id} />);
    const summary = await screen.findByRole("textbox", { name: "Summary" });
    await userEvent.clear(summary);
    await userEvent.type(summary, "Shorter.");

    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));

    await expectVisibleToast("Version from 1 Dec 2026 saved");
    expect(bodies).toEqual([
      {
        effectiveDate: scheduled.effectiveDate,
        url: scheduled.url,
        summary: "Shorter.",
        requiresAcceptance: true,
        sendsEmail: true,
      },
    ]);
    expect(router.push).toHaveBeenCalledWith(`/settings/terms/${scheduled.id}`);
  });

  it("EditVersion_VersionInForce_SaysItNeverChanges", async () => {
    server.use(versionAnswer(inForce));

    renderWithProviders(<EditVersionPage versionId={inForce.id} />);

    expect(
      await screen.findByText(
        "Only a scheduled version can change. A version in force never changes.",
      ),
    ).toBeVisible();
    expect(screen.queryByRole("button", { name: "Save changes" })).toBeNull();
  });
});
