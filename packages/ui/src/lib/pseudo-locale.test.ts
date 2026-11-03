import { describe, expect, it } from "vitest";
import { pseudoLocalize, pseudoMessages } from "./pseudo-locale";

const visible = (text: string) => text.replace(/[[\]]/g, "");

describe("pseudoLocalize", () => {
  it("PseudoLocalize_PlainText_AddsAccentsAndBrackets", () => {
    const text = pseudoLocalize("Settings");
    expect(text).toMatch(/^\[Ŝéţţîñĝš .+\]$/);
  });

  it("PseudoLocalize_PlainText_GrowsByAtLeastFortyPercent", () => {
    for (const source of ["Home", "Activity log", "Back to Northwind Traders"])
      expect(visible(pseudoLocalize(source)).length).toBeGreaterThanOrEqual(
        Math.ceil(source.length * 1.4),
      );
  });

  // An argument's name and a plural's keywords are code: only the text of a branch changes.
  it("PseudoLocalize_IcuArguments_KeepsNamesAndKeywords", () => {
    expect(
      pseudoLocalize("{count, plural, one {# member} other {# members}}"),
    ).toMatch(/^\[\{count, plural, one \{# ɱéɱƀéŕ\} other \{# ɱéɱƀéŕš\}\}/);
    expect(pseudoLocalize("Back to {name}")).toMatch(/^\[Ɓåçǩ ţø \{name\}/);
  });

  it("PseudoLocalize_RichTextTag_KeepsTheTag", () => {
    expect(pseudoLocalize("Go to <link>Home</link>")).toMatch(
      /^\[Ĝø ţø <link>Ĥøɱé<\/link>/,
    );
  });
});

describe("pseudoMessages", () => {
  it("PseudoMessages_NestedMessages_ChangesEveryStringAndKeepsTheKeys", () => {
    const messages = pseudoMessages({ shell: { home: "Home" }, n: 1 });
    expect(Object.keys(messages)).toEqual(["shell", "n"]);
    expect((messages.shell as { home: string }).home).toMatch(/^\[Ĥøɱé/);
    expect(messages.n).toBe(1);
  });

  // The language's date format is a key of the messages, not a text: the Intl API must still read it.
  it("PseudoMessages_LocaleFormat_KeepsIt", () => {
    const messages = pseudoMessages({ locale: { format: "en-IE" } });
    expect(messages.locale).toEqual({ format: "en-IE" });
  });
});
