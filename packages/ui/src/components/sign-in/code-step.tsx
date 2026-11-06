"use client";

import { useTranslations } from "next-intl";
import { type FormEvent, useState } from "react";
import { AuthHeading } from "../auth-layout/auth-screen";
import { Button } from "../button";
import { CodeInput } from "../code-input";
import type { SignInErrorKey } from "./clerk-errors";

export type CodeStepProps = {
  email: string;
  onSubmit: (code: string) => void;
  onResend: () => void;
  onUseAnotherEmail: () => void;
  pending?: boolean;
  error?: SignInErrorKey;
  /** A new code went out. */
  resent?: boolean;
};

/**
 * The email code (frames 1.05, 1.06 and 1.37): the address, six digits, the button that signs in, and a way to a new code or another
 * address. The button stays, so the code never goes out on its own (WCAG 2.2, 3.2.2).
 */
export function CodeStep({
  email,
  onSubmit,
  onResend,
  onUseAnotherEmail,
  pending,
  error,
  resent,
}: CodeStepProps) {
  const t = useTranslations("signIn");
  const [code, setCode] = useState("");
  const submit = (event: FormEvent) => {
    event.preventDefault();
    onSubmit(code);
  };
  return (
    <>
      <AuthHeading
        title={t("checkEmailTitle")}
        text={t.rich("checkEmailText", {
          email: () => <bdi dir="ltr">{email}</bdi>,
        })}
      />
      <form noValidate onSubmit={submit} className="flex flex-col gap-4">
        <CodeInput
          label={t("code")}
          value={code}
          onChange={setCode}
          error={error && t(`errors.${error}`)}
          disabled={pending}
          autoFocus
          centred
        />
        <Button
          type="submit"
          variant="primary"
          className="w-full"
          disabled={pending}
        >
          {t("signIn")}
        </Button>
      </form>
      <div className="flex flex-wrap items-center justify-center gap-x-4">
        <Button variant="ghost" onClick={onResend} disabled={pending}>
          {t("sendNewCode")}
        </Button>
        <Button variant="ghost" onClick={onUseAnotherEmail} disabled={pending}>
          {t("useAnotherEmail")}
        </Button>
      </div>
      {resent && (
        <p
          role="status"
          className="text-center text-small text-muted-foreground"
        >
          {t("newCodeSent")}
        </p>
      )}
    </>
  );
}
