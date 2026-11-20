"use client";

import { useClerk } from "@clerk/nextjs";
import {
  ErrorCode,
  getGetMeQueryKey,
  type Language,
  ProblemDetailsError,
  problemOf,
  signUp,
} from "@repo/api-client";
import { AuthHeading, AuthScreen } from "@repo/ui/components/auth-layout";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { Checkbox } from "@repo/ui/components/ui/checkbox";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useFreshRouter } from "@repo/ui/lib/use-fresh-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useLocale, useTranslations } from "next-intl";
import { useId, useState } from "react";
import { newOrganisationPage } from "@/shared/sign-up";
import { TermsAuthFooter, termsInForceQueryOptions } from "@/shared/terms-link";

/**
 * Accept the terms to create the account (frame 1.27): a person who signed up and is not a user yet (GetMe 404 `user.not_found`),
 * while the product has sign-up on. Accept sends SignUp with the page's language and the version in force the person read, or none
 * before the first version, which needs no box. While the version in force cannot be read, Accept stays off: the account never
 * starts without the terms. The new user then creates their first organisation (1.28).
 */
export function CreateAccount() {
  const t = useTranslations();
  const locale = useLocale();
  const router = useFreshRouter(useRouter());
  const { signOut } = useClerk();
  const queryClient = useQueryClient();
  const failureText = useFailureText();
  const agreeId = useId();
  const [agreed, setAgreed] = useState(false);
  const terms = useQuery(termsInForceQueryOptions());
  const version = terms.data;
  const create = useMutation({
    mutationFn: () =>
      signUp({
        language: locale as Language,
        termsVersionId: version?.id ?? null,
      }),
    onSuccess: (me) => {
      queryClient.setQueryData(getGetMeQueryKey(), me);
      router.push(newOrganisationPage);
    },
    onError: (error) => {
      // The version changed while the person read it: they read the new one, so the box starts empty again.
      if (codeOf(error) === ErrorCode.TermsVersionChanged) {
        setAgreed(false);
        void queryClient.invalidateQueries({
          queryKey: termsInForceQueryOptions().queryKey,
        });
      }
    },
  });

  return (
    <AuthScreen brand={t("meta.title")} footer={<TermsAuthFooter />}>
      {create.error && (
        <Banner tone="destructive" title={t("createAccount.notCreated")}>
          {failureText(problemOf(create.error))}
        </Banner>
      )}
      <AuthHeading
        title={t("createAccount.title")}
        text={t(
          terms.data === null
            ? "createAccount.textNoTerms"
            : "createAccount.text",
        )}
      />
      {version && (
        <>
          <Button
            variant="ghost"
            className="self-center"
            render={<a href={version.url} target="_blank" rel="noreferrer" />}
            nativeButton={false}
          >
            {t("terms.read")}
          </Button>
          <div className="flex items-start justify-center gap-2 text-body">
            <Checkbox
              id={agreeId}
              checked={agreed}
              onCheckedChange={setAgreed}
              disabled={create.isPending}
              className="mt-0.5"
            />
            <label htmlFor={agreeId}>{t("terms.agree")}</label>
          </div>
        </>
      )}
      <Button
        variant="primary"
        className="w-full"
        disabled={!terms.isSuccess || (version && !agreed) || create.isPending}
        onClick={() => create.mutate()}
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
