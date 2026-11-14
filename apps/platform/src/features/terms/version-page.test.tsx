import { ErrorCode, TermsPersonResponseStatus } from "@repo/api-client";
import { expectVisibleToast } from "@repo/ui/test/toast";
import { screen, within } from "@testing-library/react";
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
  peopleAnswer,
  scheduled,
  termsAnswer,
  versionAnswer,
} from "../../../test/terms";
import { VersionPage } from "./version-page";

const router = vi.hoisted(() => ({ push: vi.fn(), replace: vi.fn() }));
vi.mock("next/navigation", () => ({
  useRouter: () => router,
  usePathname: () => "/settings/terms",
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
beforeEach(() => server.use(meAnswer(), termsAnswer(), peopleAnswer()));
afterEach(() => {
  server.resetHandlers();
  router.push.mockReset();
  window.history.replaceState(null, "", "/");
});
afterAll(() => server.close());

/** The desktop's value of a row of the version's card, by its term. */
const rowValue = (term: string) =>
  within(
    screen
      .getAllByText(term, { selector: "dt span" })[0]
      ?.closest("div") as HTMLElement,
  ).getByRole("definition");

describe("VersionPage", () => {
  // 11.05: the scheduled version's values, its actions and who accepted it.
  it("VersionPage_Scheduled_ShowsItsValuesActionsAndAcceptedPeople", async () => {
    server.use(versionAnswer(scheduled));

    renderWithProviders(<VersionPage versionId={scheduled.id} />);

    expect(
      await screen.findByRole("heading", {
        level: 1,
        name: "Terms of use from 1 Dec 2026",
      }),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Scheduled · requires acceptance"),
    ).toBeInTheDocument();
    expect(rowValue("Applies from")).toHaveTextContent("1 Dec 2026");
    expect(rowValue("Summary")).toHaveTextContent(scheduled.summary as string);
    expect(rowValue("Requires acceptance")).toHaveTextContent("Yes");
    expect(rowValue("Email")).toHaveTextContent("Sent on 2 Nov 2026");
    expect(
      screen.getAllByRole("link", {
        name: /starter\.example\/terms\/2026-12/,
      })[0],
    ).toHaveAttribute("href", scheduled.url);
    expect(
      screen.getAllByRole("button", { name: "Edit version" }),
    ).toHaveLength(2);
    expect(
      screen.getByRole("tab", { name: "Accepted 18", selected: true }),
    ).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Not yet 49" })).toBeInTheDocument();
    const table = await screen.findByRole("table", { name: "Accepted" });
    expect(within(table).getByText("Ayşe Demir")).toBeInTheDocument();
    expect(within(table).getByText("2 Nov 2026, 09:14")).toBeInTheDocument();
  });

  // 11.06: Not yet lists the people who did not accept, in the URL (F9).
  it("VersionPage_NotYetTab_ListsThePeopleWhoDidNotAccept", async () => {
    const requests: URL[] = [];
    server.use(versionAnswer(scheduled), peopleAnswer(requests));
    renderWithProviders(<VersionPage versionId={scheduled.id} />);

    await userEvent.click(
      await screen.findByRole("tab", { name: "Not yet 49" }),
    );

    const table = await screen.findByRole("table", { name: "Not yet" });
    expect(within(table).getByText("Aiko Tanaka")).toBeInTheDocument();
    expect(window.location.search).toBe("?people=not_yet");
    expect(requests.at(-1)?.searchParams.get("status")).toBe(
      TermsPersonResponseStatus.NotYet,
    );
  });

  // 11.12: a version in force never changes, and says so.
  it("VersionPage_InForce_SaysItNeverChangesAndHasNoActions", async () => {
    server.use(versionAnswer(inForce));

    renderWithProviders(<VersionPage versionId={inForce.id} />);

    expect(
      await screen.findByText(
        "This version is in force. It never changes: it is the record of what people accepted. To change the terms, add a new version.",
      ),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Current · requires acceptance"),
    ).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit version" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Cancel version" })).toBeNull();
  });

  // 11.11, 11.13: Cancel version asks first, then opens the list with the toast.
  it("VersionPage_Cancel_AsksThenOpensTheListWithTheToast", async () => {
    const calls: string[] = [];
    server.use(
      versionAnswer(scheduled),
      http.post(
        `*/v1/platform/terms/versions/${scheduled.id}/cancel`,
        ({ request }) => {
          calls.push(request.url);
          return HttpResponse.json({
            ...scheduled,
            cancelledAt: "2026-11-03T09:00:00Z",
          });
        },
      ),
    );
    renderWithProviders(<VersionPage versionId={scheduled.id} />);
    await userEvent.click(
      (
        await screen.findAllByRole("button", { name: "Cancel version" })
      )[0] as HTMLElement,
    );

    const dialog = await screen.findByRole("alertdialog", {
      name: "Cancel the version from 1 Dec 2026?",
    });
    expect(dialog).toHaveAccessibleDescription(
      "It will not take effect. The version from 1 Mar 2026 stays in force. The 18 acceptances of this version are kept in its record.",
    );
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Cancel version" }),
    );

    await expectVisibleToast("Version from 1 Dec 2026 cancelled");
    expect(
      screen.getByText("The 1 Mar 2026 version stays in force."),
    ).toBeVisible();
    expect(calls).toHaveLength(1);
    expect(router.push).toHaveBeenCalledWith("/settings/terms");
  });

  it("VersionPage_CancelRefused_ShowsTheReasonInTheDialog", async () => {
    server.use(
      versionAnswer(scheduled),
      http.post(`*/v1/platform/terms/versions/${scheduled.id}/cancel`, () =>
        problem(409, ErrorCode.TermsVersionNotScheduled),
      ),
    );
    renderWithProviders(<VersionPage versionId={scheduled.id} />);
    await userEvent.click(
      (
        await screen.findAllByRole("button", { name: "Cancel version" })
      )[0] as HTMLElement,
    );
    const dialog = await screen.findByRole("alertdialog");

    await userEvent.click(
      within(dialog).getByRole("button", { name: "Cancel version" }),
    );

    expect(
      await within(dialog).findByText(
        "Only a scheduled version can change. A version in force never changes.",
      ),
    ).toBeVisible();
    expect(router.push).not.toHaveBeenCalled();
  });

  it("VersionPage_UnknownVersion_ShowsPageNotFound", async () => {
    server.use(
      http.get(`*/v1/platform/terms/versions/${scheduled.id}`, () =>
        problem(404, ErrorCode.TermsVersionNotFound),
      ),
    );

    renderWithProviders(<VersionPage versionId={scheduled.id} />);

    expect(
      await screen.findByRole("heading", { name: "Page not found" }),
    ).toBeVisible();
  });
});
