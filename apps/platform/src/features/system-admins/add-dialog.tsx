"use client";

import { useSession } from "@clerk/nextjs";
import {
  ErrorCode,
  getListUsersQueryOptions,
  grantSystemAdmin,
  problemOf,
  schemas,
  type UserResponse,
} from "@repo/api-client";
import { Avatar } from "@repo/ui/components/avatar";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { Input } from "@repo/ui/components/input";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useIntentKey } from "@repo/ui/lib/use-intent-key";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useEffect, useState } from "react";
import { z } from "zod";
import { savedUnderKey } from "@/shared/saved-under-key";
import { ReverifyStep } from "./reverify-step";
import {
  adminNameOf,
  afterRefusal,
  needsReverification,
  refreshSystemAdmin,
} from "./system-admin";

/** The lookup waits this long after the last key, as a list's search does (F9), so a typed address sends one request. */
const lookupDelay = 300;

const email = z.email();
const same = (a: string, b: string) => a.toLowerCase() === b.toLowerCase();

type Step = "find" | "confirm" | "reverify";

/**
 * Add a system admin (the MakeSystemAdmin board). Step 1 takes the full email, with no suggestions: the screen reads ListUsers
 * with `q` and keeps only the user whose email equals the typed one, case ignored. Step 2 warns and confirms with a secondary
 * button. GrantSystemAdmin sends one Idempotency-Key per intent (F48); after a stale second factor (403, A6a) the code step
 * checks it again and the dialog sends the same request with the same key, which the refused request never claimed (I4).
 */
export function AddDialog({ onClose }: { onClose: () => void }) {
  const t = useTranslations("systemAdmins");
  const users = useTranslations("users");
  const common = useTranslations("common");
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const { session } = useSession();
  const { key, renew } = useIntentKey();
  const [step, setStep] = useState<Step>("find");
  const [typed, setTyped] = useState("");
  const [looked, setLooked] = useState("");

  const address = typed.trim();
  useEffect(() => {
    const valid = email.safeParse(address).success;
    const timer = setTimeout(
      () => setLooked(valid ? address : ""),
      lookupDelay,
    );
    return () => clearTimeout(timer);
  }, [address]);

  const lookup = useQuery(
    getListUsersQueryOptions(
      { q: looked, pageSize: schemas.listUsersQueryPageSizeMax },
      { query: { enabled: looked !== "" } },
    ),
  );
  const current = looked !== "" && same(looked, address);
  const person = current
    ? lookup.data?.items.find((user) => user.email && same(user.email, looked))
    : undefined;
  const noUser = current && lookup.isSuccess && !person;
  // Another person is another intent: it gets a new key (F48).
  const personId = person?.id;
  // biome-ignore lint/correctness/useExhaustiveDependencies: the key follows the person the email found.
  useEffect(renew, [personId, renew]);

  const grant = useMutation({
    mutationFn: ({ user, key }: { user: UserResponse; key: string }) =>
      grantSystemAdmin(user.id, { "Idempotency-Key": key }),
    onSuccess: (_, { user }) => refreshSystemAdmin(queryClient, user.id),
    onError: (error, { user }) => {
      // The step-up claimed no key (I4): the same key goes again after the code step.
      if (needsReverification(error)) return setStep("reverify");
      afterRefusal(queryClient, user.id, error);
      if (savedUnderKey(error)) renew();
    },
  });

  const send = (user: UserResponse) =>
    grant.isPending ||
    grant.mutate(
      { user, key },
      {
        onSuccess: () => {
          renew();
          onClose();
          showToast({ title: t("granted", { name: adminNameOf(user) }) });
        },
      },
    );

  const close = (open: boolean) => !open && onClose();

  if (step === "reverify" && person && session)
    return (
      <ReverifyStep
        session={session}
        onCancel={onClose}
        onVerified={() => {
          setStep("confirm");
          send(person);
        }}
      />
    );

  if (step !== "find" && person) {
    const name = adminNameOf(person);
    const failure = (() => {
      if (!grant.error || needsReverification(grant.error)) return null;
      switch (problemOf(grant.error)?.code) {
        case ErrorCode.SystemAdminExists:
          return { title: t("alreadyAdminRaced", { name }), text: null };
        case ErrorCode.UserSuspended:
          return { title: users("reactivateFirst", { name }), text: null };
        case ErrorCode.UserDeleted:
          return { title: users("wasDeleted"), text: null };
        case ErrorCode.UserNotFound:
          return { title: users("notFound"), text: null };
        default:
          return {
            title: users("notDone", { name }),
            text: failureText(problemOf(grant.error)),
          };
      }
    })();
    return (
      <Dialog
        open
        onOpenChange={close}
        title={t("confirmTitle", { name })}
        description={t("confirmText")}
        action="secondary"
        actionLabel={t("confirmAction")}
        cancelLabel={common("cancel")}
        back={{
          label: t("back"),
          onBack: () => {
            grant.reset();
            setStep("find");
          },
        }}
        pending={grant.isPending}
        onConfirm={() => send(person)}
      >
        <PersonCard person={person} />
        {failure && (
          <Banner tone="destructive" title={failure.title}>
            {failure.text}
          </Banner>
        )}
      </Dialog>
    );
  }

  return (
    <Dialog
      open
      onOpenChange={close}
      title={t("add")}
      description={t("addText")}
      actionLabel={t("continue")}
      cancelLabel={common("cancel")}
      actionDisabled={!person || person.isSystemAdmin}
      onConfirm={() => setStep("confirm")}
    >
      <form
        noValidate
        onSubmit={(event) => {
          event.preventDefault();
          if (person && !person.isSystemAdmin) setStep("confirm");
        }}
        className="flex flex-col gap-3"
      >
        <Input
          label={t("email")}
          type="email"
          dir="ltr"
          autoComplete="off"
          spellCheck={false}
          maxLength={schemas.listUsersQueryQMax}
          value={typed}
          onChange={(event) => setTyped(event.target.value)}
          error={noUser ? t("noUser") : undefined}
        />
        {person && <PersonCard person={person} />}
        {person?.isSystemAdmin && (
          <Banner
            tone="info"
            title={t("alreadyAdmin", { name: adminNameOf(person) })}
          />
        )}
      </form>
    </Dialog>
  );
}

/** The person the email found (the MakeSystemAdmin board): the name, the email and how many organisations they belong to. */
function PersonCard({ person }: { person: UserResponse }) {
  const t = useTranslations("systemAdmins");
  return (
    <div className="flex items-center gap-3 rounded-lg border border-border bg-card p-3">
      <Avatar
        person={{ firstName: person.firstName, lastName: person.lastName }}
        className="size-10"
      />
      <div className="flex min-w-0 flex-col">
        <bdi className="truncate font-medium">{adminNameOf(person)}</bdi>
        <span className="text-small text-muted-foreground">
          <bdi dir="ltr">{person.email}</bdi>
          {" · "}
          <span>{t("memberOf", { count: person.membershipCount })}</span>
        </span>
      </div>
    </div>
  );
}
