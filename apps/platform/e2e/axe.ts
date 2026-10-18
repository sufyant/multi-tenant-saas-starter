import AxeBuilder from "@axe-core/playwright";
import { expect, type Page } from "@playwright/test";

/** Runs axe on the page for WCAG 2.2 AA (F37) and fails on any violation. */
export async function expectAccessible(page: Page): Promise<void> {
  // A component that fades in is half transparent for a moment, and axe would measure that contrast. A loop, such as the
  // Skeleton's shimmer, never ends, so the wait skips it.
  await page.waitForFunction(() =>
    document
      .getAnimations()
      .every(
        (animation) =>
          animation.playState !== "running" ||
          animation.effect?.getTiming().iterations === Number.POSITIVE_INFINITY,
      ),
  );
  const results = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"])
    .analyze();
  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
}
