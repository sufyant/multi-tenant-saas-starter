"use client";

import { useClerk } from "@clerk/nextjs";
import {
  acceptTermsVersion,
  actionFor,
  ErrorCode,
  getGetMyTermsQueryKey,
  type MyTermsResponse,
  type MyTermsVersionResponse,
  ProblemDetailsError,
} from "@repo/api-client";
import { AuthHeading, AuthScreen } from "@repo/ui/components/auth-layout";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { Checkbox } from "@repo/ui/components/ui/checkbox";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useId, useState } from "react";
import { ProblemScreen } from "@/shared/problem-screen";
import { useTermsDay } from "@/shared/terms-day";

/**
 * The gate after sign-in (frames 1.09 to 1.12, 1.14, 1.55, 1.56): a version that requires acceptance, with the day it applies from,
 * its link, its summary when it has one, the box that enables Accept, and Sign out. The summary is the system admin's own text,
 * never translated. Accept sends the link the person read; the answer says what comes next, so a scheduled version may ask in turn.
 */
export function TermsAcceptance({
  version,
}: {
  version: MyTermsVersionResponse;
}) {
  const t = useTranslations();
  const day = useTermsDay();
  const { signOut } = useClerk();
  const queryClient = useQueryClient();
  const agreeId = useId();
  const [agreed, setAgreed] = useState(false);
  const accept = useMutation({
    mutationFn: () => acceptTermsVersion(version.id, { url: version.url }),
    onSuccess: (next: MyTermsResponse) =>
      queryClient.setQueryData(getGetMyTermsQueryKey(), next),
    onError: (error) => {
      // The version changed since the person opened it: they read the new one, so the box starts empty again.
      if (codeOf(error) === ErrorCode.TermsVersionChanged) {
        setAgreed(false);
        void queryClient.invalidateQueries({
          queryKey: getGetMyTermsQueryKey(),
        });
      }
    },
  });

  const action =
    accept.error instanceof ProblemDetailsError
      ? actionFor(accept.error.status, accept.error.problem)
      : undefined;
  // A 401 or a blocked account leaves the gate for the page that says so (F4).
  if (
    accept.error &&
    action &&
    action.kind !== "message" &&
    action.kind !== "field-errors"
  )
    return <ProblemScreen error={accept.error} />;
  const failure =
    accept.error &&
    (action?.kind === "message" && t.has(action.key as "problems.errorText")
      ? t(action.key as "problems.errorText")
      : t("terms.saveFailedText"));

  return (
    <AuthScreen brand={t("meta.title")}>
      {failure && (
        <Banner tone="destructive" title={t("terms.saveFailedTitle")}>
          {failure}
        </Banner>
      )}
      <AuthHeading
        title={t("terms.title")}
        text={t("terms.text", {
          date: day(version.effectiveDate),
          product: t("meta.title"),
        })}
      />
      <Button
        variant="ghost"
        className="self-center"
        render={<a href={version.url} target="_blank" rel="noreferrer" />}
        nativeButton={false}
      >
        {t("terms.read")}
      </Button>
      {version.summary && (
        <section
          aria-label={t("terms.summary")}
          className="flex flex-col gap-1.5 rounded-lg bg-sidebar p-3.5"
        >
          <h2 className="text-small font-medium text-muted-foreground">
            {t("terms.summary")}
          </h2>
          {/* The system admin's own text, in one language: its own direction, so an English summary reads left to right in Arabic (1.56). */}
          <p dir="auto" className="text-body whitespace-pre-line">
            {version.summary}
          </p>
        </section>
      )}
      <div className="flex items-start justify-center gap-2 text-body">
        <Checkbox
          id={agreeId}
          checked={agreed}
          onCheckedChange={setAgreed}
          disabled={accept.isPending}
          className="mt-0.5"
        />
        <label htmlFor={agreeId}>{t("terms.agree")}</label>
      </div>
      <Button
        variant="primary"
        className="w-full"
        disabled={!agreed || accept.isPending}
        onClick={() => accept.mutate()}
      >
        {t("terms.accept")}
      </Button>
      <Button
        variant="ghost"
        className="w-full"
        onClick={() => void signOut({ redirectUrl: "/sign-in" })}
      >
        {t("common.signOut")}
      </Button>
    </AuthScreen>
  );
}

const codeOf = (error: Error) =>
  error instanceof ProblemDetailsError ? error.problem?.code : undefined;
