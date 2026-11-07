"use client";

import { HandleSSOCallback } from "@clerk/nextjs";
import type { Language } from "@repo/api-client";
import { useTranslations } from "next-intl";
import { useEffect, useState } from "react";
import { AuthFooter, AuthHeading, AuthScreen } from "../auth-layout";
import { redirectParameter, safeRedirect } from "./safe-redirect";
import { ssoFailedParameter } from "./sign-in";

const back = () =>
  new URLSearchParams(window.location.search).get(redirectParameter);

/** After this wait the card says the callback takes longer than usual, and offers sign-in again (frames 1.52 and platform 1.14). */
const slowAfter = 10_000;

export type SsoCallbackProps = {
  /** The product's name: the mark. */
  brand: string;
  /** The platform app's ink label. */
  platformLabel?: string;
  /** The app's `setLocale` server action (F13). */
  onLanguageChange: (language: Language) => Promise<void>;
  /** The brand's privacy policy. */
  privacyUrl: string;
};

/**
 * The end of a Google or Microsoft sign-in (Clerk's custom OAuth flow, Core 3; frame 1.38). A new session goes to the page of
 * `redirect_url`. A second factor goes back to the sign-in page, which shows the second-factor step. A sign-up is by invitation
 * only, so a person without an account goes back to sign-in with a neutral message. After 10 seconds the card says it takes longer
 * than usual and links to sign-in again, with the same page to go back to.
 */
export function SsoCallback({
  brand,
  platformLabel,
  onLanguageChange,
  privacyUrl,
}: SsoCallbackProps) {
  const t = useTranslations("signIn");
  const common = useTranslations("common");
  const [slow, setSlow] = useState(false);
  useEffect(() => {
    const timer = setTimeout(() => setSlow(true), slowAfter);
    return () => clearTimeout(timer);
  }, []);
  const signInUrl = (extra = "") =>
    `/sign-in?${redirectParameter}=${encodeURIComponent(
      safeRedirect(back(), window.location.origin),
    )}${extra}`;
  const signInPage = (extra = "") => window.location.assign(signInUrl(extra));
  return (
    <>
      <AuthScreen
        brand={brand}
        platformLabel={platformLabel}
        footer={
          <AuthFooter
            onLanguageChange={onLanguageChange}
            privacyUrl={privacyUrl}
          />
        }
      >
        <AuthHeading title={t("finishingTitle")} />
        <p
          role="status"
          className="text-center text-body text-muted-foreground"
        >
          {slow ? t("finishingSlow") : t("finishingText")}
        </p>
        {slow && (
          <a
            href={signInUrl()}
            className="self-center rounded-sm text-small text-link underline-offset-4 outline-none hover:underline focus-visible:ring-2 focus-visible:ring-ring"
          >
            {common("tryAgain")}
          </a>
        )}
      </AuthScreen>
      {/* Clerk's callback draws a div with its captcha slot, so it stays out of the card and its gaps. */}
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
