"use client";

import { useTranslations } from "next-intl";
import { type FormEvent, useState } from "react";
import { CodeInput } from "../code-input";
import { Dialog } from "../dialog";
import { Input } from "../input";
import { type SecondFactor, TextButton } from "../sign-in";
import {
  useConfirmIdentity,
  type VerificationLevel,
  type VerifyingSession,
} from "./use-confirm-identity";

/**
 * "Confirm it's you": the dialog of Clerk's session reverification (`useConfirmIdentity`), before an action that Clerk or the API
 * keeps for a recent check (F42, F62). Cancel closes it and sends nothing.
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
  const check = useConfirmIdentity({
    session,
    level,
    onVerified,
    onUnexpectedError,
  });
  const [factor, setFactor] = useState<SecondFactor>("totp");
  const [code, setCode] = useState("");
  const emailStep = check.step === "email";

  const submit = (event?: FormEvent) => {
    event?.preventDefault();
    if (!check.pending && check.step !== "starting")
      void check.verify(code, factor).then(() => setCode(""));
  };
  const change = (next: SecondFactor) => {
    setFactor(next);
    setCode("");
    check.clearError();
  };

  const message = check.error && t(`signIn.errors.${check.error}`);
  const description =
    check.step === "second"
      ? t(
          factor === "totp"
            ? "confirmIdentity.appText"
            : "signIn.backupCodeText",
        )
      : check.email &&
        t.rich("confirmIdentity.emailText", {
          email: () => <bdi dir="ltr">{check.email}</bdi>,
        });

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onCancel()}
      title={t("confirmIdentity.title")}
      description={description}
      actionLabel={t("confirmIdentity.verify")}
      cancelLabel={t("common.cancel")}
      pending={check.pending || check.step === "starting"}
      onConfirm={() => submit()}
    >
      <form noValidate onSubmit={submit} className="flex flex-col gap-2">
        {check.step === "second" && factor === "backupCode" ? (
          <Input
            label={t("signIn.backupCode")}
            value={code}
            onChange={(event) => setCode(event.target.value.trim())}
            autoComplete="off"
            spellCheck={false}
            dir="ltr"
            className="font-mono"
            error={message}
            disabled={check.pending}
            autoFocus
          />
        ) : (
          <CodeInput
            label={t("signIn.code")}
            value={code}
            onChange={setCode}
            error={message}
            disabled={check.pending || check.step === "starting"}
            autoFocus
          />
        )}
        <p className="text-small text-muted-foreground">
          {emailStep && (
            <TextButton onClick={() => void check.resend()}>
              {t("signIn.sendNewCode")}
            </TextButton>
          )}
          {emailStep && check.resent && (
            <span role="status"> {t("signIn.newCodeSent")}</span>
          )}
          {check.step === "second" && (
            <TextButton
              onClick={() => change(factor === "totp" ? "backupCode" : "totp")}
            >
              {t(
                factor === "totp"
                  ? "signIn.useBackupCode"
                  : "signIn.useAuthenticator",
              )}
            </TextButton>
          )}
        </p>
      </form>
    </Dialog>
  );
}
