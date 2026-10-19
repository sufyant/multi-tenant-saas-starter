"use client";

import { useSignIn } from "@clerk/nextjs";
import { useTranslations } from "next-intl";
import { useEffect, useState } from "react";
import { AuthLayout } from "../auth-layout";
import type { Provider } from "../provider-buttons";
import { type SignInErrorKey, signInErrorKey } from "./clerk-errors";
import { CodeStep } from "./code-step";
import { EmailStep } from "./email-step";
import { LanguageSwitcher } from "./language-switcher";
import { redirectParameter, safeRedirect } from "./safe-redirect";
import { type SecondFactor, SecondFactorStep } from "./second-factor-step";
import { TextButton } from "./text-button";

export type SignInProps = {
  /** "Sign in to Starter", "Sign in to the platform". */
  title: string;
  /** The product's name: the logo and the header. */
  brand: string;
  /** The platform app's ink label. */
  platformLabel?: string;
  /** The app's `setLocale` server action (F13). */
  onLanguageChange: (language: string) => Promise<void>;
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
 * account has one. Both apps render it; only the title and the Platform label differ. Sign-up is by invitation only, and no step
 * tells whether an address has an account (strict enumeration protection).
 */
export function SignIn({
  title,
  brand,
  platformLabel,
  onLanguageChange,
}: SignInProps) {
  const t = useTranslations("signIn");
  const { signIn, fetchStatus } = useSignIn();
  const [step, setStep] = useState<Step>("email");
  const [email, setEmail] = useState("");
  const [error, setError] = useState<SignInErrorKey>();
  const [resent, setResent] = useState(false);
  const pending = fetchStatus === "fetching";

  // A sign-in that comes back from Google or Microsoft with a second factor to pass (SsoCallback), or a page loaded again.
  useEffect(() => {
    if (signIn.status === "needs_second_factor") setStep("secondFactor");
  }, [signIn.status]);
  useEffect(() => {
    if (query().has(ssoFailedParameter)) setError("cannotSignIn");
  }, []);

  const finish = async () => {
    const { error } = await signIn.finalize({
      navigate: ({ decorateUrl }) => {
        // decorateUrl may give an absolute address of this origin (Safari's tracking protection), so the browser loads it.
        window.location.assign(decorateUrl(destination()));
      },
    });
    if (error) setError(signInErrorKey(error, "code"));
  };

  // After a factor: finish, or ask for the second factor. Any other status has no step here.
  const next = async () => {
    if (signIn.status === "complete") return finish();
    if (signIn.status === "needs_second_factor") {
      setError(undefined);
      setStep("secondFactor");
      return;
    }
    setError(signInErrorKey(undefined, "code"));
  };

  const sendCode = async (address: string) => {
    setError(undefined);
    const { error } = await signIn.emailCode.sendCode({
      emailAddress: address,
    });
    if (error) return setError(signInErrorKey(error, "email"));
    setEmail(address);
    setStep("code");
  };

  const resend = async () => {
    setError(undefined);
    setResent(false);
    const { error } = await signIn.emailCode.sendCode();
    if (error) return setError(signInErrorKey(error, "code"));
    setResent(true);
  };

  const verifyCode = async (code: string) => {
    setResent(false);
    const { error } = await signIn.emailCode.verifyCode({ code });
    if (error) return setError(signInErrorKey(error, "code"));
    await next();
  };

  const verifySecondFactor = async (code: string, factor: SecondFactor) => {
    const { error } =
      factor === "totp"
        ? await signIn.mfa.verifyTOTP({ code })
        : await signIn.mfa.verifyBackupCode({ code });
    if (error) return setError(signInErrorKey(error, factor));
    await next();
  };

  const sso = async (provider: Provider) => {
    setError(undefined);
    const back = encodeURIComponent(destination());
    const { error } = await signIn.sso({
      strategy: provider,
      redirectUrl: destination(),
      redirectCallbackUrl: `/sso-callback?${redirectParameter}=${back}`,
    });
    if (error) setError(signInErrorKey(error, "email"));
  };

  const startOver = async () => {
    await signIn.reset();
    setError(undefined);
    setResent(false);
    setStep("email");
  };

  return (
    <AuthLayout
      brand={brand}
      platformLabel={platformLabel}
      footer={<LanguageSwitcher onChange={onLanguageChange} />}
    >
      {step === "email" && (
        <EmailStep
          title={title}
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
          onSubmit={(code, factor) => void verifySecondFactor(code, factor)}
          onFactorChange={() => setError(undefined)}
          exit={
            <TextButton onClick={() => void startOver()}>
              {t("useAnotherEmail")}
            </TextButton>
          }
          pending={pending}
          error={error}
        />
      )}
    </AuthLayout>
  );
}
