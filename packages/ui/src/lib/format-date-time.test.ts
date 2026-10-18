import { afterEach, describe, expect, it, vi } from "vitest";
import {
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
});
