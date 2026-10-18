import { auth } from "@clerk/nextjs/server";
import { createBff } from "@repo/api-client";
import { env } from "@/env";

// The BFF (F6): the browser's only door to the API. Clerk's session token, without a template, becomes the bearer token.
async function handle(request: Request): Promise<Response> {
  const forward = createBff({
    apiUrl: env().API_URL,
    getToken: async () => (await auth()).getToken(),
    send: fetch,
  });
  return forward(request);
}

export { handle as DELETE, handle as GET, handle as PATCH, handle as POST };
