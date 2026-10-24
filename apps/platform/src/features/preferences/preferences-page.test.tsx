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
import { renderWithProviders } from "../../../test/render";
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

const me = getGetMeResponseMock({
  firstName: "Ciara",
  lastName: "Byrne",
  email: "ciara@starter.dev",
  isSystemAdmin: true,
  language: Language.En,
  timeZone: "Europe/Dublin",
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

const pickTimeZone = (search: string, option: RegExp) =>
  pick("Time zone", search, option);

/** RenameMe refuses with the status and the code. */
const renameFails = (status: number, code: ErrorCode) =>
  http.patch("*/v1/me", () =>
    HttpResponse.json(
      {
        status,
        title: "Refused",
        code,
        traceId: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
      },
      { status },
    ),
  );

async function retype(name: string, text: string) {
  const box = await textbox(name);
  await userEvent.clear(box);
  await userEvent.type(box, text);
}

const save = async () =>
  userEvent.click(
    await screen.findByRole("button", { name: "Save preferences" }),
  );

const renderPage = (locale: "en" | "ar" = "en") =>
  renderWithProviders(<PreferencesPage settingsBase="/settings" />, locale);

describe("PreferencesPage", () => {
  it("PreferencesPage_GetMe_ShowsTheNameTheLanguageAndTheTimeZoneToChange", async () => {
    server.use(...preferencesAnswers(me));

    renderPage();

    expect(await textbox("First name")).toHaveValue("Ciara");
    expect(await textbox("Last name")).toHaveValue("Byrne");
    expect(await field("Language")).toHaveTextContent("English");
    expect(await field("Time zone")).toHaveTextContent(/^Dublin/);
    expect(
      screen.getByText("Your name shows in the platform’s Activity."),
    ).toBeInTheDocument();
    expect(
      screen.getByText("These follow you to every device."),
    ).toBeInTheDocument();
    expect(
      screen.queryByText(/can change your language and time zone/),
    ).not.toBeInTheDocument();
  });

  it("PreferencesPage_Email_IsReadOnly", async () => {
    server.use(...preferencesAnswers(me));

    renderPage();

    expect(await screen.findByText("ciara@starter.dev")).toBeInTheDocument();
    expect(
      screen.queryByRole("textbox", { name: "Email" }),
    ).not.toBeInTheDocument();
    expect(screen.getAllByRole("textbox")).toHaveLength(2);
  });

  it("PreferencesPage_Theme_StaysOnThePage", async () => {
    server.use(...preferencesAnswers(me));

    renderPage();

    expect(await screen.findByRole("group", { name: "Theme" })).toBeVisible();
  });

  it("PreferencesPage_NameChanged_SendsRenameMeOnlyReadsGetMeAgainAndSaysSaved", async () => {
    const sent = nothingSent();
    server.use(...preferencesAnswers(me, sent));
    renderPage();

    await retype("First name", " Ciarán ");
    await save();

    expect(await screen.findByText("Preferences saved")).toBeInTheDocument();
    expect(sent.names).toEqual([{ firstName: "Ciarán", lastName: "Byrne" }]);
    expect(sent.preferences).toEqual([]);
    await vi.waitFor(() => expect(sent.reads).toBe(2));
  });

  it("PreferencesPage_TimeZoneChanged_SendsChangePreferencesOnlyAndSaysSaved", async () => {
    const sent = nothingSent();
    server.use(...preferencesAnswers(me, sent));
    renderPage();

    await pickTimeZone("Istanbul", /^Istanbul/);
    await save();

    expect(await screen.findByText("Preferences saved")).toBeInTheDocument();
    expect(sent.preferences).toEqual([
      { language: Language.En, timeZone: "Europe/Istanbul" },
    ]);
    expect(sent.names).toEqual([]);
    expect(setLocale).not.toHaveBeenCalled();
  });

  it("PreferencesPage_NameAndTimeZoneChanged_SendsBothAndSaysSavedOnce", async () => {
    const sent = nothingSent();
    server.use(...preferencesAnswers(me, sent));
    renderPage();

    await retype("Last name", "Walsh");
    await pickTimeZone("Istanbul", /^Istanbul/);
    await save();

    expect(await screen.findAllByText("Preferences saved")).toHaveLength(1);
    expect(sent.names).toEqual([{ firstName: "Ciara", lastName: "Walsh" }]);
    expect(sent.preferences).toEqual([
      { language: Language.En, timeZone: "Europe/Istanbul" },
    ]);
    await vi.waitFor(() => expect(sent.reads).toBe(2));
  });

  it("PreferencesPage_NothingChanged_SendsNothing", async () => {
    const sent = nothingSent();
    server.use(...preferencesAnswers(me, sent));
    renderPage();

    await retype("First name", "Ciara ");
    await save();

    expect(await textbox("First name")).not.toHaveAttribute("aria-invalid");
    expect(sent.names).toEqual([]);
    expect(sent.preferences).toEqual([]);
    expect(screen.queryByText("Preferences saved")).not.toBeInTheDocument();
  });

  it.each([
    ["firstName", "First name", "Enter your first name."],
    ["lastName", "Last name", "Enter your last name."],
  ])(
    "PreferencesPage_%sOnlySpaces_SaysWhatToEnterAndSendsNothing",
    async (_json, name, message) => {
      const sent = nothingSent();
      server.use(...preferencesAnswers(me, sent));
      renderPage();

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
      renderPage();

      await retype(name, "Walsh");
      await save();

      const box = await textbox(name);
      await vi.waitFor(() => expect(box).toHaveAccessibleDescription(message));
      expect(box).toHaveAttribute("aria-invalid", "true");
    },
  );

  it("PreferencesPage_RenameMeRefusedChangePreferencesSaved_KeepsTheTimeZoneAndShowsTheNameError", async () => {
    const sent = nothingSent();
    server.use(
      renameRefuses({ firstName: ["required"] }),
      ...preferencesAnswers(me, sent),
    );
    renderPage();

    await retype("First name", "Ciarán");
    await pickTimeZone("Istanbul", /^Istanbul/);
    await save();

    const box = await textbox("First name");
    await vi.waitFor(() =>
      expect(box).toHaveAccessibleDescription("Enter your first name."),
    );
    expect(sent.preferences).toEqual([
      { language: Language.En, timeZone: "Europe/Istanbul" },
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
    renderPage();

    await retype("Last name", "Walsh");
    await pickTimeZone("Istanbul", /^Istanbul/);
    await save();

    const timeZone = await field("Time zone");
    await vi.waitFor(() =>
      expect(timeZone).toHaveAccessibleDescription(
        "Choose a time zone from the list.",
      ),
    );
    expect(sent.names).toEqual([{ firstName: "Ciara", lastName: "Walsh" }]);
    await vi.waitFor(() => expect(sent.reads).toBe(2));
  });

  it("PreferencesPage_LanguageChanged_SetsTheLocaleCookieAndRefreshes", async () => {
    const sent = nothingSent();
    server.use(...preferencesAnswers(me, sent));
    renderPage();

    await pick("Language", "Türk", /Türkçe/);
    await save();

    await vi.waitFor(() => expect(refresh).toHaveBeenCalled());
    expect(setLocale).toHaveBeenCalledWith(Language.Tr);
    expect(sent.preferences).toEqual([
      { language: Language.Tr, timeZone: "Europe/Dublin" },
    ]);
  });

  it("PreferencesPage_UserSuspended_ReadsGetMeAgain", async () => {
    const sent = nothingSent();
    server.use(
      renameFails(403, ErrorCode.UserSuspended),
      ...preferencesAnswers(me, sent),
    );
    renderPage();

    await retype("First name", "Ciarán");
    await save();

    await vi.waitFor(() => expect(sent.reads).toBe(2));
    expect(screen.queryByText("Preferences not saved")).not.toBeInTheDocument();
  });

  it("PreferencesPage_UserDeleted_ReadsGetMeAgainWithoutAToast", async () => {
    const sent = nothingSent();
    server.use(
      renameFails(403, ErrorCode.UserDeleted),
      ...preferencesAnswers(me, sent),
    );
    renderPage();

    await retype("First name", "Ciarán");
    await save();

    await vi.waitFor(() => expect(sent.reads).toBe(2));
    expect(screen.queryByText("Preferences not saved")).not.toBeInTheDocument();
  });

  it("PreferencesPage_SignedOut_GoesToSignIn", async () => {
    const assign = vi.fn();
    vi.stubGlobal("location", { ...window.location, assign });
    server.use(
      renameFails(401, ErrorCode.AuthUnauthenticated),
      ...preferencesAnswers(me),
    );
    renderPage();

    await retype("First name", "Ciarán");
    await save();

    await vi.waitFor(() => expect(assign).toHaveBeenCalledWith("/sign-in"));
    vi.unstubAllGlobals();
  });

  it("PreferencesPage_ServerError_SaysNotSaved", async () => {
    server.use(
      renameFails(500, ErrorCode.ServerError),
      ...preferencesAnswers(me),
    );
    renderPage();

    await retype("First name", "Ciarán");
    await save();

    expect(
      await screen.findByText("Preferences not saved"),
    ).toBeInTheDocument();
  });

  it("PreferencesPage_InArabic_ShowsArabicTexts", async () => {
    server.use(...preferencesAnswers({ ...me, language: Language.Ar }));

    renderPage("ar");

    expect(
      await screen.findByRole("heading", { name: "التفضيلات" }),
    ).toBeInTheDocument();
    expect(await textbox("الاسم الأول")).toHaveValue("Ciara");
    expect(screen.getByText("يظهر اسمك في نشاط المنصة.")).toBeInTheDocument();
  });
});
