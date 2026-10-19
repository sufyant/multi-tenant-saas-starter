import { test as teardown } from "@playwright/test";
import { deleteUser } from "./clerk-users";
import { readUser } from "./two-step";

// Deletes the setup's temporary user. Playwright runs it after the tests that need the setup, also when one failed.
teardown("delete the temporary user", async () => {
  const user = readUser();
  if (user) await deleteUser(user.id);
});
