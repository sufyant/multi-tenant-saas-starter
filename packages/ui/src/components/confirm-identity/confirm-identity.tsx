"use client";

import type { useSession } from "@clerk/nextjs";
import { useTranslations } from "next-intl";
import { type FormEvent, useEffect, useRef, useState } from "react";
import { CodeInput } from "../code-input";
import { Dialog } from "../dialog";
import { Input } from "../input";
import {
  type SecondFactor,
  type SignInErrorKey,
  type SignInStep,
  signInErrorKey,
  TextButton,
} from "../sign-in";

export type VerifyingSession = NonNullable<
  ReturnType<typeof useSession>["session"]
>;

/** The check Clerk asks for: the email code, the authenticator, or both one after the other. */
export type VerificationLevel =
  | "first_factor"
  | "second_factor"
  | "multi_factor";

type Step = "starting" | "email" | SecondFactor;

const strategies = { totp: "totp", backupCode: "backup_code" } as const;

/**
 * "Confirm it's you": Clerk's session reverification, before an action that Clerk or the API keeps for a recent check (F42, F62).
 * It starts the check at the level asked; the email code goes to the person's address, and the authenticator's code or a backup
 * code follows when the level needs a second factor. After a check that completes it asks Clerk for a fresh session token, which
 * writes the session cookie again, so the proxy and the BFF read the new factor's age. Cancel sends nothing.
 */
export function ConfirmIdentity({
  session,
  level,
  onVerified,
  onCancel,
  onUnexpectedError,
}: {
  session: VerifyingSession;
  level: VerificationLevel;
  onVerified: () => void;
  onCancel: () => void;
  /** The app's error tracking, for a Clerk error that has no message yet. */
  onUnexpectedError?: (error: Error) => void;
}) {
  const t = useTranslations();
  const [step, setStep] = useState<Step>("starting");
  const [email, setEmail] = useState<{ id: string; address: string }>();
  const [code, setCode] = useState("");
  const [error, setError] = useState<SignInErrorKey>();
  const [pending, setPending] = useState(false);
  const [resent, setResent] = useState(false);
  const started = useRef(false);

  const fail = (reason: unknown, at: SignInStep) =>
    setError(signInErrorKey(reason, at, onUnexpectedError));

  const sendEmailCode = (emailAddressId: string) =>
    session.prepareFirstFactorVerification({
      strategy: "email_code",
      emailAddressId,
    });

  // Clerk answers where the check stands: the next step, or done.
  const follow = async (status: string) => {
    if (status === "complete") {
      await session.getToken({ skipCache: true });
      return onVerified();
    }
    setCode("");
    setStep(status === "needs_second_factor" ? "totp" : "email");
  };

  // The check starts once, after the first render: the ref keeps a later render, or Strict Mode's second mount, from sending a
  // second code.
  useEffect(() => {
    if (started.current) return;
    started.current = true;
    void (async () => {
      try {
        const verification = await session.startVerification({ level });
        const factor = verification.supportedFirstFactors?.find(
          (each) => each.strategy === "email_code",
        );
        if (verification.status === "needs_first_factor" && factor) {
          setEmail({
            id: factor.emailAddressId,
            address: factor.safeIdentifier,
          });
          await sendEmailCode(factor.emailAddressId);
        }
        await follow(verification.status);
      } catch (reason) {
        fail(reason, "code");
        setStep("email");
      }
    })();
  });

  const verify = async () => {
    setPending(true);
    setError(undefined);
    try {
      const verification =
        step === "email"
          ? await session.attemptFirstFactorVerification({
              strategy: "email_code",
              code,
            })
          : await session.attemptSecondFactorVerification({
              strategy: strategies[step as SecondFactor],
              code,
            });
      await follow(verification.status);
    } catch (reason) {
      fail(reason, step === "email" ? "code" : (step as SecondFactor));
    }
    setPending(false);
  };

  const submit = (event?: FormEvent) => {
    event?.preventDefault();
    if (!pending && step !== "starting") void verify();
  };

  const resend = async () => {
    if (!email) return;
    setError(undefined);
    try {
      await sendEmailCode(email.id);
      setResent(true);
    } catch (reason) {
      fail(reason, "code");
    }
  };

  const change = (next: SecondFactor) => {
    setStep(next);
    setCode("");
    setError(undefined);
  };

  const message = error && t(`signIn.errors.${error}`);
  const description =
    step === "email" || step === "starting"
      ? email &&
        t.rich("confirmIdentity.emailText", {
          email: () => <bdi dir="ltr">{email.address}</bdi>,
        })
      : t(
          step === "totp" ? "confirmIdentity.appText" : "signIn.backupCodeText",
        );

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onCancel()}
      title={t("confirmIdentity.title")}
      description={description}
      actionLabel={t("confirmIdentity.verify")}
      cancelLabel={t("common.cancel")}
      pending={pending || step === "starting"}
      onConfirm={() => submit()}
    >
      <form noValidate onSubmit={submit} className="flex flex-col gap-2">
        {step === "backupCode" ? (
          <Input
            label={t("signIn.backupCode")}
            value={code}
            onChange={(event) => setCode(event.target.value.trim())}
            autoComplete="off"
            spellCheck={false}
            dir="ltr"
            className="font-mono"
            error={message}
            disabled={pending}
            autoFocus
          />
        ) : (
          <CodeInput
            label={t("signIn.code")}
            value={code}
            onChange={setCode}
            error={message}
            disabled={pending || step === "starting"}
            autoFocus
          />
        )}
        <p className="text-small text-muted-foreground">
          {step === "email" && (
            <TextButton onClick={() => void resend()}>
              {t("signIn.sendNewCode")}
            </TextButton>
          )}
          {step === "email" && resent && (
            <span role="status"> {t("signIn.newCodeSent")}</span>
          )}
          {step === "totp" && (
            <TextButton onClick={() => change("backupCode")}>
              {t("signIn.useBackupCode")}
            </TextButton>
          )}
          {step === "backupCode" && (
            <TextButton onClick={() => change("totp")}>
              {t("signIn.useAuthenticator")}
            </TextButton>
          )}
        </p>
      </form>
    </Dialog>
  );
}
