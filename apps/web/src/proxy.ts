import { clerkMiddleware, createRouteMatcher } from "@clerk/nextjs/server";
import { env } from "@/env";

// Every page needs a session but sign-in, its SSO callback, the invitation's accept page and the health check (F46). The BFF under /api checks the
// session itself and answers 401 without one (F6), so a screen can send the user to sign-in (F4); Clerk's protect() would answer
// such a call with 404. The accept page signs a new person up with Clerk's ticket, or sends a signed-out person to sign-in and
// back (F44).
const isOpen = createRouteMatcher([
  "/sign-in(.*)",
  "/sso-callback(.*)",
  "/invitations/accept(.*)",
  "/health",
  "/api/(.*)",
]);

// The publishable key comes from the server at run time, and the sign-in page is the same in every environment (F7).
export default clerkMiddleware(
  async (auth, request) => {
    if (!isOpen(request)) await auth.protect();
  },
  () => ({
    publishableKey: env().CLERK_PUBLISHABLE_KEY,
    signInUrl: "/sign-in",
  }),
);

export const config = {
  matcher: [
    "/((?!_next|[^?]*\\.(?:html?|css|js(?!on)|jpe?g|webp|png|gif|svg|ttf|woff2?|ico|csv|docx?|xlsx?|zip|webmanifest)).*)",
    "/(api|trpc)(.*)",
  ],
};
