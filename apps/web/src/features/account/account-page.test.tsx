import { BuiltInRole, Language } from "@repo/api-client";
import { getGetMeResponseMock } from "@repo/api-client/msw";
import { screen, within } from "@testing-library/react";
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
import { renderInTenant, tenantAnswers } from "../../../test/tenant";
import { AccountPage } from "./account-page";

// Clerk is a system we do not own: the page's identity provider cards read a signed-in person with one session.
vi.mock("@clerk/nextjs", () => ({
  useUser: () => ({
    user: {
      externalAccounts: [],
      totpEnabled: false,
      getSessions: async () => [],
      reload: async () => {},
    },
  }),
  useSession: () => ({ session: { id: "sess_this" } }),
  useReverification: (change: unknown) => change,
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const me = getGetMeResponseMock({
  firstName: "Ayşe",
  lastName: "Yılmaz",
  email: "ayse@northwind.io",
  language: Language.En,
  timeZone: "Europe/Istanbul",
});

describe("AccountPage", () => {
  it("AccountPage_Shown_NamesThePersonAndShowsTheirAddress", async () => {
    server.use(
      http.get("*/v1/me", () => HttpResponse.json(me)),
      ...tenantAnswers(BuiltInRole.Member),
    );

    renderInTenant(<AccountPage />, { url: "/northwind/settings/account" });

    // The name is isolated from the sentence around it (F15).
    expect(await screen.findByText("Ayşe Yılmaz")).toContainHTML(
      "<bdi>Ayşe Yılmaz</bdi>",
    );
    expect(
      screen.getByText(/these settings are the same in every organisation\.$/),
    ).toBeInTheDocument();
    expect(screen.getByText("ayse@northwind.io")).toBeInTheDocument();
    const twoStep = within(
      screen.getByRole("region", { name: "Two-step verification" }),
    );
    expect(twoStep.getByText(/^Optional\./)).toBeInTheDocument();
    expect(twoStep.getByText("Off")).toBeInTheDocument();
  });
});
