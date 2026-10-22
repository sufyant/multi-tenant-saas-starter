import {
  type ChangePreferencesRequest,
  ErrorCode,
  Language,
  type MeResponse,
} from "@repo/api-client";
import { getGetMeResponseMock } from "@repo/api-client/msw";
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
import { setLocale } from "@/i18n/set-locale";
import { BuiltInRole } from "@/shared/roles";
import { problem, renderInTenant, tenantAnswers } from "../../../test/tenant";
import { PreferencesPage } from "./preferences-page";

const refresh = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh }),
}));
vi.mock("@/i18n/set-locale", () => ({ setLocale: vi.fn(async () => {}) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
beforeEach(() => vi.clearAllMocks());
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const url = "/northwind/settings/preferences";

const me = getGetMeResponseMock({
  firstName: "Ayşe",
  lastName: "Yılmaz",
  email: "ayse@northwind.io",
  language: Language.En,
  timeZone: "Europe/Istanbul",
});

/** GetMe, which counts its reads, and ChangePreferences, which records each body and answers it back. */
function preferencesAnswers(
  person: MeResponse,
  bodies: ChangePreferencesRequest[] = [],
  reads: { count: number } = { count: 0 },
) {
  return [
    http.get("*/v1/me", () => {
      reads.count += 1;
      return HttpResponse.json(person);
    }),
    http.patch("*/v1/me/preferences", async ({ request }) => {
      const body = (await request.json()) as ChangePreferencesRequest;
      bodies.push(body);
      return HttpResponse.json(body);
    }),
    ...tenantAnswers(BuiltInRole.Member),
  ];
}

/** ChangePreferences refuses the time zone: 400 request.validation_failed with time_zone_invalid. */
const refusesTheTimeZone = () =>
  http.patch("*/v1/me/preferences", () =>
    HttpResponse.json(
      {
        status: 400,
        title: "Refused",
        code: ErrorCode.RequestValidationFailed,
        traceId: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
        errors: { timeZone: ["time_zone_invalid"] },
      },
      { status: 400 },
    ),
  );

/** ChangePreferences refuses with the status and the code. */
const refuses = (status: number, code: ErrorCode) =>
  http.patch("*/v1/me/preferences", () => problem(status, code));

const field = (name: string) => screen.findByRole("combobox", { name });

async function pick(fieldName: string, search: string, option: RegExp) {
  await userEvent.click(await field(fieldName));
  await userEvent.type(
    await screen.findByRole("combobox", {
      name:
        fieldName === "Language"
          ? "Search languages"
          : "Search cities or zones",
    }),
    search,
  );
  await userEvent.click(await screen.findByRole("option", { name: option }));
}

const save = async () =>
  userEvent.click(
    await screen.findByRole("button", { name: "Save preferences" }),
  );

describe("PreferencesPage", () => {
  it("PreferencesPage_GetMe_ShowsTheLanguageAndTheTimeZone", async () => {
    server.use(...preferencesAnswers(me));

    renderInTenant(<PreferencesPage />, { url });

    expect(await field("Language")).toHaveTextContent("English");
    expect(await field("Time zone")).toHaveTextContent(/^Istanbul/);
    expect(
      screen.getByText("These follow you to every organisation and device."),
    ).toBeInTheDocument();
  });

  it("PreferencesPage_Theme_IsNotOnThisPage", async () => {
    server.use(...preferencesAnswers(me));

    renderInTenant(<PreferencesPage />, { url });

    await field("Language");
    expect(screen.queryByText("Theme")).not.toBeInTheDocument();
    expect(screen.queryByRole("radio")).not.toBeInTheDocument();
  });

  it("PreferencesPage_LanguageOptions_AreEachInTheirOwnScriptAndDirection", async () => {
    server.use(...preferencesAnswers(me));
    renderInTenant(<PreferencesPage />, { url });

    await userEvent.click(await field("Language"));

    const arabic = await screen.findByRole("option", { name: "العربية" });
    expect(arabic.querySelector("[lang]")).toHaveAttribute("lang", "ar");
    expect(arabic.querySelector("[lang]")).toHaveAttribute("dir", "rtl");
    expect(
      screen.getByRole("option", { name: "Türkçe" }).querySelector("[lang]"),
    ).toHaveAttribute("lang", "tr");
  });

  it("PreferencesPage_TimeZoneOption_ShowsItsOffsetAndItsTime", async () => {
    server.use(...preferencesAnswers(me));
    renderInTenant(<PreferencesPage />, { url });

    await userEvent.click(await field("Time zone"));
    await userEvent.type(
      await screen.findByRole("combobox", { name: "Search cities or zones" }),
      "Riyadh",
    );

    expect(
      await screen.findByRole("option", {
        name: /^Riyadh, .+UTC\+3 · \d{2}:\d{2}$/,
      }),
    ).toBeInTheDocument();
  });

  it("PreferencesPage_Save_SendsBothValuesReadsGetMeAgainAndSaysSaved", async () => {
    const bodies: ChangePreferencesRequest[] = [];
    const reads = { count: 0 };
    server.use(...preferencesAnswers(me, bodies, reads));
    renderInTenant(<PreferencesPage />, { url });

    await pick("Time zone", "Riyadh", /^Riyadh/);
    await save();

    expect(await screen.findByText("Preferences saved")).toBeInTheDocument();
    expect(bodies).toEqual([
      { language: Language.En, timeZone: "Asia/Riyadh" },
    ]);
    await vi.waitFor(() => expect(reads.count).toBe(2));
    expect(setLocale).not.toHaveBeenCalled();
  });

  it("PreferencesPage_LanguageChanged_SetsTheLocaleCookieAndRefreshes", async () => {
    const bodies: ChangePreferencesRequest[] = [];
    server.use(...preferencesAnswers(me, bodies));
    renderInTenant(<PreferencesPage />, { url });

    await pick("Language", "Türk", /Türkçe/);
    await save();

    await vi.waitFor(() => expect(refresh).toHaveBeenCalled());
    expect(setLocale).toHaveBeenCalledWith(Language.Tr);
    expect(bodies).toEqual([
      { language: Language.Tr, timeZone: "Europe/Istanbul" },
    ]);
  });

  it("PreferencesPage_NoTimeZone_ShowsThisDevicesZoneAndSavesNull", async () => {
    const bodies: ChangePreferencesRequest[] = [];
    server.use(...preferencesAnswers({ ...me, timeZone: null }, bodies));
    renderInTenant(<PreferencesPage />, { url });

    expect(await field("Time zone")).toHaveTextContent(/\(this device\)$/);
    await save();

    await vi.waitFor(() =>
      expect(bodies).toEqual([{ language: Language.En, timeZone: null }]),
    );
  });

  it("PreferencesPage_TimeZoneInvalid_ShowsTheErrorUnderTheField", async () => {
    server.use(refusesTheTimeZone(), ...preferencesAnswers(me));
    renderInTenant(<PreferencesPage />, { url });

    await save();

    const timeZone = await field("Time zone");
    await vi.waitFor(() =>
      expect(timeZone).toHaveAccessibleDescription(
        "Choose a time zone from the list.",
      ),
    );
    expect(timeZone).toHaveAttribute("aria-invalid", "true");
  });

  it("PreferencesPage_InArabic_ShowsArabicTexts", async () => {
    server.use(...preferencesAnswers({ ...me, language: Language.Ar }));

    renderInTenant(<PreferencesPage />, { url, locale: "ar" });

    expect(
      await screen.findByRole("heading", { name: "التفضيلات" }),
    ).toBeInTheDocument();
    expect(await field("اللغة")).toHaveTextContent("العربية");
  });

  it("PreferencesPage_UserSuspended_ReadsGetMeAgain", async () => {
    const reads = { count: 0 };
    server.use(
      refuses(403, ErrorCode.UserSuspended),
      ...preferencesAnswers(me, [], reads),
    );
    renderInTenant(<PreferencesPage />, { url });

    await save();

    await vi.waitFor(() => expect(reads.count).toBe(2));
  });

  it("PreferencesPage_ServerError_SaysNotSavedWithTheReference", async () => {
    server.use(refuses(500, ErrorCode.ServerError), ...preferencesAnswers(me));
    renderInTenant(<PreferencesPage />, { url });

    await save();

    expect(
      await screen.findByText("Preferences not saved"),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Ref 4bf92f3577b34da6a3ce929d0e0e4736/),
    ).toBeInTheDocument();
  });
});
