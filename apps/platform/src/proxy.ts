import { clerkMiddleware, createRouteMatcher } from "@clerk/nextjs/server";

// Every page needs a session but sign-in. The BFF under /api checks the session itself and answers 401 without one (F6), so a
// screen can send the user to sign-in (F4); Clerk's protect() would answer such a call with 404.
const isOpen = createRouteMatcher(["/sign-in(.*)", "/api/(.*)"]);

export default clerkMiddleware(async (auth, request) => {
  if (!isOpen(request)) await auth.protect();
});

export const config = {
  matcher: [
    "/((?!_next|[^?]*\\.(?:html?|css|js(?!on)|jpe?g|webp|png|gif|svg|ttf|woff2?|ico|csv|docx?|xlsx?|zip|webmanifest)).*)",
    "/(api|trpc)(.*)",
  ],
};
