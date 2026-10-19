// The liveness check of the platform's health check and the deploy's smoke test: 200 with no body. It needs no session and calls
// no API, so it answers while the process runs. It sits outside /api, so the BFF never forwards it (F6, F46).
export function GET(): Response {
  return new Response(null, { status: 200 });
}
