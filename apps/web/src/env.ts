import { z } from "zod";

// Every environment variable of the app, read once through one schema (F7). `env()` runs at server start
// (instrumentation.ts), so a missing variable fails there, not at its first use. API_URL is a server variable: no
// NEXT_PUBLIC_ name, so the browser never sees it. CLERK_PUBLISHABLE_KEY has no NEXT_PUBLIC_ name either, because a build would
// write such a value into the image: the server reads it at run time and gives it to ClerkProvider and clerkMiddleware, so one
// image serves every environment. Error tracking starts only with ERROR_TRACKING_DSN, which then needs its environment; the root
// layout gives both to the browser at run time (F55). An empty value counts as missing, as Railway gives it for a missing shared one.
const optional = <T extends z.ZodType>(type: T) =>
  z.preprocess((value) => (value === "" ? undefined : value), type.optional());

const schema = z
  .object({
    API_URL: z.url(),
    CLERK_PUBLISHABLE_KEY: z.string().min(1),
    CLERK_SECRET_KEY: z.string().min(1),
    ERROR_TRACKING_DSN: optional(z.url()),
    ERROR_TRACKING_ENVIRONMENT: optional(z.enum(["staging", "production"])),
  })
  .refine(
    (variables) =>
      !variables.ERROR_TRACKING_DSN || variables.ERROR_TRACKING_ENVIRONMENT,
    { path: ["ERROR_TRACKING_ENVIRONMENT"] },
  );

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
