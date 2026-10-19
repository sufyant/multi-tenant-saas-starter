"use client";

import { useTranslations } from "next-intl";
import { type FormEvent, useState } from "react";
import { AuthCard } from "../auth-layout";
import { Button } from "../button";
import { CodeInput } from "../code-input";
import type { SignInErrorKey } from "./clerk-errors";
import { TextButton } from "./text-button";

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

/** The email code (the SignInCode board): the address, the ten minutes, six digits, and a way to a new code or another address. */
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
      <div className="flex flex-col gap-2">
        <h1 className="text-page-title">{t("checkEmailTitle")}</h1>
        <p className="text-body text-muted-foreground">
          {t.rich("checkEmailText", {
            email: () => <bdi dir="ltr">{email}</bdi>,
          })}
        </p>
      </div>
      <AuthCard>
        <form noValidate onSubmit={submit} className="flex flex-col gap-3">
          <CodeInput
            label={t("code")}
            value={code}
            onChange={setCode}
            error={error && t(`errors.${error}`)}
            disabled={pending}
            autoFocus
          />
          <Button
            type="submit"
            variant="primary"
            size="lg"
            className="w-full"
            disabled={pending}
          >
            {t("signIn")}
          </Button>
        </form>
        <p className="text-small text-muted-foreground">
          {t("noCode")}{" "}
          <TextButton onClick={onResend} disabled={pending}>
            {t("sendNewCode")}
          </TextButton>
          {" · "}
          <TextButton onClick={onUseAnotherEmail} disabled={pending}>
            {t("useAnotherEmail")}
          </TextButton>
        </p>
        {resent && (
          <p role="status" className="text-small text-muted-foreground">
            {t("newCodeSent")}
          </p>
        )}
      </AuthCard>
    </>
  );
}
