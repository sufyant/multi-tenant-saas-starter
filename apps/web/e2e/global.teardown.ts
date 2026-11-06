import { test as teardown } from "@playwright/test";
import { deleteSavedUser } from "./clerk-users";

// Deletes the setup's temporary user, and with it its sessions. Playwright runs it after the tests that need the setup, also
// when one failed.
teardown("delete the temporary user", async () => {
  await deleteSavedUser();
});
