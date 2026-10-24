import AxeBuilder from "@axe-core/playwright";
import { expect, type Page } from "@playwright/test";

/**
 * Runs axe on the page for WCAG 2.2 AA (F37) and fails on any violation. Clerk's footer is left out: its development instance
 * draws "Secured by" on an orange "Development mode" strip at 1.97:1, which a production instance does not show. `known` names a
 * violation of a page that a later step fixes, as `rule: target`; only that exact one passes, and the test lists it until then.
 */
export async function expectAccessible(
  page: Page,
  { known = [] }: { known?: string[] } = {},
): Promise<void> {
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
    .exclude(".cl-footer")
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"])
    .analyze();
  expect(
    results.violations
      .map(
        (violation) =>
          `${violation.id}: ${violation.nodes.map((node) => node.target.join(" ")).join(", ")}`,
      )
      .filter((violation) => !known.includes(violation)),
  ).toEqual([]);
}
