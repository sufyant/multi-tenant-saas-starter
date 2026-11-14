import { expect } from "vitest";

/**
 * Checks a dialog's disabled action and its reason (F52): the action is disabled, it names one element as its description, and that
 * element is inside the dialog, visible, and says the text. So a reason can never point at nothing or at hidden text.
 */
export function expectShownReason(action: HTMLElement, text: string): void {
  expect(action).toBeDisabled();
  const ids = (action.getAttribute("aria-describedby") ?? "")
    .split(" ")
    .filter(Boolean);
  expect(ids).toHaveLength(1);
  const reason = document.getElementById(ids[0] ?? "");
  expect(action.closest("[role=alertdialog]")).toContainElement(reason);
  expect(reason).toBeVisible();
  expect(action).toHaveAccessibleDescription(text);
}
