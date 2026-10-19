"use client";

import { useTranslations } from "next-intl";
import { type FormEvent, useState } from "react";
import { AuthCard } from "../auth-layout";
import { Button } from "../button";
import { Input } from "../input";
import { type Provider, ProviderButtons } from "../provider-buttons";
import type { SignInErrorKey } from "./clerk-errors";

export type EmailStepProps = {
  title: string;
  onSubmit: (email: string) => void;
  onProvider: (provider: Provider) => void;
  pending?: boolean;
  error?: SignInErrorKey;
};

/** The first step (the SignIn board): Google, Microsoft, "or", then the email that gets a code. No password anywhere. */
export function EmailStep({
  title,
  onSubmit,
  onProvider,
  pending,
  error,
}: EmailStepProps) {
  const t = useTranslations("signIn");
  const [email, setEmail] = useState("");
  const [empty, setEmpty] = useState(false);
  const submit = (event: FormEvent) => {
    event.preventDefault();
    const address = email.trim();
    setEmpty(address === "");
    if (address !== "") onSubmit(address);
  };
  const shown = empty ? "emailRequired" : error;
  return (
    <>
      <h1 className="text-page-title">{title}</h1>
      <AuthCard>
        <ProviderButtons
          labels={{
            oauth_google: t("continueWithGoogle"),
            oauth_microsoft: t("continueWithMicrosoft"),
          }}
          onSelect={onProvider}
          disabled={pending}
        />
        <div className="my-1 flex items-center gap-3 text-small text-muted-foreground">
          <span className="h-px flex-1 bg-border" />
          {t("or")}
          <span className="h-px flex-1 bg-border" />
        </div>
        <form noValidate onSubmit={submit} className="flex flex-col gap-3">
          <Input
            label={t("email")}
            type="email"
            name="email"
            autoComplete="email"
            placeholder={t("emailPlaceholder")}
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            error={shown && t(`errors.${shown}`)}
          />
          <Button
            type="submit"
            variant="primary"
            size="lg"
            className="w-full"
            disabled={pending}
          >
            {t("sendCode")}
          </Button>
        </form>
        <p className="text-small text-muted-foreground">{t("noPassword")}</p>
      </AuthCard>
    </>
  );
}
