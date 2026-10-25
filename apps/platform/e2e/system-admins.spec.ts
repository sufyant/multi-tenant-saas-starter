import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import { mockGrant, mockPlatform } from "./api";
import { expectAccessible } from "./axe";
import { readUser, totpCode } from "./two-step";

// Settings › System admins: the list and Add, with the code step after a stale second factor (the SystemAdmins and
// MakeSystemAdmin boards, F42). The API's refusal is mocked; the code step is Clerk's real session reverification with the
// setup's authenticator.

const path = "/settings/system-admins";
const row = (page: Page, name: string) =>
  page.getByRole("row", { name: new RegExp(name) });

const open = async (page: Page) => {
  await setupClerkTestingToken({ page });
  await mockPlatform(page);
  await page.goto(path);
};

/** Types an address into Add's email step, and waits for the lookup that follows it. */
const typeEmail = async (page: Page, email: string) => {
  const lookup = page.waitForResponse((response) =>
    response.url().includes(`email=${encodeURIComponent(email)}`),
  );
  await page
    .getByRole("alertdialog")
    .getByRole("textbox", { name: "Email" })
    .fill(email);
  await lookup;
};

/** Add's first two steps for John: the exact email shows his card, and Continue asks to confirm. */
const findJohn = async (page: Page) => {
  await page.getByRole("button", { name: "Add system admin" }).click();
  const dialog = page.getByRole("alertdialog");
  // The email filter compares the whole address: one that only starts John's finds nobody.
  await typeEmail(page, "john@kestrel.exampl");
  await expect(
    dialog.getByText("No user has this email. They must sign in once first."),
  ).toBeVisible();
  await expect(dialog.getByRole("button", { name: "Continue" })).toBeDisabled();
  await typeEmail(page, "JOHN@kestrel.example");
  await expect(dialog).toContainText(
    "John Byrnejohn@kestrel.example · Member of 1 organisation",
  );
  await expectAccessible(page);
  await dialog.getByRole("button", { name: "Continue" }).click();
  await expect(
    page.getByRole("alertdialog", { name: "Make John Byrne a system admin?" }),
  ).toBeVisible();
};

test("SystemAdmins_List_ShowsTheRootAndTheOwnRow", async ({ page }) => {
  await open(page);

  await expect(
    page.getByRole("heading", { level: 1, name: "System admins" }),
  ).toBeVisible();
  await expect(row(page, "root@starter.example")).toContainText("Root");
  await expect(row(page, "root@starter.example")).toContainText(
    "Root admins come from the platform’s configuration.",
  );
  await expect(row(page, "Sam Kelly")).toContainText("(you)");
  await page.getByRole("button", { name: "Actions for Sam Kelly" }).click();
  await expect(
    page.getByRole("menuitem", { name: /Remove system admin/ }),
  ).toBeDisabled();
  await expect(
    page.getByText("You can’t remove yourself. Ask another system admin."),
  ).toBeVisible();
  await expectAccessible(page);
});

test("AddSystemAdmin_ExactEmail_MakesThemASystemAdmin", async ({ page }) => {
  const keys: string[] = [];
  await open(page);
  await mockGrant(page, { keys });

  await findJohn(page);
  await expectAccessible(page);
  await page.getByRole("button", { name: "Make system admin" }).click();

  await expect(page.getByText("John Byrne is a system admin.")).toBeVisible();
  expect(keys).toHaveLength(1);
});

test("AddSystemAdmin_StaleSecondFactor_AsksTheCodeThenSendsAgain", async ({
  page,
}) => {
  test.setTimeout(90_000);
  const secret = readUser()?.secret;
  if (!secret) throw new Error("The setup did not enrol the temporary user.");
  const keys: string[] = [];
  await open(page);
  await mockGrant(page, { refusals: 1, keys });

  await findJohn(page);
  await page.getByRole("button", { name: "Make system admin" }).click();
  const step = page.getByRole("alertdialog", { name: "Confirm it’s you" });
  await expect(step).toBeVisible();
  await expectAccessible(page);
  await step
    .getByRole("textbox", { name: "Code" })
    .pressSequentially(await totpCode(secret));
  await step.getByRole("button", { name: "Verify and continue" }).click();

  await expect(page.getByText("John Byrne is a system admin.")).toBeVisible({
    timeout: 30_000,
  });
  // The refused request claimed no key (the API's I4), so the request after the code step sends the same one.
  expect(keys).toHaveLength(2);
  expect(keys[1]).toBe(keys[0]);
});
