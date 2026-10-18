import { z } from "zod";

// Every environment variable of the app, read once through one schema (F7). `env()` runs at server start
// (instrumentation.ts), so a missing variable fails there, not at its first use. API_URL is a server variable: no
// NEXT_PUBLIC_ name, so the browser never sees it.
const schema = z.object({
  API_URL: z.url(),
  NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY: z.string().min(1),
  CLERK_SECRET_KEY: z.string().min(1),
  NEXT_PUBLIC_CLERK_SIGN_IN_URL: z.string().startsWith("/"),
});

export type Env = z.infer<typeof schema>;

/** Reads and checks the variables of a source. Throws with the name of each missing or wrong variable. */
export function readEnv(source: Record<string, string | undefined>): Env {
  const result = schema.safeParse(source);
  if (!result.success) {
    const names = result.error.issues
      .map((issue) => issue.path.join("."))
      .join(", ");
    throw new Error(
      `Missing or invalid environment variables: ${names}. See .env.example.`,
    );
  }
  return result.data;
}

let cached: Env | undefined;

/** The app's variables, read from process.env on the first call. */
export function env(): Env {
  cached ??= readEnv(process.env);
  return cached;
}
