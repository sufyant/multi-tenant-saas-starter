// The one place that formats and reads an amount of money. An amount in the API is a whole number of its currency's minor unit
// with an ISO 4217 code (Fowler, *Patterns of Enterprise Application Architecture*, "Money"); this file turns it into text and back
// with the Intl API only. Digits are Western in every language, as in format-date-time.ts.

/** The digits after the decimal point of a currency: 2 for EUR, 0 for JPY. */
export const minorDigits = (currency: string): number =>
  new Intl.NumberFormat("und", {
    style: "currency",
    currency,
  }).resolvedOptions().maximumFractionDigits ?? 2;

/** An amount in minor units as the person reads it: "€12", "€12.50". Whole amounts show no decimals. */
export function formatMoney(
  minor: number,
  currency: string,
  locale: string,
): string {
  const digits = minorDigits(currency);
  const major = minor / 10 ** digits;
  return new Intl.NumberFormat(locale, {
    style: "currency",
    currency,
    numberingSystem: "latn",
    minimumFractionDigits: Number.isInteger(major) ? 0 : digits,
    maximumFractionDigits: digits,
  }).format(major);
}

/** The currency's sign in the locale, for a field's prefix: "€". */
export const currencySign = (currency: string, locale: string): string =>
  new Intl.NumberFormat(locale, {
    style: "currency",
    currency,
    numberingSystem: "latn",
  })
    .formatToParts(0)
    .find((part) => part.type === "currency")?.value ?? currency;

/** An amount in minor units as a field holds it: "12", "12.5" (no sign, no grouping, a point before the decimals). */
export function moneyText(minor: number, currency: string): string {
  const digits = minorDigits(currency);
  return String(minor / 10 ** digits);
}

/**
 * A field's text as minor units, or null when it is not an amount: digits, then a point or a comma and at most the currency's
 * decimals. Empty is null too; the API refuses an amount out of its range itself.
 */
export function parseMoney(text: string, currency: string): number | null {
  const digits = minorDigits(currency);
  const pattern =
    digits === 0
      ? /^(\d+)$/
      : new RegExp(`^(\\d+)(?:[.,](\\d{1,${digits}}))?$`);
  const match = pattern.exec(text.trim());
  if (!match) return null;
  const whole = match[1] ?? "0";
  const fraction = (match[2] ?? "").padEnd(digits, "0");
  return Number(whole) * 10 ** digits + Number(fraction || "0");
}
