"use client";

import { useTranslations } from "next-intl";
import { type FormEvent, type ReactNode, useState } from "react";
import { z } from "zod";
import { AuthHeading } from "../auth-layout/auth-screen";
import { Banner } from "../banner";
import { Button } from "../button";
import { Input } from "../input";
import { type Provider, ProviderButtons } from "../provider-buttons";
import type { SignInErrorKey } from "./clerk-errors";

export type EmailStepProps = {
  title: string;
  /** The sentence under the title: the app's own words. */
  text: string;
  onSubmit: (email: string) => void;
  onProvider: (provider: Provider) => void;
  pending?: boolean;
  error?: SignInErrorKey;
  /** A banner under the text, such as "Your session ended". */
  banner?: ReactNode;
};

// The browser's own check takes "ayse@northwind"; a sign-in address needs a domain with a dot (frame 1.02).
const address = z.email();

/** The first step (frames 1.01 to 1.04): Google, Microsoft, "or", then the email that gets a code. No password anywhere. */
export function EmailStep({
  title,
  text,
  onSubmit,
  onProvider,
  pending,
  error,
  banner,
}: EmailStepProps) {
  const t = useTranslations("signIn");
  const [email, setEmail] = useState("");
  const [invalid, setInvalid] = useState<SignInErrorKey>();
  const submit = (event: FormEvent) => {
    event.preventDefault();
    const typed = email.trim();
    const problem =
      typed === ""
        ? "emailRequired"
        : address.safeParse(typed).success
          ? undefined
          : "emailInvalid";
    setInvalid(problem);
    if (!problem) onSubmit(typed);
  };
  const failed = !invalid && error === "sendFailed";
  const shown = invalid ?? (failed ? undefined : error);
  return (
    <>
      <AuthHeading title={title} text={text} />
      {banner}
      {failed && (
        <Banner tone="destructive" title={t("errors.sendFailed")}>
          {t("errors.sendFailedText")}
        </Banner>
      )}
      <ProviderButtons
        labels={{
          oauth_google: t("continueWithGoogle"),
          oauth_microsoft: t("continueWithMicrosoft"),
        }}
        onSelect={onProvider}
        disabled={pending}
      />
      <div className="flex items-center gap-3 text-small text-muted-foreground">
        <span className="h-px flex-1 bg-border" />
        {t("or")}
        <span className="h-px flex-1 bg-border" />
      </div>
      <form noValidate onSubmit={submit} className="flex flex-col gap-4">
        <Input
          label={t("email")}
          type="email"
          name="email"
          autoComplete="email"
          placeholder={t("emailPlaceholder")}
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          error={shown && t(`errors.${shown}`)}
          disabled={pending}
        />
        <Button
          type="submit"
          variant="primary"
          className="w-full"
          disabled={pending}
        >
          {pending ? t("sendingCode") : t("continue")}
        </Button>
      </form>
    </>
  );
}
