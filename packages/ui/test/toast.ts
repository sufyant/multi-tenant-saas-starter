import { type Matcher, screen, waitFor } from "@testing-library/react";
import { expect } from "vitest";

/**
 * Waits until a toast with the text is visible. sonner adds a toast with `data-mounted="false"`, which its style sheet shows at
 * opacity 0, and sets it to true one render later, in a task of React's scheduler. `findByText` resolves on the first state, so a
 * `toBeVisible` after it races that task. This waits for the state it checks. The text must sit in a toast.
 */
export async function expectVisibleToast(text: Matcher): Promise<void> {
  await waitFor(() => {
    const element = screen.getByText(text);
    expect(element.closest("[data-sonner-toast]")).not.toBeNull();
    expect(element).toBeVisible();
  });
}
