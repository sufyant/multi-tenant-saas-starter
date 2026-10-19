import { request } from "@playwright/test";

// The platform's tests use a temporary user of the development instance, made and deleted through Clerk's Backend API with the
// instance's CLERK_SECRET_KEY, which CI already has. Its address takes Clerk's test code 424242.
const api = "https://api.clerk.com/v1";

function secretKey(): string {
  const key = process.env.CLERK_SECRET_KEY;
  if (!key?.startsWith("sk_test_"))
    throw new Error("Set CLERK_SECRET_KEY to the development instance's key.");
  return key;
}

export async function createUser(email: string): Promise<string> {
  const context = await request.newContext({
    extraHTTPHeaders: { Authorization: `Bearer ${secretKey()}` },
  });
  const response = await context.post(`${api}/users`, {
    data: { email_address: [email] },
  });
  if (!response.ok())
    throw new Error(`Clerk did not create the test user: ${response.status()}`);
  const { id } = (await response.json()) as { id: string };
  await context.dispose();
  return id;
}

export async function deleteUser(id: string): Promise<void> {
  const context = await request.newContext({
    extraHTTPHeaders: { Authorization: `Bearer ${secretKey()}` },
  });
  const response = await context.delete(`${api}/users/${id}`);
  if (!response.ok() && response.status() !== 404)
    throw new Error(`Clerk did not delete the test user: ${response.status()}`);
  await context.dispose();
}

type ClerkUser = {
  id: string;
  created_at: number;
  email_addresses: { email_address: string }[];
};

const isTemporary = (user: ClerkUser) =>
  user.email_addresses.some(
    ({ email_address }) =>
      email_address.startsWith("e2e-platform-") &&
      email_address.endsWith("+clerk_test@example.com"),
  );

/**
 * Deletes every temporary user of an earlier run that is older than an hour. A run that CI cancelled before its teardown so
 * leaves nothing behind for long; a run that is still going, in another job, keeps its own user. Answers how many it deleted.
 */
export async function deleteStaleUsers(): Promise<number> {
  const context = await request.newContext({
    extraHTTPHeaders: { Authorization: `Bearer ${secretKey()}` },
  });
  const response = await context.get(`${api}/users`, {
    params: { email_address_query: "e2e-platform-", limit: 100 },
  });
  if (!response.ok())
    throw new Error(`Clerk did not list the test users: ${response.status()}`);
  const anHourAgo = Date.now() - 60 * 60 * 1000;
  const stale = ((await response.json()) as ClerkUser[]).filter(
    (user) => isTemporary(user) && user.created_at < anHourAgo,
  );
  await context.dispose();
  for (const user of stale) await deleteUser(user.id);
  return stale.length;
}
