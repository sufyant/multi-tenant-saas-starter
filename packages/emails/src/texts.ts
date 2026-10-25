import { Language } from "@repo/api-client";
import { type Direction, directions } from "@repo/ui/lib/locale";
import ar from "../messages/ar.json" with { type: "json" };
import en from "../messages/en.json" with { type: "json" };
import tr from "../messages/tr.json" with { type: "json" };

export type Messages = typeof en;

/** The texts of every email in each language of the contract, so a new language fails the type check until it has its file (F14). */
export const messages: Record<Language, Messages> = { en, tr, ar };

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
