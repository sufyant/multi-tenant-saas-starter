import { auth } from "@clerk/nextjs/server";
import { type BffOptions, createBff } from "@repo/api-client";
import { env } from "@/env";

/**
 * The BFF (F6) as the web's route handlers build it: Clerk's session token, without a template, becomes the bearer token. A route
 * handler passes `fetch`, since only the route handlers and the mutator call it.
 */
export const bffWith = (send: BffOptions["send"]) =>
  createBff({
    apiUrl: env().API_URL,
    getToken: async () => (await auth()).getToken(),
    send,
  });
