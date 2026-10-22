// The one place that formats a date or a time (F18). The Intl API only: no date library. Every function takes the user's locale
// and IANA time zone; a null zone means the browser's own. Digits are Western and the calendar is Gregorian in every language
// (the design system's README, "Languages and direction").

export type Instant = string | Date;

export type DateTimeOptions = {
  /** A BCP 47 locale, such as en-IE, tr-TR or ar-SA. */
  locale: string;
  /** An IANA time zone, such as Europe/Dublin. Null or absent: the browser's zone. */
  timeZone?: string | null;
};

const base = (options: DateTimeOptions): Intl.DateTimeFormatOptions => ({
  numberingSystem: "latn",
  calendar: "gregory",
  timeZone: options.timeZone ?? undefined,
});

const toDate = (at: Instant) => (typeof at === "string" ? new Date(at) : at);

function format(
  at: Instant,
  options: DateTimeOptions,
  parts: Intl.DateTimeFormatOptions,
): string {
  return new Intl.DateTimeFormat(options.locale, {
    ...base(options),
    ...parts,
  }).format(toDate(at));
}

const datePart: Intl.DateTimeFormatOptions = {
  day: "numeric",
  month: "short",
  year: "numeric",
};
const timePart: Intl.DateTimeFormatOptions = {
  hour: "2-digit",
  minute: "2-digit",
};

/** The local date and time: `16 Oct 2026, 14:32`. */
export function formatDateTime(at: Instant, options: DateTimeOptions): string {
  return format(at, options, { ...datePart, ...timePart });
}

/** The local date: `16 Oct 2026`. */
export function formatDate(at: Instant, options: DateTimeOptions): string {
  return format(at, options, datePart);
}

/** The local time: `14:32`. */
export function formatTime(at: Instant, options: DateTimeOptions): string {
  return format(at, options, timePart);
}

/**
 * The calendar day of an instant in the zone, as `2026-10-16`: the key that groups a list by day. It changes at the zone's
 * midnight, not at UTC's. Null or absent zone: the browser's.
 */
export function calendarDay(
  at: Instant,
  options: Pick<DateTimeOptions, "timeZone">,
): string {
  const parts = new Intl.DateTimeFormat("en-US", {
    ...base({ locale: "en-US", ...options }),
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(toDate(at));
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((item) => item.type === type)?.value ?? "";
  return `${part("year")}-${part("month")}-${part("day")}`;
}

/** The calendar day before a day of `calendarDay`: `2026-03-01` gives `2026-02-28`. */
export function dayBefore(day: string): string {
  const [year = 0, month = 1, date = 1] = day.split("-").map(Number);
  return new Date(Date.UTC(year, month - 1, date - 1))
    .toISOString()
    .slice(0, 10);
}

/** The same moment in UTC: `13:32 UTC`. The screen writes "UTC", never "Z" or "GMT". */
export function formatUtc(at: Instant, options: DateTimeOptions): string {
  return `${format(at, { locale: options.locale, timeZone: "UTC" }, { ...timePart, hourCycle: "h23" })} UTC`;
}

function zonePart(
  at: Instant,
  locale: string,
  timeZone: string | undefined,
  style: "long" | "longGeneric" | "longOffset",
): string {
  const parts = new Intl.DateTimeFormat(locale, {
    timeZone,
    timeZoneName: style,
  }).formatToParts(toDate(at));
  return parts.find((part) => part.type === "timeZoneName")?.value ?? "";
}

/** The IANA name of the browser's zone, such as `Europe/Dublin`. */
export function browserTimeZone(): string {
  return new Intl.DateTimeFormat().resolvedOptions().timeZone;
}

/** The zone's offset from UTC at an instant: `UTC+1`, `UTC+5:30`, `UTC-4`. Null or absent zone: the browser's. */
export function utcOffset(at: Instant, timeZone?: string | null): string {
  // The offset is read in one fixed locale, whose format is GMT+01:00, and then written as UTC+1.
  const offset = zonePart(
    at,
    "en-US",
    timeZone ?? browserTimeZone(),
    "longOffset",
  ).replace(/^GMT/, "");
  const [, sign = "+", hours = "0", minutes] =
    /^([+-])?(\d{2})?(?::(\d{2}))?$/.exec(offset) ?? [];
  return `UTC${sign}${Number(hours)}${minutes && minutes !== "00" ? `:${minutes}` : ""}`;
}

/** The browser's name for the zone in the language, or null when it has only an offset for it. */
function namedZone(
  at: Instant,
  locale: string,
  timeZone: string,
  style: "long" | "longGeneric",
): string | null {
  const latin = `${locale}-u-nu-latn`;
  const name = zonePart(at, latin, timeZone, style);
  return name !== zonePart(at, latin, timeZone, "longOffset") ? name : null;
}

/** The city of an IANA zone: `America/Argentina/Buenos_Aires` gives `Buenos Aires`. */
const cityOf = (timeZone: string) =>
  (timeZone.split("/").pop() ?? timeZone).replaceAll("_", " ");

/**
 * The zone's name in the user's language with its offset: `Irish Standard Time (UTC+1)`. When the browser has no name for the
 * zone, it gives only an offset; the city of the IANA zone takes its place: `Istanbul (UTC+3)`.
 */
export function zoneLabel(at: Instant, options: DateTimeOptions): string {
  const timeZone = options.timeZone ?? browserTimeZone();
  const name = namedZone(at, options.locale, timeZone, "long");
  return `${name ?? cityOf(timeZone)} (${utcOffset(at, timeZone)})`;
}

/**
 * A zone as a person looks for it in a list: its city and its generic name in the user's language, `Dublin, Ireland Time`. When
 * the browser has no name for the zone, the city alone: `Istanbul`. The city is the IANA zone's, in English: the Intl API names no
 * city.
 */
export function zoneName(at: Instant, options: DateTimeOptions): string {
  const timeZone = options.timeZone ?? browserTimeZone();
  const name = namedZone(at, options.locale, timeZone, "longGeneric");
  return name ? `${cityOf(timeZone)}, ${name}` : cityOf(timeZone);
}

const minute = 60_000;
const day = 24 * 60 * minute;
const units: [Intl.RelativeTimeFormatUnit, number][] = [
  ["day", day],
  ["hour", 60 * minute],
  ["minute", minute],
];

/**
 * The time since an instant: `12 minutes ago`. Under a minute it is the language's word for now; after 30 days it is the date.
 */
export function formatRelative(
  at: Instant,
  options: DateTimeOptions & { now?: Instant },
): string {
  const elapsed =
    toDate(options.now ?? new Date()).getTime() - toDate(at).getTime();
  if (Math.abs(elapsed) > 30 * day) return formatDate(at, options);
  const relative = new Intl.RelativeTimeFormat(`${options.locale}-u-nu-latn`, {
    numeric: "auto",
  });
  if (Math.abs(elapsed) < minute) return relative.format(0, "second");
  const [unit, size] = units.find(
    ([, length]) => Math.abs(elapsed) >= length,
  ) ?? ["minute", minute];
  return relative.format(-Math.round(elapsed / size), unit);
}
