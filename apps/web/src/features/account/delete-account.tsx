"use client";

import { useClerk } from "@clerk/nextjs";
import {
  confirmAccountDeletion,
  ErrorCode,
  getListMyTenantsQueryOptions,
  problemOf,
  requestAccountDeletion,
  schemas,
} from "@repo/api-client";
import { Card, Row } from "@repo/ui/components/account-security";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { CodeInput } from "@repo/ui/components/code-input";
import { Dialog } from "@repo/ui/components/dialog";
import { TextButton } from "@repo/ui/components/sign-in";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useIntentKey } from "@repo/ui/lib/use-intent-key";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { type FormEvent, type ReactNode, useState } from "react";
import { SupportLink } from "@/shared/support-link";
import { deletedEmailKey } from "./deleted-account";
import { useReadFailure } from "./use-read-failure";

/** The most organisations a sentence names; the rest are "n more" (F15). */
const named = 3;
const minute = 60_000;

type Open =
  | { step: "refused"; reason: ReactNode }
  | { step: "code"; minutes: number }
  | undefined;

/**
 * Delete my account (6.22 to 6.24, F62; `flows/user-deletion.md`, section 2): `RequestAccountDeletion` checks first and emails a
 * 6-digit code, as the email change does (the maintainer's decision of 2026-11-05; 6.23's typed address is a deviation). The last
 * Owner of an organisation is refused with its names, and the sentence ends as Leave's does, with "contact support", since only
 * the platform closes an organisation (#219, section 3). `ConfirmAccountDeletion` with the code deletes the account at once; the
 * person is signed out and sees the page of the deleted account, which names the address from this tab's storage.
 */
export function DeleteAccount({ email }: { email: string | null }) {
  const t = useTranslations();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const readFailure = useReadFailure();
  const { signOut } = useClerk();
  const { key, renew } = useIntentKey();
  const [open, setOpen] = useState<Open>();
  const [code, setCode] = useState("");
  const [error, setError] = useState<string>();
  const request = useMutation({
    mutationFn: (intent: string) =>
      requestAccountDeletion({ "Idempotency-Key": intent }),
  });
  const confirm = useMutation({
    mutationFn: (entered: string) => confirmAccountDeletion({ code: entered }),
  });

  /** The organisations the person is the last Owner of, by name: at most three, then "n more". */
  const lastOwnerOf = async (tenantIds: string[]) => {
    const mine = await queryClient.fetchQuery(
      getListMyTenantsQueryOptions({
        pageSize: schemas.listMyTenantsQueryPageSizeMax,
      }),
    );
    const names = mine.items
      .filter((item) => tenantIds.includes(item.id))
      .map((item) => item.name);
    const rest = tenantIds.length - Math.min(names.length, named);
    const items = [
      ...names.slice(0, named),
      ...(rest > 0
        ? [t("securityPage.moreOrganisations", { count: rest })]
        : []),
    ];
    return new Intl.ListFormat(t("locale.format"), { type: "conjunction" })
      .formatToParts(items)
      .map((part, index) =>
        part.type === "element" ? (
          // biome-ignore lint/suspicious/noArrayIndexKey: the parts of a formatted list have no id of their own.
          <bdi key={index}>{part.value}</bdi>
        ) : (
          part.value
        ),
      );
  };

  const ask = async (again: boolean) => {
    setError(undefined);
    try {
      const sent = await request.mutateAsync(key);
      renew();
      const left = new Date(sent.expiresAt).getTime() - Date.now();
      setOpen({
        step: "code",
        minutes: Math.max(1, Math.round(left / minute)),
      });
    } catch (reason) {
      const { problem, saved, handled } = readFailure(reason);
      if (saved) renew();
      if (handled) return setOpen(undefined);
      if (problem?.code === ErrorCode.UserLastOwner && !again) {
        const organisations = await lastOwnerOf(
          problemOf(reason)?.tenantIds ?? [],
        );
        return setOpen({
          step: "refused",
          reason: t.rich("securityPage.lastOwner", {
            organisations: () => organisations,
            support: (chunks) => <SupportLink>{chunks}</SupportLink>,
          }),
        });
      }
      if (again) return setError(failureText(problem));
      // Any other failure asks nothing of the person, so it is no refusal: a toast says what went wrong (F4).
      showToast({
        title: t("accountSecurity.notDone"),
        description: failureText(problem),
        tone: "destructive",
      });
    }
  };

  const remove = async (event?: FormEvent) => {
    event?.preventDefault();
    if (confirm.isPending) return;
    setError(undefined);
    try {
      await confirm.mutateAsync(code);
      if (email) sessionStorage.setItem(deletedEmailKey, email);
      await signOut({ redirectUrl: "/account/deleted" });
    } catch (reason) {
      const { problem, handled } = readFailure(reason);
      if (handled) return setOpen(undefined);
      setError(failureText(problem));
    }
  };

  const close = () => {
    setOpen(undefined);
    setCode("");
    setError(undefined);
  };

  return (
    <Card
      title={t("securityPage.deleteTitle")}
      text={t("securityPage.deleteText")}
      tone="destructive"
    >
      <Row
        action={
          <Button
            variant="destructive"
            disabled={request.isPending}
            onClick={() => void ask(false)}
          >
            {t("securityPage.deleteTitle")}
          </Button>
        }
      >
        <p className="text-body">{t("securityPage.deleteTitle")}</p>
      </Row>
      {open?.step === "refused" && (
        <Dialog
          open
          onOpenChange={(isOpen) => !isOpen && close()}
          title={t("securityPage.cannotDeleteTitle")}
          actionLabel={t("common.close")}
        >
          <Banner tone="destructive" title={open.reason} />
        </Dialog>
      )}
      {open?.step === "code" && (
        <Dialog
          open
          onOpenChange={(isOpen) => !isOpen && close()}
          title={t("securityPage.confirmTitle")}
          description={t("securityPage.confirmText")}
          tone="destructive"
          actionLabel={t("securityPage.deleteTitle")}
          cancelLabel={t("common.cancel")}
          pending={confirm.isPending}
          onConfirm={() => void remove()}
        >
          <form
            noValidate
            onSubmit={(event) => void remove(event)}
            className="flex flex-col gap-2"
          >
            <p className="text-small text-muted-foreground">
              {t.rich("securityPage.codeSent", {
                email: () => <bdi dir="ltr">{email}</bdi>,
                minutes: open.minutes,
              })}
            </p>
            <CodeInput
              label={t("signIn.code")}
              value={code}
              onChange={setCode}
              error={error}
              disabled={confirm.isPending}
              autoFocus
            />
            <p className="text-small text-muted-foreground">
              <TextButton
                disabled={request.isPending}
                onClick={() => void ask(true)}
              >
                {t("signIn.sendNewCode")}
              </TextButton>
            </p>
          </form>
        </Dialog>
      )}
    </Card>
  );
}
