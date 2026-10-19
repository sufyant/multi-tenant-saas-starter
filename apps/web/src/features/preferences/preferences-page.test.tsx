import { Language, MemberRole } from "@repo/api-client";
import {
  getGetMeMockHandler,
  getGetMeResponseMock,
} from "@repo/api-client/msw";
import { screen } from "@testing-library/react";
import { setupServer } from "msw/node";
import { afterAll, afterEach, beforeAll, describe, expect, it } from "vitest";
import { renderInTenant, tenantAnswers } from "../../../test/tenant";
import { PreferencesPage } from "./preferences-page";

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const me = getGetMeResponseMock({
  firstName: "Ayşe",
  lastName: "Yılmaz",
  email: "ayse@northwind.io",
  language: Language.Tr,
  timeZone: "Europe/Istanbul",
});

describe("PreferencesPage", () => {
  it("PreferencesPage_GetMe_ShowsNameEmailLanguageAndTimeZone", async () => {
    server.use(getGetMeMockHandler(me), ...tenantAnswers(MemberRole.Member));

    renderInTenant(<PreferencesPage />);

    expect(await screen.findByText("Ayşe Yılmaz")).toBeInTheDocument();
    expect(screen.getByText("ayse@northwind.io")).toBeInTheDocument();
    expect(screen.getByText("Türkçe")).toBeInTheDocument();
    expect(screen.getByText("Europe/Istanbul")).toBeInTheDocument();
  });

  it("PreferencesPage_NoTimeZone_SaysTheDevicesZone", async () => {
    server.use(
      getGetMeMockHandler({ ...me, timeZone: null }),
      ...tenantAnswers(MemberRole.Member),
    );

    renderInTenant(<PreferencesPage />);

    expect(
      await screen.findByText("Not set: this device's time zone"),
    ).toBeInTheDocument();
  });

  it("PreferencesPage_InArabic_ShowsArabicTexts", async () => {
    server.use(getGetMeMockHandler(me), ...tenantAnswers(MemberRole.Member));

    renderInTenant(<PreferencesPage />, { locale: "ar" });

    expect(
      await screen.findByRole("heading", { name: "التفضيلات" }),
    ).toBeInTheDocument();
  });
});
