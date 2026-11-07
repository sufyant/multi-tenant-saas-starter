import { bffWith } from "@/shared/bff";

// The BFF (F6): the browser's only door to the API.
async function handle(request: Request): Promise<Response> {
  return bffWith(fetch)(request);
}

export { handle as DELETE, handle as GET, handle as PATCH, handle as POST };
