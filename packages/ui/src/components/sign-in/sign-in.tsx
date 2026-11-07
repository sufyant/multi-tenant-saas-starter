"use client";

import { useSignIn } from "@clerk/nextjs";
import type { Language } from "@repo/api-client";
import { useTranslations } from "next-intl";
import { useEffect, useState } from "react";
import { AuthFooter, AuthScreen } from "../auth-layout";
import { Banner } from "../banner";
import { Button } from "../button";
import type { Provider } from "../provider-buttons";
import {
  type SignInErrorKey,
  type SignInStep,
  signInErrorKey,
} from "./clerk-errors";
import { CodeStep } from "./code-step";
import { EmailStep } from "./email-step";
import {
  reasonParameter,
  redirectParameter,
  safeRedirect,
  sessionEnded,
} from "./safe-redirect";
import { type SecondFactor, SecondFactorStep } from "./second-factor-step";

export type SignInProps = {
  /** "Sign in to your account", "Sign in to the platform". */
  title: string;
  /** The sentence under the title: who signs in here, and how. */
  text: string;
  /** The app's own title and sentence of the authenticator code. */
  secondFactorTitle: string;
  secondFactorText: string;
  /** The product's name: the logo and the header. */
  brand: string;
  /** The platform app's ink label. */
  platformLabel?: string;
  /** The app's `setLocale` server action (F13). */
  onLanguageChange: (language: Language) => Promise<void>;
  /** The brand's privacy policy, which the footer links as "Privacy". */
  privacyUrl: string;
  /** The app's error tracking: a Clerk error code without a message, once per code (F41, F55). */
  onUnexpectedError?: (error: Error) => void;
  /** The page after sign-in, a path of this app, in place of the address's `redirect_url`: the invitation's page (F44). */
  redirectTo?: string;
};

/** The query parameter that `SsoCallback` sets when an SSO sign-in could not finish. */
export const ssoFailedParameter = "sso";

type Step = "email" | "code" | "secondFactor";

const query = () => new URLSearchParams(window.location.search);

/** The page after sign-in: the `redirect_url` of this origin, else `/`. */
export const destination = () =>
  safeRedirect(query().get(redirectParameter), window.location.origin);

/**
 * Our own sign-in, on Clerk's custom-flow hooks (Core 3): an email code, Google or Microsoft, then the second factor when the
 * account has one. Both apps render it; only the app's own words and the Platform label differ. Sign-up is by invitation only, and no step
 * tells whether an address has an account (strict enumeration protection).
 */
export function SignIn({
  title,
  text,
  secondFactorTitle,
  secondFactorText,
  brand,
  platformLabel,
  onLanguageChange,
  privacyUrl,
  onUnexpectedError,
  redirectTo,
}: SignInProps) {
  const t = useTranslations("signIn");
  const { signIn, fetchStatus } = useSignIn();
  const [step, setStep] = useState<Step>("email");
  const [email, setEmail] = useState("");
  const [error, setError] = useState<SignInErrorKey>();
  const [resent, setResent] = useState(false);
  const [ended, setEnded] = useState(false);
  const pending = fetchStatus === "fetching";
  const after = () =>
    redirectTo
      ? safeRedirect(redirectTo, window.location.origin)
      : destination();
  const keyOf = (error: unknown, step: SignInStep) =>
    signInErrorKey(error, step, onUnexpectedError);

  // A sign-in that comes back from Google or Microsoft with a second factor to pass (SsoCallback), or a page loaded again.
  useEffect(() => {
    if (signIn.status === "needs_second_factor") setStep("secondFactor");
  }, [signIn.status]);
  useEffect(() => {
    if (query().has(ssoFailedParameter)) setError("cannotSignIn");
    setEnded(query().get(reasonParameter) === sessionEnded);
  }, []);

  const finish = async () => {
    const { error } = await signIn.finalize({
      navigate: ({ decorateUrl }) => {
        // decorateUrl may give an absolute address of this origin (Safari's tracking protection), so the browser loads it.
        window.location.assign(decorateUrl(after()));
      },
    });
    if (error) setError(keyOf(error, "code"));
  };

  // After a factor: finish, or ask for the second factor. Any other status has no step here.
  const next = async () => {
    if (signIn.status === "complete") return finish();
    if (signIn.status === "needs_second_factor") {
      setError(undefined);
      setStep("secondFactor");
      return;
    }
    setError(keyOf(undefined, "code"));
  };

  const sendCode = async (address: string) => {
    setError(undefined);
    // A fetch that fails may reject instead of returning Clerk's error (frame 1.04).
    const { error } = await signIn.emailCode
      .sendCode({ emailAddress: address })
      .catch((thrown: unknown) => ({ error: thrown }));
    if (error) return setError(keyOf(error, "send"));
    setEmail(address);
    setStep("code");
  };

  const resend = async () => {
    setError(undefined);
    setResent(false);
    const { error } = await signIn.emailCode.sendCode();
    if (error) return setError(keyOf(error, "code"));
    setResent(true);
  };

  const verifyCode = async (code: string) => {
    setResent(false);
    const { error } = await signIn.emailCode.verifyCode({ code });
    if (error) return setError(keyOf(error, "code"));
    await next();
  };

  const verifySecondFactor = async (code: string, factor: SecondFactor) => {
    const { error } =
      factor === "totp"
        ? await signIn.mfa.verifyTOTP({ code })
        : await signIn.mfa.verifyBackupCode({ code });
    if (error) return setError(keyOf(error, factor));
    await next();
  };

  const sso = async (provider: Provider) => {
    setError(undefined);
    const back = encodeURIComponent(after());
    const { error } = await signIn.sso({
      strategy: provider,
      redirectUrl: after(),
      redirectCallbackUrl: `/sso-callback?${redirectParameter}=${back}`,
    });
    if (error) setError(keyOf(error, "email"));
  };

  const startOver = async () => {
    await signIn.reset();
    setError(undefined);
    setResent(false);
    setStep("email");
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
      {step === "email" && (
        <EmailStep
          title={title}
          text={text}
          banner={
            ended && (
              <Banner title={t("sessionEndedTitle")}>
                {t("sessionEndedText")}
              </Banner>
            )
          }
          onSubmit={(address) => void sendCode(address)}
          onProvider={(provider) => void sso(provider)}
          pending={pending}
          error={error}
        />
      )}
      {step === "code" && (
        <CodeStep
          email={email}
          onSubmit={(code) => void verifyCode(code)}
          onResend={() => void resend()}
          onUseAnotherEmail={() => void startOver()}
          pending={pending}
          error={error}
          resent={resent}
        />
      )}
      {step === "secondFactor" && (
        <SecondFactorStep
          title={secondFactorTitle}
          text={secondFactorText}
          onSubmit={(code, factor) => void verifySecondFactor(code, factor)}
          onFactorChange={() => setError(undefined)}
          exit={
            <Button
              variant="ghost"
              onClick={() => void startOver()}
              disabled={pending}
            >
              {t("useAnotherEmail")}
            </Button>
          }
          pending={pending}
          error={error}
        />
      )}
    </AuthScreen>
  );
}
