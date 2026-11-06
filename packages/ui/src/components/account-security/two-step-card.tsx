"use client";

import { useTranslations } from "next-intl";
import { type FormEvent, useState } from "react";
import { Button } from "../button";
import { ConfirmIdentity } from "../confirm-identity";
import { Dialog } from "../dialog";
import { showToast } from "../toast";
import { BackupCodeList, TotpFields, useTotpEnrolment } from "../two-step";
import { Card, Row } from "./card";
import { useFailure } from "./failure";
import {
  authenticatorOn,
  createAuthenticator,
  isCancelled,
  newBackupCodes,
  readAgain,
  turnOffAuthenticator,
  useIdentity,
  useSensitive,
} from "./identity";

type Open = "setUp" | "codes" | "turnOff" | undefined;

/**
 * Two-step verification (F62): On or Off. On the platform it is required, on and has no action (F42). On the tenant web Set up
 * enrols an authenticator app (6.25) and shows the ten backup codes once (6.26); when it is on, New backup codes makes ten new
 * ones and Turn off asks for a code of the app or a backup code first (6.27 to 6.29).
 */
export function TwoStep({
  text,
  required,
  onUnexpectedError,
}: {
  text: string;
  required: boolean;
  onUnexpectedError?: (error: Error) => void;
}) {
  const t = useTranslations("accountSecurity");
  const { user } = useIdentity();
  const [open, setOpen] = useState<Open>();
  const [codes, setCodes] = useState<string[]>([]);
  const [firstCodes, setFirstCodes] = useState(false);
  const on = required || authenticatorOn(user);
  const failed = useFailure(onUnexpectedError);
  const making = useSensitive(
    () => (user ? newBackupCodes(user) : Promise.resolve([])),
    onUnexpectedError,
  );

  const showCodes = (next: string[], first: boolean) => {
    setCodes(next);
    setFirstCodes(first);
    setOpen("codes");
  };

  // However the codes' dialog closes, the person is read again: Clerk's verification does not write the factor back to them.
  const codesSeen = () => {
    setOpen(undefined);
    void readAgain(user);
    if (firstCodes) showToast({ title: t("isOn"), description: t("isOnText") });
  };

  const actions = required ? null : on ? (
    <>
      <Button
        onClick={() =>
          void making
            .run()
            .then((next) => showCodes(next, false))
            .catch(failed)
        }
      >
        {t("newCodes")}
      </Button>
      <Button onClick={() => setOpen("turnOff")}>{t("turnOff")}</Button>
    </>
  ) : (
    <Button onClick={() => setOpen("setUp")}>{t("setUp")}</Button>
  );

  return (
    <Card title={t("twoStepTitle")} text={text}>
      <Row action={actions}>
        <p className="text-body">{t("authenticatorApp")}</p>
        <p className="text-small text-muted-foreground">
          {t(on ? "on" : "off")}
        </p>
      </Row>
      {open === "setUp" && user && (
        <SetUp
          user={user}
          onUnexpectedError={onUnexpectedError}
          onCancel={() => setOpen(undefined)}
          onEnrolled={(next) => showCodes(next, true)}
        />
      )}
      {open === "codes" && (
        <Dialog
          open
          onOpenChange={(isOpen) => !isOpen && codesSeen()}
          title={t("codesTitle")}
          description={t("codesText")}
          actionLabel={t("savedCodes")}
          action="primary"
          onConfirm={codesSeen}
        >
          <BackupCodeList
            codes={codes}
            labels={{
              list: t("codesList"),
              copy: t("copyCodes"),
              download: t("downloadCodes"),
              fileName: t("codesFile"),
            }}
          />
        </Dialog>
      )}
      {open === "turnOff" && user && (
        <TurnOff
          onUnexpectedError={onUnexpectedError}
          onCancel={() => setOpen(undefined)}
          onDone={async () => {
            await turnOffAuthenticator(user);
            setOpen(undefined);
            showToast({ title: t("isOff"), description: t("isOffText") });
          }}
        />
      )}
      {making.confirm}
    </Card>
  );
}

/** Set up (6.25): Clerk makes the secret through reverification; the six digits the app shows turn the factor on. */
function SetUp({
  user,
  onEnrolled,
  onCancel,
  onUnexpectedError,
}: {
  user: NonNullable<ReturnType<typeof useIdentity>["user"]>;
  onEnrolled: (codes: string[]) => void;
  onCancel: () => void;
  onUnexpectedError?: (error: Error) => void;
}) {
  const t = useTranslations();
  const [code, setCode] = useState("");
  const creating = useSensitive(
    () => createAuthenticator(user),
    onUnexpectedError,
  );
  const { totp, verify, error, pending } = useTotpEnrolment({
    user,
    // A cancelled check closes Set up and says nothing; the promise then never settles, so the enrolment shows no error.
    create: () =>
      creating.run().catch((reason: unknown) => {
        if (!isCancelled(reason)) throw reason;
        onCancel();
        return new Promise<never>(() => {});
      }),
    onEnrolled,
    onUnexpectedError,
  });
  const submit = (event?: FormEvent) => {
    event?.preventDefault();
    if (!pending) void verify(code);
  };
  return (
    <Dialog
      open
      onOpenChange={(isOpen) => !isOpen && onCancel()}
      title={t("accountSecurity.setUpTitle")}
      actionLabel={t("accountSecurity.turnOn")}
      action="primary"
      cancelLabel={t("common.cancel")}
      actionDisabled={!totp}
      pending={pending}
      onConfirm={() => submit()}
    >
      <form noValidate onSubmit={submit}>
        <TotpFields
          totp={totp}
          code={code}
          onCodeChange={setCode}
          error={error && t(`signIn.errors.${error}`)}
          disabled={pending}
          labels={{
            scan: t("accountSecurity.scan"),
            qr: t("accountSecurity.qr"),
            cannotScan: t("accountSecurity.cannotScan"),
            copyKey: t("accountSecurity.copyKey"),
            copied: t("accountSecurity.copied"),
            enterCode: t("accountSecurity.enterCode"),
          }}
        />
      </form>
      {/* Inside Set up's dialog, so Base UI keeps the check on top of it as a nested dialog. */}
      {creating.confirm}
    </Dialog>
  );
}

/**
 * Turn off (6.28): the code of the app or a backup code is the recent check that Clerk asks for (`ConfirmIdentity` at the level
 * `second_factor`, with the question's own words), then the factor goes.
 */
function TurnOff({
  onDone,
  onCancel,
  onUnexpectedError,
}: {
  onDone: () => Promise<void>;
  onCancel: () => void;
  onUnexpectedError?: (error: Error) => void;
}) {
  const t = useTranslations("accountSecurity");
  const { session } = useIdentity();
  if (!session) return null;
  return (
    <ConfirmIdentity
      session={session}
      level="second_factor"
      onUnexpectedError={onUnexpectedError}
      onCancel={onCancel}
      onVerified={() =>
        void onDone().catch((reason: unknown) => {
          if (reason instanceof Error) onUnexpectedError?.(reason);
          showToast({ title: t("notDone"), tone: "destructive" });
          onCancel();
        })
      }
      words={{
        title: t("turnOffTitle"),
        text: t("turnOffText"),
        action: t("turnOff"),
      }}
    />
  );
}
