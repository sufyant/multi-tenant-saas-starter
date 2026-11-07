"use client";

import { useTranslations } from "next-intl";
import { type FormEvent, type ReactNode, useState } from "react";
import { AuthHeading } from "../auth-layout/auth-screen";
import { Button } from "../button";
import { CodeInput } from "../code-input";
import { Input } from "../input";
import type { SignInErrorKey } from "./clerk-errors";

export type SecondFactor = "totp" | "backupCode";

export type SecondFactorStepProps = {
  /** The app's own title and sentence for the authenticator code: "Two-step verification", "Enter the code from your app". */
  title: string;
  text: string;
  onSubmit: (code: string, factor: SecondFactor) => void;
  /** The way out under the switch to the other factor, a ghost button: "Use another email" before sign-in, "Sign out" after it. */
  exit: ReactNode;
  pending?: boolean;
  error?: SignInErrorKey;
  /** Called when the person switches between the app and a backup code, so the caller can clear an error. */
  onFactorChange?: (factor: SecondFactor) => void;
};

/**
 * The second factor (web frames 1.07 and 1.08, platform frames 1.05 and 1.06): the six digits of the authenticator app, or one backup
 * code. Both apps ask for it, because a system admin who signs in to the tenant web passes it too: one Clerk instance serves both.
 */
export function SecondFactorStep({
  title,
  text,
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
      <AuthHeading
        title={totp ? title : t("backupCodeTitle")}
        text={totp ? text : t("backupCodeSignInText")}
      />
      <form noValidate onSubmit={submit} className="flex flex-col gap-4">
        {totp ? (
          <CodeInput
            label={t("code")}
            value={code}
            onChange={setCode}
            error={message}
            disabled={pending}
            autoFocus
            centred
          />
        ) : (
          <Input
            label={t("backupCode")}
            value={code}
            onChange={(event) => setCode(event.target.value)}
            autoComplete="off"
            spellCheck={false}
            dir="ltr"
            error={message}
            disabled={pending}
            autoFocus
          />
        )}
        <Button
          type="submit"
          variant="primary"
          className="w-full"
          disabled={pending}
        >
          {t("continue")}
        </Button>
      </form>
      <div className="flex flex-col items-center gap-4">
        <Button
          variant="ghost"
          onClick={() => change(totp ? "backupCode" : "totp")}
          disabled={pending}
        >
          {totp ? t("useBackupCode") : t("useAuthenticator")}
        </Button>
        {totp && exit}
      </div>
    </>
  );
}
