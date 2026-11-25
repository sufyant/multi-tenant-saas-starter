import { Language } from "@repo/api-client";
import { type Direction, directions } from "@repo/ui/lib/locale";
import ar from "../messages/ar.json" with { type: "json" };
import en from "../messages/en.json" with { type: "json" };
import productAr from "../messages/product/ar.json" with { type: "json" };
import productEn from "../messages/product/en.json" with { type: "json" };
import productTr from "../messages/product/tr.json" with { type: "json" };
import tr from "../messages/tr.json" with { type: "json" };

export type Messages = typeof en;

type Tree = { [key: string]: unknown };

const isTree = (value: unknown): value is Tree =>
  typeof value === "object" && value !== null && !Array.isArray(value);

/**
 * The product's texts over the template's: an object merges key by key, and any other value of the product replaces the template's.
 * The template ships `messages/product/*.json` empty and never changes them, so a product adds its words there, such as the word of
 * its entitlement key under `notificationWords.entitlementKey` (X1 of apps/api/docs/ARCHITECTURE.md).
 */
function withProduct(template: Tree, product: Tree): Tree {
  const merged: Tree = { ...template };
  for (const [key, value] of Object.entries(product)) {
    const below = merged[key];
    merged[key] =
      isTree(below) && isTree(value) ? withProduct(below, value) : value;
  }
  return merged;
}

/** The template's texts in each language of the contract, so a new language fails the type check until it has its file (F14). */
const template: Record<Language, Messages> = { en, tr, ar };

/** The texts of every email in each language, the template's with the product's. */
export const messages: Record<Language, Messages> = {
  en: withProduct(template.en, productEn) as Messages,
  tr: withProduct(template.tr, productTr) as Messages,
  ar: withProduct(template.ar, productAr) as Messages,
};

/** The languages of the contract (the API's K8). */
export const languages = Object.values(Language);

/** The direction of a language, from the one place that knows it (F13). */
export const directionOf = (language: Language): Direction =>
  directions[language];

/** Writes the values into the `{name}` arguments of a text. An argument without a value is an error, so no text goes out with a hole. */
export function format(text: string, values: Record<string, string>): string {
  return text.replace(/\{(\w+)\}/g, (_, name: string) => {
    const value = values[name];
    if (value === undefined) {
      throw new Error(`The text "${text}" has no value for {${name}}.`);
    }
    return value;
  });
}

/** The parts of a text around its one `<link>...</link>`: before, the link's own text, after. */
export function linkParts(text: string): [string, string, string] {
  const found = /^(.*)<link>(.*)<\/link>(.*)$/s.exec(text);
  if (!found) {
    throw new Error(`The text "${text}" has no <link>...</link>.`);
  }
  return [found[1] ?? "", found[2] ?? "", found[3] ?? ""];
}

/** A text without its link's tags, for the plain-text part. */
export const withoutLinkTags = (text: string): string =>
  linkParts(text).join("");
