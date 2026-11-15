import { describe, expect, it } from "vitest";
import {
  currencySign,
  formatMoney,
  minorDigits,
  moneyText,
  parseMoney,
} from "./format-money";

describe("format-money", () => {
  it.each([
    ["EUR", 2],
    ["JPY", 0],
  ])("MinorDigits_%s_Is%s", (currency, digits) => {
    expect(minorDigits(currency)).toBe(digits);
  });

  it.each([
    [1200, "en", "€12"],
    [1250, "en", "€12.50"],
    [0, "en", "€0"],
    [1200, "ar", "\u200f12 €"],
  ])("FormatMoney_%s_In%s_Is%s", (minor, locale, text) => {
    expect(formatMoney(minor, "EUR", locale).replace(/\s/g, " ")).toBe(text);
  });

  it("CurrencySign_Eur_IsTheEuroSign", () => {
    expect(currencySign("EUR", "en")).toBe("€");
  });

  it.each([
    [1200, "12"],
    [1250, "12.5"],
  ])("MoneyText_%s_Is%s", (minor, text) => {
    expect(moneyText(minor, "EUR")).toBe(text);
  });

  it.each([
    ["12", 1200],
    ["12.5", 1250],
    ["12,05", 1205],
    [" 0 ", 0],
    ["12.345", null],
    ["-1", null],
    ["", null],
    ["€12", null],
  ])("ParseMoney_%s_Is%s", (text, minor) => {
    expect(parseMoney(text, "EUR")).toBe(minor);
  });
});
