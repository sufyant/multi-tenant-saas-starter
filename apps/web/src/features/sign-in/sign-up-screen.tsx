"use client";

import { getGetSignUpQueryOptions } from "@repo/api-client";
import { SignUp, type SignUpProps } from "@repo/ui/components/sign-in";
import { useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { reportError } from "@/error-tracking/report-error";
import { useTermsUrl } from "@/shared/terms-link";

/**
 * The SignUp of packages/ui while the product has sign-up on (GetSignUp; frames 1.25, 1.26, 1.32), with this app's error tracking
 * and the footer's "Terms". While sign-up is off, or when GetSignUp fails, the page leads to sign-in, which works either way. After
 * sign-up `/` asks the terms (1.27).
 */
export function SignUpScreen(
  props: Omit<
    SignUpProps,
    "onUnexpectedError" | "termsUrl" | "signInHref" | "after"
  >,
) {
  const router = useRouter();
  const termsUrl = useTermsUrl();
  const signUp = useQuery(getGetSignUpQueryOptions());
  const off = signUp.isError || (signUp.isSuccess && !signUp.data.enabled);
  useEffect(() => {
    if (off) router.replace("/sign-in");
  }, [off, router]);
  if (!signUp.data?.enabled) return null;
  return (
    <SignUp
      {...props}
      onUnexpectedError={reportError}
      termsUrl={termsUrl}
      signInHref="/sign-in"
      after="/"
    />
  );
}
