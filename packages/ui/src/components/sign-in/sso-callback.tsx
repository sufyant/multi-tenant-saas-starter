"use client";

import { HandleSSOCallback } from "@clerk/nextjs";
import { useTranslations } from "next-intl";
import { Skeleton } from "../skeleton";
import { redirectParameter, safeRedirect } from "./safe-redirect";
import { ssoFailedParameter } from "./sign-in";

const back = () =>
  new URLSearchParams(window.location.search).get(redirectParameter);

/**
 * The end of a Google or Microsoft sign-in (Clerk's custom OAuth flow, Core 3). A new session goes to the page of `redirect_url`.
 * A second factor goes back to the sign-in page, which shows the second-factor step. A sign-up is by invitation only, so a person
 * without an account goes back to sign-in with a neutral message.
 */
export function SsoCallback() {
  const t = useTranslations("signIn");
  const signInPage = (extra = "") => {
    const page = `/sign-in?${redirectParameter}=${encodeURIComponent(
      safeRedirect(back(), window.location.origin),
    )}${extra}`;
    window.location.assign(page);
  };
  return (
    <>
      <Skeleton label={t("finishing")} />
      <HandleSSOCallback
        navigateToApp={({ decorateUrl }) =>
          window.location.assign(
            decorateUrl(safeRedirect(back(), window.location.origin)),
          )
        }
        navigateToSignIn={() => signInPage()}
        navigateToSignUp={() => signInPage(`&${ssoFailedParameter}=failed`)}
      />
    </>
  );
}
