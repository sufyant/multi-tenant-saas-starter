"use client";

import { SignIn, type SignInProps } from "@repo/ui/components/sign-in";
import { reportError } from "@/error-tracking/report-error";

/**
 * The SignIn of packages/ui with this app's error tracking: a Clerk error code without a message goes out once (F41, F55). The route
 * is a server component, which cannot pass a function of the browser, so this client component passes it.
 */
export function SignInScreen(props: Omit<SignInProps, "onUnexpectedError">) {
  return <SignIn {...props} onUnexpectedError={reportError} />;
}
