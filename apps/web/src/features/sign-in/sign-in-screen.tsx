"use client";

import { SignIn, type SignInProps } from "@repo/ui/components/sign-in";
import { reportError } from "@/error-tracking/report-error";
import { useTermsUrl } from "@/shared/terms-link";

/**
 * The SignIn of packages/ui with this app's error tracking: a Clerk error code without a message goes out once (F41, F55), and the
 * footer's "Terms" of the version in force. The route is a server component, which cannot pass a function of the browser nor read
 * a query, so this client component passes both.
 */
export function SignInScreen(
  props: Omit<SignInProps, "onUnexpectedError" | "termsUrl">,
) {
  return (
    <SignIn
      {...props}
      onUnexpectedError={reportError}
      termsUrl={useTermsUrl()}
    />
  );
}
