"use client";

import { useTranslations } from "next-intl";
import { type FormEvent, type ReactNode, useState } from "react";
import { AuthCard } from "../auth-layout";
import { Button } from "../button";
import { CodeInput } from "../code-input";
import { Input } from "../input";
import type { SignInErrorKey } from "./clerk-errors";
import { TextButton } from "./text-button";

export type SecondFactor = "totp" | "backupCode";

export type SecondFactorStepProps = {
  onSubmit: (code: string, factor: SecondFactor) => void;
  /** The way out at the end of the hint: "Use another email" before sign-in, "Sign out" after it. */
  exit: ReactNode;
  pending?: boolean;
  error?: SignInErrorKey;
  /** Called when the person switches between the app and a backup code, so the caller can clear an error. */
  onFactorChange?: (factor: SecondFactor) => void;
};

/**
 * The second factor (the TotpChallenge board): the six digits of the authenticator app, or one backup code. Both apps ask for it,
 * because a system admin who signs in to the tenant web passes it too: one Clerk instance serves both.
 */
export function SecondFactorStep({
  onSubmit,
  exit,
  pending,
  error,
  onFactorChange,
}: SecondFactorStepProps) {
  const t = useTranslations("signIn");
  const [factor, setFactor] = useState<SecondFactor>("totp");
  const [code, setCode] = useState("");
  const change = (next: SecondFactor) => {
    setFactor(next);
    setCode("");
    onFactorChange?.(next);
  };
  const submit = (event: FormEvent) => {
    event.preventDefault();
    onSubmit(code.trim(), factor);
  };
  const message = error && t(`errors.${error}`);
  const totp = factor === "totp";
  return (
    <>
      <div className="flex flex-col gap-2">
        <h1 className="text-page-title">
          {totp ? t("totpTitle") : t("backupCodeTitle")}
        </h1>
        <p className="text-body text-muted-foreground">
          {totp ? t("totpText") : t("backupCodeText")}
        </p>
      </div>
      <AuthCard>
        <form noValidate onSubmit={submit} className="flex flex-col gap-3">
          {totp ? (
            <CodeInput
              label={t("code")}
              value={code}
              onChange={setCode}
              error={message}
              disabled={pending}
              autoFocus
            />
          ) : (
            <Input
              label={t("backupCode")}
              value={code}
              onChange={(event) => setCode(event.target.value)}
              autoComplete="off"
              spellCheck={false}
              dir="ltr"
              className="font-mono"
              error={message}
              disabled={pending}
              autoFocus
            />
          )}
          <Button
            type="submit"
            variant="primary"
            size="lg"
            className="w-full"
            disabled={pending}
          >
            {t("continue")}
          </Button>
        </form>
        <p className="text-small text-muted-foreground">
          {totp ? (
            <>
              {t("noPhone")}{" "}
              <TextButton onClick={() => change("backupCode")}>
                {t("useBackupCode")}
              </TextButton>
            </>
          ) : (
            <TextButton onClick={() => change("totp")}>
              {t("useAuthenticator")}
            </TextButton>
          )}
          {" · "}
          {exit}
        </p>
      </AuthCard>
    </>
  );
}
