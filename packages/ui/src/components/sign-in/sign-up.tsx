"use client";

import { useSignUp } from "@clerk/nextjs";
import type { Language } from "@repo/api-client";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { AuthFooter, AuthScreen } from "../auth-layout";
import type { Provider } from "../provider-buttons";
import {
  type SignInErrorKey,
  type SignInStep,
  signInErrorKey,
} from "./clerk-errors";
import { CodeStep } from "./code-step";
import { EmailStep } from "./email-step";
import { OtherWay } from "./other-way";
import { redirectParameter } from "./safe-redirect";

export type SignUpProps = {
  /** "Create an account". */
  title: string;
  /** The sentence under the title: how the person signs up. */
  text: string;
  /** The product's name: the logo and the header. */
  brand: string;
  /** The app's `setLocale` server action (F13). */
  onLanguageChange: (language: Language) => Promise<void>;
  /** The brand's privacy policy, which the footer links as "Privacy". */
  privacyUrl: string;
  /** The link of the terms version in force, which the footer shows as "Terms"; undefined hides it. */
  termsUrl?: string;
  /** The app's error tracking: a Clerk error code without a message, once per code (F41, F55). */
  onUnexpectedError?: (error: Error) => void;
  /** The sign-in page: "Already have an account? Sign in" (frame 1.25). */
  signInHref: string;
  /** The page after sign-up, a path of this app: the app asks the terms and the person's first organisation there. */
  after: string;
};

type Step = "email" | "code";

/**
 * The sign-up while the product has sign-up on (frames 1.25, 1.26, 1.32), on Clerk's custom-flow hooks (Core 3): an email code,
 * Google or Microsoft. Clerk asks its captcha in the slot under the card and verifies the address with the code. The person is signed
 * in at the end, and not a user of the API yet: the page of `after` asks the terms first (1.27). No step tells whether an address has
 * an account (strict enumeration protection).
 */
export function SignUp({
  title,
  text,
  brand,
  onLanguageChange,
  privacyUrl,
  termsUrl,
  onUnexpectedError,
  signInHref,
  after,
}: SignUpProps) {
  const t = useTranslations("signIn");
  const { signUp, fetchStatus } = useSignUp();
  const [step, setStep] = useState<Step>("email");
  const [email, setEmail] = useState("");
  const [error, setError] = useState<SignInErrorKey>();
  const [resent, setResent] = useState(false);
  const pending = fetchStatus === "fetching";
  const keyOf = (error: unknown, step: SignInStep) =>
    signInErrorKey(error, step, onUnexpectedError);

  const sendCode = async (address: string) => {
    setError(undefined);
    // A fetch that fails may reject instead of returning Clerk's error (frame 1.04).
    const created = await signUp
      .create({ emailAddress: address })
      .catch((thrown: unknown) => ({ error: thrown }));
    if (created.error) return setError(keyOf(created.error, "signUp"));
    const sent = await signUp.verifications
      .sendEmailCode()
      .catch((thrown: unknown) => ({ error: thrown }));
    if (sent.error) return setError(keyOf(sent.error, "send"));
    setEmail(address);
    setStep("code");
  };

  const resend = async () => {
    setError(undefined);
    setResent(false);
    const { error } = await signUp.verifications.sendEmailCode();
    if (error) return setError(keyOf(error, "code"));
    setResent(true);
  };

  const verifyCode = async (code: string) => {
    setResent(false);
    const { error } = await signUp.verifications.verifyEmailCode({ code });
    if (error) return setError(keyOf(error, "code"));
    if (signUp.status !== "complete") return setError("generic");
    const finished = await signUp.finalize({
      navigate: ({ decorateUrl }) => {
        // decorateUrl may give an absolute address of this origin (Safari's tracking protection), so the browser loads it.
        window.location.assign(decorateUrl(after));
      },
    });
    if (finished.error) setError(keyOf(finished.error, "code"));
  };

  const sso = async (provider: Provider) => {
    setError(undefined);
    const { error } = await signUp.sso({
      strategy: provider,
      redirectUrl: after,
      redirectCallbackUrl: `/sso-callback?${redirectParameter}=${encodeURIComponent(after)}`,
    });
    if (error) setError(keyOf(error, "signUp"));
  };

  const startOver = async () => {
    await signUp.reset();
    setError(undefined);
    setResent(false);
    setStep("email");
  };

  return (
    <>
      <AuthScreen
        brand={brand}
        footer={
          <AuthFooter
            onLanguageChange={onLanguageChange}
            privacyUrl={privacyUrl}
            termsUrl={termsUrl}
          />
        }
      >
        {step === "email" && (
          <EmailStep
            title={title}
            text={text}
            onSubmit={(address) => void sendCode(address)}
            onProvider={(provider) => void sso(provider)}
            pending={pending}
            error={error}
            footer={<OtherWay message="haveAccount" href={signInHref} />}
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
            submitLabel={t("continue")}
          />
        )}
      </AuthScreen>
      {/* Clerk's bot protection draws its check here (Clerk, "Custom sign-up flow"), out of the card and its gaps (1.25). */}
      <div id="clerk-captcha" />
    </>
  );
}
