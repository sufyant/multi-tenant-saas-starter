"use client";

import { useTranslations } from "next-intl";
import { type FormEvent, useState } from "react";
import type { Instant } from "../../lib/format-date-time";
import { type Problem, useFailureText } from "../../lib/use-failure-text";
import { useIntentKey } from "../../lib/use-intent-key";
import { Button } from "../button";
import { CodeInput } from "../code-input";
import { Dialog } from "../dialog";
import { Input } from "../input";
import { TextButton } from "../sign-in";
import { showToast } from "../toast";

/** A failed call as the app reads it: the API's code, its field codes, and whether the API saved the answer under the key (I6). */
export type ReadFailure = (error: unknown) => {
  problem: Problem;
  fields: Record<string, string[]> | null;
  saved: boolean;
};

export type EmailChangeProps = {
  /** `RequestEmailChange` with its Idempotency-Key (F48); it answers until when the code works. */
  send: (email: string, key: string) => Promise<{ expiresAt: string }>;
  /** `ChangeEmail` with the code. */
  verify: (code: string) => Promise<unknown>;
  readFailure: ReadFailure;
  /** After the change: the app reads GetMe again, so every screen shows the new address. */
  onChanged: () => void;
  /** Tests fix the clock. */
  now?: Instant;
};

type Step = "email" | "code" | undefined;

const minute = 60_000;

/**
 * Change email (6.08 to 6.11, F62): the new address gets a 6-digit code; the address changes when the person enters it. Our API
 * owns the address and gives it to the identity provider (A7). The calls are the app's; this holds the steps, the keys of F48 and
 * where a refusal shows: an address's under the address, a code's under the code.
 */
export function EmailChange({
  send,
  verify,
  readFailure,
  onChanged,
  now,
}: EmailChangeProps) {
  const t = useTranslations();
  const failureText = useFailureText();
  const { key, renew } = useIntentKey();
  const [step, setStep] = useState<Step>();
  const [address, setAddress] = useState("");
  const [code, setCode] = useState("");
  const [minutes, setMinutes] = useState(10);
  const [error, setError] = useState<string>();
  const [pending, setPending] = useState(false);
  const [resent, setResent] = useState(false);

  const close = () => {
    setStep(undefined);
    setAddress("");
    setCode("");
    setError(undefined);
    setResent(false);
    renew();
  };

  const refused = (reason: unknown, field: "email" | "code") => {
    const failure = readFailure(reason);
    if (failure.saved) renew();
    const fieldCode = failure.fields?.[field]?.[0];
    setError(
      fieldCode
        ? t(`validation.${fieldCode}` as "validation.invalid")
        : failureText(failure.problem),
    );
  };

  const sendCode = async (again: boolean) => {
    setPending(true);
    setError(undefined);
    try {
      const sent = await send(address.trim(), key);
      renew();
      const left =
        new Date(sent.expiresAt).getTime() -
        new Date(now ?? Date.now()).getTime();
      setMinutes(Math.max(1, Math.round(left / minute)));
      setResent(again);
      setStep("code");
    } catch (reason) {
      refused(reason, again ? "code" : "email");
    }
    setPending(false);
  };

  const check = async () => {
    setPending(true);
    setError(undefined);
    try {
      await verify(code);
      const changedTo = address.trim();
      close();
      onChanged();
      showToast({
        title: t("accountSecurity.changed"),
        description: t("accountSecurity.changedText", { email: changedTo }),
      });
    } catch (reason) {
      refused(reason, "code");
    }
    setPending(false);
  };

  const submit = (event?: FormEvent) => {
    event?.preventDefault();
    if (pending) return;
    void (step === "email" ? sendCode(false) : check());
  };

  return (
    <>
      <Button onClick={() => setStep("email")}>
        {t("accountSecurity.changeEmail")}
      </Button>
      {step === "email" && (
        <Dialog
          open
          onOpenChange={(isOpen) => !isOpen && close()}
          title={t("accountSecurity.changeTitle")}
          description={t("accountSecurity.changeText")}
          actionLabel={t("accountSecurity.sendCode")}
          action="primary"
          cancelLabel={t("common.cancel")}
          pending={pending}
          onConfirm={() => submit()}
        >
          <form noValidate onSubmit={submit}>
            <Input
              label={t("accountSecurity.newEmail")}
              type="email"
              autoComplete="email"
              dir="ltr"
              placeholder={t("signIn.emailPlaceholder")}
              value={address}
              onChange={(event) => {
                setAddress(event.target.value);
                setError(undefined);
                // Another address is another intent (F48).
                renew();
              }}
              error={error}
              disabled={pending}
              autoFocus
            />
          </form>
        </Dialog>
      )}
      {step === "code" && (
        <Dialog
          open
          onOpenChange={(isOpen) => !isOpen && close()}
          title={t("accountSecurity.checkTitle")}
          description={t.rich("accountSecurity.sentText", {
            email: () => <bdi dir="ltr">{address.trim()}</bdi>,
            minutes,
          })}
          actionLabel={t("accountSecurity.verify")}
          action="primary"
          cancelLabel={t("common.cancel")}
          pending={pending}
          onConfirm={() => submit()}
        >
          <form noValidate onSubmit={submit} className="flex flex-col gap-2">
            <CodeInput
              label={t("signIn.code")}
              value={code}
              onChange={setCode}
              error={error}
              disabled={pending}
              autoFocus
            />
            <p className="text-small text-muted-foreground">
              <TextButton onClick={() => void sendCode(true)}>
                {t("signIn.sendNewCode")}
              </TextButton>
              {resent && <span role="status"> {t("signIn.newCodeSent")}</span>}
            </p>
          </form>
        </Dialog>
      )}
    </>
  );
}
