import createClient, { type Client } from "openapi-fetch";
import type { paths } from "./schema";

export type { components, operations, paths } from "./schema";

export type ApiClient = Client<paths>;

export type ApiClientOptions = {
  /** The API's origin, such as https://api.example.com. Paths start with /v1. */
  baseUrl: string;
  /** The signed-in user's token. Without a token, the request goes without an Authorization header. */
  getToken: () => Promise<string | null> | string | null;
};

/**
 * A typed client for the API. It adds `Authorization: Bearer <token>` to each request, and nothing else: no retry, no cache, no state.
 */
export function createApiClient({
  baseUrl,
  getToken,
}: ApiClientOptions): ApiClient {
  const client = createClient<paths>({ baseUrl });
  client.use({
    async onRequest({ request }) {
      const token = await getToken();
      if (token) request.headers.set("Authorization", `Bearer ${token}`);
      return request;
    },
  });
  return client;
}
