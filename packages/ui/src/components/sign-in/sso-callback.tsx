"use client";

import { HandleSSOCallback } from "@clerk/nextjs";
import type { Language } from "@repo/api-client";
import { useTranslations } from "next-intl";
import { AuthFooter, AuthHeading, AuthScreen } from "../auth-layout";
import { Skeleton } from "../ui/skeleton";
import { redirectParameter, safeRedirect } from "./safe-redirect";
import { ssoFailedParameter } from "./sign-in";

const back = () =>
  new URLSearchParams(window.location.search).get(redirectParameter);

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
 * only, so a person without an account goes back to sign-in with a neutral message.
 */
export function SsoCallback({
  brand,
  platformLabel,
  onLanguageChange,
  privacyUrl,
}: SsoCallbackProps) {
  const t = useTranslations("signIn");
  const signInPage = (extra = "") => {
    const page = `/sign-in?${redirectParameter}=${encodeURIComponent(
      safeRedirect(back(), window.location.origin),
    )}${extra}`;
    window.location.assign(page);
  };
  return (
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
      <AuthHeading title={t("finishingTitle")} text={t("finishingText")} />
      <div role="status" aria-label={t("finishingTitle")} aria-busy="true">
        <Skeleton className="h-control-md w-full rounded-md" />
      </div>
      <HandleSSOCallback
        navigateToApp={({ decorateUrl }) =>
          window.location.assign(
            decorateUrl(safeRedirect(back(), window.location.origin)),
          )
        }
        navigateToSignIn={() => signInPage()}
        navigateToSignUp={() => signInPage(`&${ssoFailedParameter}=failed`)}
      />
    </AuthScreen>
  );
}
