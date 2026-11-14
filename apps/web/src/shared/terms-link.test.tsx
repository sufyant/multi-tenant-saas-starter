import { getGetTermsInForceMockHandler } from "@repo/api-client/msw";
import { screen } from "@testing-library/react";
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
import { renderWithProviders } from "../../test/render";
import { TermsAuthFooter } from "./terms-link";

vi.mock("@/i18n/set-locale", () => ({ setLocale: vi.fn(async () => {}) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

describe("TermsAuthFooter", () => {
  it("TermsAuthFooter_VersionInForce_LinksItsTerms", async () => {
    server.use(
      getGetTermsInForceMockHandler({
        id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f",
        effectiveDate: "2026-06-01",
        url: "https://starter.example/terms/2026-06",
      }),
    );

    renderWithProviders(<TermsAuthFooter />);

    expect(await screen.findByRole("link", { name: "Terms" })).toHaveAttribute(
      "href",
      "https://starter.example/terms/2026-06",
    );
  });

  it("TermsAuthFooter_NoVersionInForce_ShowsOnlyPrivacy", async () => {
    const read = vi.fn();
    server.use(
      http.get("*/v1/public/terms", () => {
        read();
        return new HttpResponse(null, { status: 204 });
      }),
    );

    renderWithProviders(<TermsAuthFooter />);

    await vi.waitFor(() => expect(read).toHaveBeenCalled());
    expect(screen.getByRole("link", { name: "Privacy" })).toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: "Terms" }),
    ).not.toBeInTheDocument();
  });
});
