import { describe, expect, it } from "vitest";
import { messages, messagesFor } from "./messages";

describe("messagesFor", () => {
  it("MessagesFor_NoTestLocale_GivesTheLanguagesMessages", () => {
    expect(messagesFor("en", false)).toBe(messages.en);
  });

  it("MessagesFor_TestLocale_GivesEveryTextLongerWithAccents", () => {
    const pseudo = messagesFor("en", true);

    expect(pseudo.common.close).toMatch(/^\[Çļøšé ŵŵ\]$/);
    expect(Object.keys(pseudo)).toEqual(Object.keys(messages.en));
  });
});
