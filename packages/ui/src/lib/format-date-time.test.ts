import { afterEach, describe, expect, it, vi } from "vitest";
import {
  calendarDay,
  formatDate,
  formatDateTime,
  formatRelative,
  formatTime,
  formatUtc,
  zoneLabel,
} from "./format-date-time";

const at = "2026-10-16T13:32:00Z";
const dublin = { locale: "en-IE", timeZone: "Europe/Dublin" };
const istanbul = { locale: "tr-TR", timeZone: "Europe/Istanbul" };
const riyadh = { locale: "ar-SA", timeZone: "Asia/Riyadh" };

// A runtime whose ICU data has no name for a zone gives only its offset, as some do for Europe/Istanbul.
class UnnamedZoneFormat extends Intl.DateTimeFormat {
  override formatToParts(): Intl.DateTimeFormatPart[] {
    return [{ type: "timeZoneName", value: "GMT+03:00" }];
  }
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe("format-date-time", () => {
  it("FormatDateTime_EnIe_ShowsLocalTime", () => {
    expect(formatDateTime(at, dublin)).toBe("16 Oct 2026, 14:32");
  });

  it("FormatDateTime_TrTr_ShowsLocalTimeInTurkish", () => {
    expect(formatDateTime(at, istanbul)).toBe("16 Eki 2026 16:32");
  });

  it("FormatDateTime_ArSa_UsesWesternDigitsAndTheGregorianCalendar", () => {
    const text = formatDateTime(at, riyadh);

    expect(text).toContain("16");
    expect(text).toContain("2026");
    expect(text).toContain("أكتوبر");
    expect(text).not.toMatch(/[٠-٩]/);
  });

  it("FormatDate_EnIe_ShowsNoTime", () => {
    expect(formatDate(at, dublin)).toBe("16 Oct 2026");
  });

  it("FormatTime_EnIe_ShowsHoursAndMinutes", () => {
    expect(formatTime(at, dublin)).toBe("14:32");
  });

  it("FormatUtc_AnyLocale_WritesUtcNeverZOrGmt", () => {
    expect(formatUtc(at, riyadh)).toBe("13:32 UTC");
  });

  it("ZoneLabel_DublinInSummer_EndsWithItsUtcOffset", () => {
    const label = zoneLabel(at, dublin);

    expect(label).toMatch(/^\S.* \(UTC\+1\)$/);
  });

  it("ZoneLabel_DublinInWinter_EndsWithOffsetZero", () => {
    expect(zoneLabel("2026-12-03T09:14:00Z", dublin)).toMatch(
      /^\S.* \(UTC\+0\)$/,
    );
  });

  it("ZoneLabel_ZoneWithoutAName_UsesTheCityOfTheZone", () => {
    vi.spyOn(Intl, "DateTimeFormat").mockImplementation(
      UnnamedZoneFormat as never,
    );

    expect(zoneLabel(at, dublin)).toBe("Dublin (UTC+3)");
  });

  it("ZoneLabel_ArabicIstanbul_EndsWithItsOffsetInWesternDigits", () => {
    const label = zoneLabel(at, {
      locale: "ar-SA",
      timeZone: "Europe/Istanbul",
    });

    expect(label).toMatch(/^\S.* \(UTC\+3\)$/);
    expect(label).not.toMatch(/[٠-٩]/);
  });

  it("ZoneLabel_ArabicRiyadh_EndsWithItsOffsetInWesternDigits", () => {
    const label = zoneLabel(at, riyadh);

    expect(label).toMatch(/^\S.* \(UTC\+3\)$/);
    expect(label).not.toMatch(/[٠-٩]/);
  });

  it("FormatTime_NullTimeZone_UsesTheBrowsersZone", () => {
    const browserZone = Intl.DateTimeFormat().resolvedOptions().timeZone;

    expect(formatTime(at, { locale: "en-IE", timeZone: null })).toBe(
      formatTime(at, { locale: "en-IE", timeZone: browserZone }),
    );
  });

  it("FormatRelative_TwelveMinutesAgo_SaysSo", () => {
    expect(formatRelative(at, { ...dublin, now: "2026-10-16T13:44:00Z" })).toBe(
      "12 minutes ago",
    );
  });

  it("FormatRelative_UnderAMinute_SaysNow", () => {
    expect(
      formatRelative(at, { ...istanbul, now: "2026-10-16T13:32:40Z" }),
    ).toBe("şimdi");
  });

  it("FormatRelative_MoreThanThirtyDays_ShowsTheDate", () => {
    expect(formatRelative(at, { ...dublin, now: "2026-12-16T13:32:00Z" })).toBe(
      "16 Oct 2026",
    );
  });

  it("FormatRelative_ArSa_UsesWesternDigits", () => {
    expect(formatRelative(at, { ...riyadh, now: "2026-10-16T15:40:00Z" })).toBe(
      "قبل ساعتين",
    );
  });

  // F18 at its edges: under a minute the language's word for now, from a minute to 30 days the largest whole unit, then the date.
  it.each([
    ["59Seconds", "now", "2026-10-16T13:32:59Z"],
    ["1Minute", "1 minute ago", "2026-10-16T13:33:00Z"],
    ["59Minutes", "59 minutes ago", "2026-10-16T14:31:00Z"],
    ["1Hour", "1 hour ago", "2026-10-16T14:32:00Z"],
    ["30Days", "30 days ago", "2026-11-15T13:32:00Z"],
    ["31Days", "16 Oct 2026", "2026-11-16T13:32:00Z"],
  ])("FormatRelative_%sLaterInEnIe_Says %s", (_, expected, now) => {
    expect(formatRelative(at, { ...dublin, now })).toBe(expected);
  });

  it.each([
    ["59Seconds", "الآن", "2026-10-16T13:32:59Z"],
    ["1Minute", "قبل دقيقة واحدة", "2026-10-16T13:33:00Z"],
    ["59Minutes", "قبل 59 دقيقة", "2026-10-16T14:31:00Z"],
    ["1Hour", "قبل ساعة واحدة", "2026-10-16T14:32:00Z"],
    ["30Days", "قبل 30 يومًا", "2026-11-15T13:32:00Z"],
    ["31Days", "16 أكتوبر 2026", "2026-11-16T13:32:00Z"],
  ])("FormatRelative_%sLaterInArSa_Says %s", (_, expected, now) => {
    expect(formatRelative(at, { ...riyadh, now })).toBe(expected);
  });

  // The calendar day changes at the zone's midnight: 22:30 UTC is still the 15th in Dublin (UTC+1) and already the 16th in Riyadh (UTC+3).
  it.each([
    ["Europe/Dublin", "2026-10-15T22:59:00Z", "2026-10-15"],
    ["Europe/Dublin", "2026-10-15T23:00:00Z", "2026-10-16"],
    ["Asia/Riyadh", "2026-10-15T20:59:00Z", "2026-10-15"],
    ["Asia/Riyadh", "2026-10-15T21:00:00Z", "2026-10-16"],
  ])("CalendarDay_%sAt%s_Is %s", (timeZone, instant, expected) => {
    expect(calendarDay(instant, { timeZone })).toBe(expected);
  });

  it("CalendarDay_AnyZone_WritesYearMonthAndDayInWesternDigits", () => {
    expect(calendarDay(at, { timeZone: "Asia/Riyadh" })).toMatch(
      /^\d{4}-\d{2}-\d{2}$/,
    );
  });
});
