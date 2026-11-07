"use client";

import type { useUser } from "@clerk/nextjs";
import { AuthHeading } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import { TotpFields, useTotpEnrolment } from "@repo/ui/components/two-step";
import { useTranslations } from "next-intl";
import { type FormEvent, useState } from "react";
import { reportError } from "@/error-tracking/report-error";

type User = NonNullable<ReturnType<typeof useUser>["user"]>;

/**
 * The enrolment of an authenticator app at the platform's gate (platform frame 1.03, F42): the shared fields and Clerk calls of
 * `packages/ui` with the gate's words. The six digits the app shows turn the factor on, and Clerk makes the ten backup codes.
 */
export function TotpEnrol({
  user,
  onEnrolled,
}: {
  user: User;
  onEnrolled: (backupCodes: string[]) => void;
}) {
  const t = useTranslations();
  const [code, setCode] = useState("");
  const { totp, verify, error, pending } = useTotpEnrolment({
    user,
    onEnrolled,
    onUnexpectedError: reportError,
  });

  const submit = (event: FormEvent) => {
    event.preventDefault();
    void verify(code);
  };

  return (
    <>
      <AuthHeading
        title={t("twoStep.enrolTitle")}
        text={t("twoStep.enrolText")}
      />
      <form noValidate onSubmit={submit} className="flex flex-col gap-4">
        <TotpFields
          centred
          totp={totp}
          code={code}
          onCodeChange={setCode}
          error={error && t(`signIn.errors.${error}`)}
          disabled={pending}
          labels={{
            scan: t("twoStep.scan"),
            qr: t("twoStep.qrLabel"),
            cannotScan: t("twoStep.cannotScan"),
            copyKey: t("twoStep.copyKey"),
            copied: t("twoStep.copied"),
            enterCode: t("twoStep.enterCode"),
          }}
        />
        <Button
          type="submit"
          variant="primary"
          className="w-full"
          disabled={pending || !totp}
        >
          {t("twoStep.turnOn")}
        </Button>
      </form>
    </>
  );
}
