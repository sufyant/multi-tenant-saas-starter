/**
 * Whether a request is a client-side navigation of the App Router, which each app's proxy answers with 401 when Clerk sees it signed
 * out, so the browser loads the page as a document and Clerk's handshake refreshes the token (F63). Next.js strips its RSC header off
 * the proxy's request, so a navigation is a GET that is no document request, by the test of Clerk's handshake, and no server action.
 */
export function isNavigation(request: Request): boolean {
  const destination = request.headers.get("sec-fetch-dest");
  const document = destination
    ? destination === "document" || destination === "iframe"
    : request.headers.get("accept")?.startsWith("text/html");
  return (
    request.method === "GET" && !document && !request.headers.has("next-action")
  );
}
