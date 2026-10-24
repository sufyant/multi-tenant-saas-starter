import {
  type ChangePreferencesRequest,
  ErrorCode,
  Language,
  type MeResponse,
  type RenameMeRequest,
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

/** What the page sent: the bodies of RenameMe and of ChangePreferences, and how often it read GetMe. */
type Sent = {
  names: RenameMeRequest[];
  preferences: ChangePreferencesRequest[];
  reads: number;
};
const nothingSent = (): Sent => ({ names: [], preferences: [], reads: 0 });

/** GetMe, which counts its reads, and RenameMe and ChangePreferences, which record each body and answer it back. */
function preferencesAnswers(person: MeResponse, sent: Sent = nothingSent()) {
  return [
    http.get("*/v1/me", () => {
      sent.reads += 1;
      return HttpResponse.json(person);
    }),
    http.patch("*/v1/me", async ({ request }) => {
      const body = (await request.json()) as RenameMeRequest;
      sent.names.push(body);
      return HttpResponse.json({ ...person, ...body });
    }),
    http.patch("*/v1/me/preferences", async ({ request }) => {
      const body = (await request.json()) as ChangePreferencesRequest;
      sent.preferences.push(body);
      return HttpResponse.json(body);
    }),
    ...tenantAnswers(BuiltInRole.Member),
  ];
}

/** 400 request.validation_failed with the field codes, as the API answers a refused body. */
const validationFailed = (errors: Record<string, string[]>) =>
  HttpResponse.json(
    {
      status: 400,
      title: "Refused",
      code: ErrorCode.RequestValidationFailed,
      traceId: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
      errors,
    },
    { status: 400 },
  );

const renameRefuses = (errors: Record<string, string[]>) =>
  http.patch("*/v1/me", () => validationFailed(errors));

const changeRefuses = (errors: Record<string, string[]>) =>
  http.patch("*/v1/me/preferences", () => validationFailed(errors));

/** ChangePreferences refuses with the status and the code. */
const refuses = (status: number, code: ErrorCode) =>
  http.patch("*/v1/me/preferences", () => problem(status, code));

const field = (name: string) => screen.findByRole("combobox", { name });
const textbox = (name: string) => screen.findByRole("textbox", { name });

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

async function retype(name: string, text: string) {
  const box = await textbox(name);
  await userEvent.clear(box);
  await userEvent.type(box, text);
}

const save = async () =>
  userEvent.click(
    await screen.findByRole("button", { name: "Save preferences" }),
  );

describe("PreferencesPage", () => {
  it("PreferencesPage_GetMe_ShowsTheNameTheLanguageAndTheTimeZone", async () => {
    server.use(...preferencesAnswers(me));

    renderInTenant(<PreferencesPage />, { url });

    expect(await textbox("First name")).toHaveValue("Ayşe");
    expect(await textbox("Last name")).toHaveValue("Yılmaz");
    expect(await field("Language")).toHaveTextContent("English");
    expect(await field("Time zone")).toHaveTextContent(/^Istanbul/);
    expect(
      screen.getByText("These follow you to every organisation and device."),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        "Your name shows to the members of every organisation you belong to.",
      ),
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

  it("PreferencesPage_NameChanged_SendsRenameMeOnlyReadsGetMeAgainAndSaysSaved", async () => {
    const sent = nothingSent();
    server.use(...preferencesAnswers(me, sent));
    renderInTenant(<PreferencesPage />, { url });

    await retype("First name", "  Ayla ");
    await save();

    expect(await screen.findByText("Preferences saved")).toBeInTheDocument();
    expect(sent.names).toEqual([{ firstName: "Ayla", lastName: "Yılmaz" }]);
    expect(sent.preferences).toEqual([]);
    await vi.waitFor(() => expect(sent.reads).toBe(2));
  });

  it("PreferencesPage_TimeZoneChanged_SendsChangePreferencesOnlyAndSaysSaved", async () => {
    const sent = nothingSent();
    server.use(...preferencesAnswers(me, sent));
    renderInTenant(<PreferencesPage />, { url });

    await pick("Time zone", "Riyadh", /^Riyadh/);
    await save();

    expect(await screen.findByText("Preferences saved")).toBeInTheDocument();
    expect(sent.preferences).toEqual([
      { language: Language.En, timeZone: "Asia/Riyadh" },
    ]);
    expect(sent.names).toEqual([]);
    await vi.waitFor(() => expect(sent.reads).toBe(2));
    expect(setLocale).not.toHaveBeenCalled();
  });

  it("PreferencesPage_NameAndTimeZoneChanged_SendsBothAndSaysSavedOnce", async () => {
    const sent = nothingSent();
    server.use(...preferencesAnswers(me, sent));
    renderInTenant(<PreferencesPage />, { url });

    await retype("Last name", "Kaya");
    await pick("Time zone", "Riyadh", /^Riyadh/);
    await save();

    expect(await screen.findAllByText("Preferences saved")).toHaveLength(1);
    expect(sent.names).toEqual([{ firstName: "Ayşe", lastName: "Kaya" }]);
    expect(sent.preferences).toEqual([
      { language: Language.En, timeZone: "Asia/Riyadh" },
    ]);
    await vi.waitFor(() => expect(sent.reads).toBe(2));
  });

  it("PreferencesPage_NothingChanged_SendsNothing", async () => {
    const sent = nothingSent();
    server.use(...preferencesAnswers({ ...me, timeZone: null }, sent));
    renderInTenant(<PreferencesPage />, { url });

    expect(await field("Time zone")).toHaveTextContent(/\(this device\)$/);
    await retype("First name", " Ayşe ");
    await save();

    expect(await textbox("First name")).not.toHaveAttribute("aria-invalid");
    expect(sent.names).toEqual([]);
    expect(sent.preferences).toEqual([]);
    expect(screen.queryByText("Preferences saved")).not.toBeInTheDocument();
  });

  it("PreferencesPage_LanguageChanged_SetsTheLocaleCookieAndRefreshes", async () => {
    const sent = nothingSent();
    server.use(...preferencesAnswers(me, sent));
    renderInTenant(<PreferencesPage />, { url });

    await pick("Language", "Türk", /Türkçe/);
    await save();

    await vi.waitFor(() => expect(refresh).toHaveBeenCalled());
    expect(setLocale).toHaveBeenCalledWith(Language.Tr);
    expect(sent.preferences).toEqual([
      { language: Language.Tr, timeZone: "Europe/Istanbul" },
    ]);
  });

  it.each([
    ["firstName", "First name", "Enter your first name."],
    ["lastName", "Last name", "Enter your last name."],
  ])(
    "PreferencesPage_%sOnlySpaces_SaysWhatToEnterAndSendsNothing",
    async (_json, name, message) => {
      const sent = nothingSent();
      server.use(...preferencesAnswers(me, sent));
      renderInTenant(<PreferencesPage />, { url });

      await retype(name, "   ");
      await save();

      await vi.waitFor(async () =>
        expect(await textbox(name)).toHaveAccessibleDescription(message),
      );
      expect(sent.names).toEqual([]);
    },
  );

  it.each([
    ["firstName", "required", "First name", "Enter your first name."],
    ["lastName", "required", "Last name", "Enter your last name."],
    ["firstName", "too_long", "First name", "Use 1 to 100 characters."],
    ["lastName", "too_long", "Last name", "Use 1 to 100 characters."],
  ])(
    "PreferencesPage_RenameMeRefuses%s_%s_ShowsTheTextUnderTheField",
    async (json, code, name, message) => {
      server.use(renameRefuses({ [json]: [code] }), ...preferencesAnswers(me));
      renderInTenant(<PreferencesPage />, { url });

      await retype(name, "Kaya");
      await save();

      const box = await textbox(name);
      await vi.waitFor(() => expect(box).toHaveAccessibleDescription(message));
      expect(box).toHaveAttribute("aria-invalid", "true");
    },
  );

  it("PreferencesPage_NameTooLong_SaysUse1To100CharactersAndSendsNothing", async () => {
    const sent = nothingSent();
    server.use(...preferencesAnswers(me, sent));
    renderInTenant(<PreferencesPage />, { url });

    const box = await textbox("Last name");
    await userEvent.clear(box);
    await userEvent.click(box);
    await userEvent.paste("a".repeat(101));
    await save();

    await vi.waitFor(() =>
      expect(box).toHaveAccessibleDescription("Use 1 to 100 characters."),
    );
    expect(sent.names).toEqual([]);
  });

  it("PreferencesPage_RenameMeRefusedChangePreferencesSaved_KeepsTheTimeZoneAndShowsTheNameError", async () => {
    const sent = nothingSent();
    server.use(
      renameRefuses({ lastName: ["too_long"] }),
      ...preferencesAnswers(me, sent),
    );
    renderInTenant(<PreferencesPage />, { url });

    await retype("Last name", "Kaya");
    await pick("Time zone", "Riyadh", /^Riyadh/);
    await save();

    const box = await textbox("Last name");
    await vi.waitFor(() =>
      expect(box).toHaveAccessibleDescription("Use 1 to 100 characters."),
    );
    expect(sent.preferences).toEqual([
      { language: Language.En, timeZone: "Asia/Riyadh" },
    ]);
    await vi.waitFor(() => expect(sent.reads).toBe(2));
    expect(screen.queryByText("Preferences saved")).not.toBeInTheDocument();
  });

  it("PreferencesPage_ChangePreferencesRefusedRenameMeSaved_KeepsTheNameAndShowsTheTimeZoneError", async () => {
    const sent = nothingSent();
    server.use(
      changeRefuses({ timeZone: ["time_zone_invalid"] }),
      ...preferencesAnswers(me, sent),
    );
    renderInTenant(<PreferencesPage />, { url });

    await retype("First name", "Ayla");
    await pick("Time zone", "Riyadh", /^Riyadh/);
    await save();

    const timeZone = await field("Time zone");
    await vi.waitFor(() =>
      expect(timeZone).toHaveAccessibleDescription(
        "Choose a time zone from the list.",
      ),
    );
    expect(timeZone).toHaveAttribute("aria-invalid", "true");
    expect(sent.names).toEqual([{ firstName: "Ayla", lastName: "Yılmaz" }]);
    await vi.waitFor(() => expect(sent.reads).toBe(2));
    expect(await textbox("First name")).not.toHaveAttribute("aria-invalid");
  });

  it("PreferencesPage_InArabic_ShowsArabicTexts", async () => {
    server.use(...preferencesAnswers({ ...me, language: Language.Ar }));

    renderInTenant(<PreferencesPage />, { url, locale: "ar" });

    expect(
      await screen.findByRole("heading", { name: "التفضيلات" }),
    ).toBeInTheDocument();
    expect(await field("اللغة")).toHaveTextContent("العربية");
    expect(await textbox("الاسم الأول")).toHaveValue("Ayşe");
  });

  it("PreferencesPage_UserSuspended_ReadsGetMeAgain", async () => {
    const sent = nothingSent();
    server.use(
      refuses(403, ErrorCode.UserSuspended),
      ...preferencesAnswers(me, sent),
    );
    renderInTenant(<PreferencesPage />, { url });

    await pick("Time zone", "Riyadh", /^Riyadh/);
    await save();

    await vi.waitFor(() => expect(sent.reads).toBe(2));
  });

  it("PreferencesPage_UserDeleted_ReadsGetMeAgainWithoutAToast", async () => {
    const sent = nothingSent();
    server.use(
      refuses(403, ErrorCode.UserDeleted),
      ...preferencesAnswers(me, sent),
    );
    renderInTenant(<PreferencesPage />, { url });

    await pick("Time zone", "Riyadh", /^Riyadh/);
    await save();

    await vi.waitFor(() => expect(sent.reads).toBe(2));
    expect(screen.queryByText("Preferences not saved")).not.toBeInTheDocument();
  });

  it("PreferencesPage_ServerError_SaysNotSavedWithTheReference", async () => {
    server.use(refuses(500, ErrorCode.ServerError), ...preferencesAnswers(me));
    renderInTenant(<PreferencesPage />, { url });

    await pick("Time zone", "Riyadh", /^Riyadh/);
    await save();

    expect(
      await screen.findByText("Preferences not saved"),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Ref 4bf92f3577b34da6a3ce929d0e0e4736/),
    ).toBeInTheDocument();
  });
});
