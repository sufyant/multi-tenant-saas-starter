import { render } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Avatar, initialsOf, tintOf } from "./avatar";

describe("initialsOf", () => {
  it("InitialsOf_TwoWords_IsTheirFirstLetters", () => {
    expect(initialsOf({ name: "Ayşe Yılmaz" })).toBe("AY");
  });

  // The maintainer's decision of 2026-11-06: the first letters of the first and the last word, whatever lies between.
  it("InitialsOf_ThreeWords_IsTheFirstAndTheLastWordsFirstLetters", () => {
    expect(initialsOf({ name: "María  José García" })).toBe("MG");
  });

  // The Arabic article "ال" joins the word it defines, so the letter after it names the word (8.36).
  it("InitialsOf_ArabicWordsWithTheArticle_SkipTheArticle", () => {
    expect(initialsOf({ name: "عبد الرحمن الهاشمي" })).toBe("عه");
    expect(initialsOf({ name: "فاطمة الزهراء القحطاني" })).toBe("فق");
  });

  it("InitialsOf_LatinWordsThatStartWithAl_KeepTheirFirstLetters", () => {
    expect(initialsOf({ name: "Alia Al Hashimi" })).toBe("AH");
    expect(initialsOf({ name: "Alan Alvarez" })).toBe("AA");
  });

  it("InitialsOf_OneWord_IsItsFirstLetter", () => {
    expect(initialsOf({ name: "Ayşe" })).toBe("A");
  });

  it("InitialsOf_NoName_IsTheEmailsFirstLetterInCapitals", () => {
    expect(
      initialsOf({
        name: null,
        email: "ayse@northwind.example",
      }),
    ).toBe("A");
  });

  it("InitialsOf_DeletedUser_IsEmpty", () => {
    expect(initialsOf({ name: null })).toBe("");
  });
});

describe("Avatar", () => {
  it("Avatar_Person_ShowsTheInitialsHiddenFromScreenReaders", () => {
    const { container } = render(
      <Avatar person={{ id: "a", name: "Ayşe Yılmaz" }} />,
    );
    const avatar = container.querySelector('[data-slot="avatar"]');

    expect(avatar).toHaveTextContent("AY");
    expect(avatar).toHaveAttribute("aria-hidden", "true");
  });

  // jsdom draws nothing, so the look is the classes of the tokens; Playwright reads the top bar's drawn avatar.
  it("Avatar_InAList_IsTheSmallTintedCircle", () => {
    const { container } = render(
      <Avatar person={{ id: "f", name: "Ayşe Yılmaz" }} />,
    );

    // Small, 22 px, in a table row (45:328).
    expect(container.querySelector('[data-slot="avatar"]')).toHaveClass(
      "size-5.5",
      "rounded-full",
    );
    expect(
      container.querySelector('[data-slot="avatar-fallback"]'),
    ).toHaveClass(
      "bg-role-owner-subtle",
      "text-role-owner-text",
      "text-status",
    );
  });

  it("Avatar_Medium_IsThePhoneCardsCircle", () => {
    const { container } = render(
      <Avatar person={{ id: "f", name: "Ayşe Yılmaz" }} size="medium" />,
    );

    expect(container.querySelector('[data-slot="avatar"]')).toHaveClass(
      "size-8",
    );
    expect(
      container.querySelector('[data-slot="avatar-fallback"]'),
    ).toHaveClass("text-small", "font-medium");
  });

  // The Avatar component: one person keeps one tint everywhere, chosen from the person's id (45:334).
  it("Avatar_SamePerson_KeepsOneTint", () => {
    const { container } = render(
      <>
        <Avatar person={{ id: "b", name: "Ayşe Yılmaz" }} />
        <Avatar person={{ id: "b", name: "Ayşe Demir" }} size="medium" />
      </>,
    );

    const [row, card] = container.querySelectorAll(
      '[data-slot="avatar-fallback"]',
    );
    expect(row).toHaveClass("bg-success-subtle", "text-success-text");
    expect(card).toHaveClass("bg-success-subtle", "text-success-text");
  });

  it("TintOf_Ids_UseTheSixTintsInTheComponentsOrder", () => {
    expect(["f", "a", "b", "c", "d", "e"].map(tintOf)).toEqual([
      "owner",
      "info",
      "success",
      "warning",
      "destructive",
      "neutral",
    ]);
  });

  it("TintOf_SameId_IsTheSameTint", () => {
    const id = "0f8c2a51-7a4e-4c1b-9d3e-5b6a7c8d9e01";

    expect(tintOf(id)).toBe(tintOf(id));
  });

  it("Avatar_Size_TakesTheClass", () => {
    const { container } = render(
      <Avatar person={{ id: "a", name: "Ayşe Yılmaz" }} className="size-11" />,
    );

    expect(container.querySelector('[data-slot="avatar"]')).toHaveClass(
      "size-11",
    );
  });
});
