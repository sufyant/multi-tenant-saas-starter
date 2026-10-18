import { clerkSetup } from "@clerk/testing/playwright";

// Playwright's global setup runs in the main process, so the testing token that clerkSetup puts into the environment reaches
// every worker of every project.
export default async function globalSetup(): Promise<void> {
  await clerkSetup();
}
