"use client";

import type { useUser } from "@clerk/nextjs";
import { AuthCard } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import { CodeInput } from "@repo/ui/components/code-input";
import { Input } from "@repo/ui/components/input";
import {
  type SignInErrorKey,
  signInErrorKey,
} from "@repo/ui/components/sign-in";
import { useTranslations } from "next-intl";
import { QRCodeSVG } from "qrcode.react";
import { type FormEvent, useEffect, useRef, useState } from "react";

type User = NonNullable<ReturnType<typeof useUser>["user"]>;
type Totp = Awaited<ReturnType<User["createTOTP"]>>;

/** The typed key in groups of four, as authenticator apps show it: "JBSW Y3DP EHPK". */
export const groupsOfFour = (secret: string) =>
  secret.match(/.{1,4}/g)?.join(" ") ?? "";

/**
 * The enrolment of an authenticator app (the TotpEnrol board). Clerk makes the secret; the browser draws its QR code, so the
 * secret goes to no other server, ours included, and to no QR service. The six digits the app shows then turn the factor on, and
 * Clerk makes the ten backup codes.
 */
export function TotpEnrol({
  user,
  onEnrolled,
}: {
  user: User;
  onEnrolled: (backupCodes: string[]) => void;
}) {
  const t = useTranslations();
  const [totp, setTotp] = useState<Totp>();
  const [code, setCode] = useState("");
  const [error, setError] = useState<SignInErrorKey>();
  const [pending, setPending] = useState(false);
  const [copied, setCopied] = useState(false);
  // React runs an effect twice in development; a second createTOTP would replace the secret the QR code shows.
  const started = useRef(false);

  useEffect(() => {
    if (started.current) return;
    started.current = true;
    user
      .createTOTP()
      .then(setTotp)
      .catch((reason: unknown) => setError(signInErrorKey(reason, "totp")));
  }, [user]);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setPending(true);
    try {
      await user.verifyTOTP({ code });
      const backup = await user.createBackupCode();
      onEnrolled(backup.codes);
    } catch (reason) {
      setError(signInErrorKey(reason, "totp"));
      setPending(false);
    }
  };

  const copy = async () => {
    await navigator.clipboard.writeText(totp?.secret ?? "");
    setCopied(true);
  };

  return (
    <>
      <div className="flex flex-col gap-2">
        <h1 className="text-page-title">{t("twoStep.enrolTitle")}</h1>
        <p className="text-body text-muted-foreground">
          {t("twoStep.enrolText")}
        </p>
      </div>
      <AuthCard>
        <form noValidate onSubmit={submit} className="flex flex-col gap-4">
          <ol className="flex list-outside list-decimal flex-col gap-4 ps-5 text-body">
            <li>
              {t("twoStep.scan")}
              <div className="mt-3 flex flex-col gap-3">
                {/* Dark modules on a light ground in both themes, so every camera reads the code. */}
                <div className="self-start rounded-md bg-background p-3 text-foreground dark:bg-foreground dark:text-background">
                  {totp?.uri ? (
                    <QRCodeSVG
                      value={totp.uri}
                      size={160}
                      fgColor="currentColor"
                      bgColor="transparent"
                      title={t("twoStep.qrLabel")}
                    />
                  ) : (
                    <div className="size-40" />
                  )}
                </div>
                <div className="flex flex-wrap items-end gap-2">
                  <Input
                    label={t("twoStep.cannotScan")}
                    readOnly
                    value={groupsOfFour(totp?.secret ?? "")}
                    dir="ltr"
                    className="min-w-0 flex-1 basis-64 [&_input]:font-mono"
                  />
                  <Button
                    type="button"
                    onClick={() => void copy()}
                    disabled={!totp?.secret}
                  >
                    {copied ? t("twoStep.copied") : t("twoStep.copyKey")}
                  </Button>
                </div>
              </div>
            </li>
            <li>
              <CodeInput
                label={t("twoStep.enterCode")}
                value={code}
                onChange={setCode}
                error={error && t(`signIn.errors.${error}`)}
                disabled={pending || !totp}
              />
            </li>
          </ol>
          <Button
            type="submit"
            variant="primary"
            size="lg"
            className="w-full"
            disabled={pending || !totp}
          >
            {t("twoStep.turnOn")}
          </Button>
        </form>
      </AuthCard>
    </>
  );
}
