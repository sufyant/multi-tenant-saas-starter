"use client";

import { CodeInput } from "@repo/ui/components/code-input";
import { Dialog } from "@repo/ui/components/dialog";
import { Input } from "@repo/ui/components/input";
import { type SecondFactor, TextButton } from "@repo/ui/components/sign-in";
import { useTranslations } from "next-intl";
import { type FormEvent, useState } from "react";
import { type Session, useReverification } from "@/shared/use-reverification";

/**
 * "Confirm it's you" (the MakeSystemAdmin and RemoveSystemAdmin boards): the API refused a grant or a revoke for a second factor
 * that is too old (403 `system_admin.reverification_required`, A6a). The step checks the authenticator's code or a backup code
 * through Clerk's session reverification, as the two-step challenge does (F42), then the dialog sends its request again. Cancel
 * closes the dialog and sends nothing.
 */
export function ReverifyStep({
  session,
  onVerified,
  onCancel,
}: {
  session: Session;
  onVerified: () => void;
  onCancel: () => void;
}) {
  const t = useTranslations();
  const { verify, error, pending, clearError } = useReverification(
    session,
    onVerified,
  );
  const [factor, setFactor] = useState<SecondFactor>("totp");
  const [code, setCode] = useState("");
  const change = (next: SecondFactor) => {
    setFactor(next);
    setCode("");
    clearError();
  };
  const submit = (event?: FormEvent) => {
    event?.preventDefault();
    if (!pending) void verify(code.trim(), factor);
  };
  const message = error && t(`signIn.errors.${error}`);

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onCancel()}
      title={t("systemAdmins.reverifyTitle")}
      description={t("systemAdmins.reverifyText")}
      actionLabel={t("systemAdmins.verify")}
      cancelLabel={t("common.cancel")}
      pending={pending}
      onConfirm={() => submit()}
    >
      <form noValidate onSubmit={submit} className="flex flex-col gap-2">
        {factor === "totp" ? (
          <CodeInput
            label={t("signIn.code")}
            value={code}
            onChange={setCode}
            error={message}
            disabled={pending}
            autoFocus
          />
        ) : (
          <Input
            label={t("signIn.backupCode")}
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
        <p className="text-small text-muted-foreground">
          {factor === "totp" ? (
            <TextButton onClick={() => change("backupCode")}>
              {t("signIn.useBackupCode")}
            </TextButton>
          ) : (
            <TextButton onClick={() => change("totp")}>
              {t("signIn.useAuthenticator")}
            </TextButton>
          )}
        </p>
      </form>
    </Dialog>
  );
}
