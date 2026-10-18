import { clerkMiddleware, createRouteMatcher } from "@clerk/nextjs/server";

// Every route needs a session but sign-in and the health check. The BFF still checks the session itself (F6).
const isPublic = createRouteMatcher(["/sign-in(.*)", "/api/health"]);

export default clerkMiddleware(async (auth, request) => {
  if (!isPublic(request)) await auth.protect();
});

export const config = {
  matcher: [
    "/((?!_next|[^?]*\\.(?:html?|css|js(?!on)|jpe?g|webp|png|gif|svg|ttf|woff2?|ico|csv|docx?|xlsx?|zip|webmanifest)).*)",
    "/(api|trpc)(.*)",
  ],
};
