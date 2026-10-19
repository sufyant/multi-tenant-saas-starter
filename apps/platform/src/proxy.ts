import { clerkMiddleware, createRouteMatcher } from "@clerk/nextjs/server";
import { NextResponse } from "next/server";
import { hasVerifiedSecondFactor } from "@/shared/second-factor";

// Every page needs a session but sign-in and its SSO callback. The BFF under /api checks the session itself and answers 401
// without one (F6), so a screen can send the user to sign-in (F4); Clerk's protect() would answer such a call with 404.
const isOpen = createRouteMatcher([
  "/sign-in(.*)",
  "/sso-callback(.*)",
  "/api/(.*)",
]);
// The two-step gate (F42): a session without a verified second factor goes to /two-step before anything else. This is a
// convenience: the API still refuses every /v1/platform request without one (A6), and the BFF passes that answer on.
const needsNoSecondFactor = createRouteMatcher(["/two-step(.*)"]);

export default clerkMiddleware(async (auth, request) => {
  if (isOpen(request)) return;
  const { factorVerificationAge } = await auth.protect();
  if (
    !needsNoSecondFactor(request) &&
    !hasVerifiedSecondFactor(factorVerificationAge)
  )
    return NextResponse.redirect(new URL("/two-step", request.url));
});

export const config = {
  matcher: [
    "/((?!_next|[^?]*\\.(?:html?|css|js(?!on)|jpe?g|webp|png|gif|svg|ttf|woff2?|ico|csv|docx?|xlsx?|zip|webmanifest)).*)",
    "/(api|trpc)(.*)",
  ],
};
