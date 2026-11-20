"use client";

import { getGetSignUpQueryOptions } from "@repo/api-client";
import { SignIn, type SignInProps } from "@repo/ui/components/sign-in";
import { useQuery } from "@tanstack/react-query";
import { reportError } from "@/error-tracking/report-error";
import { useTermsUrl } from "@/shared/terms-link";

/**
 * The SignIn of packages/ui with this app's error tracking: a Clerk error code without a message goes out once (F41, F55), and the
 * footer's "Terms" of the version in force, and "Create an account" while the product has sign-up on (GetSignUp, 1.24). The route is a server component, which cannot pass a function of the browser nor read
 * a query, so this client component passes both.
 */
export function SignInScreen(
  props: Omit<SignInProps, "onUnexpectedError" | "termsUrl" | "signUpHref">,
) {
  const termsUrl = useTermsUrl();
  const signUp = useQuery(getGetSignUpQueryOptions());
  return (
    <SignIn
      {...props}
      onUnexpectedError={reportError}
      termsUrl={termsUrl}
      signUpHref={signUp.data?.enabled ? "/sign-up" : undefined}
    />
  );
}
