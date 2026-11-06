import { Language } from "@repo/api-client";
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
import { renderWithProviders } from "../../../test/render";
import { AccountPage } from "./account-page";

// Clerk is a system we do not own: the page's identity provider cards read a signed-in system admin with one session.
// Clerk's hooks give the same objects on every render, as Clerk does.
const clerk = vi.hoisted(() => ({
  user: {
    externalAccounts: [],
    totpEnabled: true,
    getSessions: async () => [],
    reload: async () => {},
  },
  session: { id: "sess_this" },
}));
vi.mock("@clerk/nextjs", () => ({
  useUser: () => ({ user: clerk.user }),
  useSession: () => ({ session: clerk.session }),
  useReverification: (change: unknown) => change,
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const me = getGetMeResponseMock({
  name: "Ciara Byrne",
  email: "ciara@starter.dev",
  isSystemAdmin: true,
  language: Language.En,
  timeZone: "Europe/Dublin",
});

describe("AccountPage", () => {
  it("AccountPage_Shown_SaysTwoStepIsRequiredAndOn", async () => {
    server.use(http.get("*/v1/me", () => HttpResponse.json(me)));

    renderWithProviders(<AccountPage />);

    // The name is isolated from the sentence around it (F15).
    expect(await screen.findByText("Ciara Byrne")).toContainHTML(
      "<bdi>Ciara Byrne</bdi>",
    );
    expect(
      screen.getByText(
        /these settings are the same on the platform and in every organisation\.$/,
      ),
    ).toBeInTheDocument();
    expect(screen.getByText("ciara@starter.dev")).toBeInTheDocument();
    const twoStep = within(
      screen.getByRole("region", { name: "Two-step verification" }),
    );
    expect(
      twoStep.getByText(/^Required on the platform\./),
    ).toBeInTheDocument();
    expect(twoStep.getByText("On")).toBeInTheDocument();
    expect(twoStep.queryByRole("button")).toBeNull();
  });
});
