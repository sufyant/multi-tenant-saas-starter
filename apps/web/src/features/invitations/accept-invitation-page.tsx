"use client";

import { useAuth, useClerk, useSignUp, useUser } from "@clerk/nextjs";
import {
  acceptInvitation,
  actionFor,
  ErrorCode,
  getGetMeQueryKey,
  getGetMeQueryOptions,
  getGetTermsInForceQueryKey,
  getInvitationPreview,
  getListMyTenantsQueryKey,
  InvitationResponseState,
  type MyTenantResponse,
  ProblemDetailsError,
} from "@repo/api-client";
import { brand } from "@repo/brand";
import {
  SignIn,
  type SignInErrorKey,
  signInErrorKey,
} from "@repo/ui/components/sign-in";
import { Skeleton } from "@repo/ui/components/skeleton";
import { showToast } from "@repo/ui/components/toast";
import { useFreshRouter } from "@repo/ui/lib/use-fresh-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useEffect, useRef, useState } from "react";
import { reportError } from "@/error-tracking/report-error";
import { setLocale } from "@/i18n/set-locale";
import { ProblemScreen } from "@/shared/problem-screen";
import { termsInForceQueryOptions, useTermsUrl } from "@/shared/terms-link";
import { InvitationEnded } from "./invitation-ended";
import { InvitationFrame } from "./invitation-frame";
import { JoinCard } from "./join-card";
import { SystemAdminCannotJoin, TenantSuspended } from "./join-refused";
import { type NewPerson, NewPersonForm } from "./new-person-form";
import { WrongEmail } from "./wrong-email";

const codeOf = (error: Error | null) =>
  error instanceof ProblemDetailsError ? error.problem?.code : undefined;

/** This page without its code: the code waits in this tab's sessionStorage while the person signs in, so it never goes into a
 * URL we build, nor to Clerk as a redirect address. */
const page = "/invitations/accept";
const storedCode = "invitation-code";

/** The code of the link, else the one this tab kept before sign-in; undefined until the browser has read it. */
function useInvitationCode(fromLink: string): string | undefined {
  const [code, setCode] = useState(fromLink || undefined);
  useEffect(() => {
    if (fromLink) return;
    try {
      setCode(sessionStorage.getItem(storedCode) ?? "");
    } catch {
      setCode("");
    }
  }, [fromLink]);
  return code;
}

const keepCode = (code: string) => {
  try {
    sessionStorage.setItem(storedCode, code);
  } catch {
    // Without storage the person opens the link from the email again.
  }
};

/**
 * `/invitations/accept?code=...` (F44). A new person arrives from Clerk's invitation with `__clerk_ticket`: the ticket signs
 * them up and verifies the invited address. A signed-out person without a ticket signs in on this page, which names no
 * organisation, because the preview answers only the invited address (frame 1.13; the maintainer's decision of 2026-11-07), and
 * comes back to it. A signed-in person sees
 * the invitation (GetInvitationPreview) and joins (AcceptInvitation). The code is the invitation's secret: it goes only into the
 * bodies of those two calls, never into a log, a toast or a message.
 */
export function AcceptInvitationPage({
  code: fromLink,
  ticket,
}: {
  code: string;
  ticket?: string;
}) {
  const code = useInvitationCode(fromLink);
  const t = useTranslations();
  const router = useFreshRouter(useRouter());
  const queryClient = useQueryClient();
  const { isLoaded, isSignedIn } = useAuth();
  const { user } = useUser();
  const { signOut } = useClerk();
  const { signUp } = useSignUp();
  const [ticketError, setTicketError] = useState<SignInErrorKey>();
  const signingUp = useRef(false);
  const termsUrl = useTermsUrl();

  const signedOut = isLoaded && !isSignedIn;

  // The ticket sign-up (Clerk, "Sign-up with application invitations"): no names, no password; the session starts on this page.
  // biome-ignore lint/correctness/useExhaustiveDependencies: it runs once for a signed-out person with a ticket.
  useEffect(() => {
    if (!signedOut || signingUp.current || code === undefined) return;
    keepCode(code);
    if (!ticket) return;
    signingUp.current = true;
    void (async () => {
      const { error } = await signUp.ticket({ ticket });
      if (error)
        return setTicketError(signInErrorKey(error, "ticket", reportError));
      if (signUp.status !== "complete") return setTicketError("generic");
      await signUp.finalize({
        navigate: ({ decorateUrl }) =>
          window.location.assign(decorateUrl(page)),
      });
    })();
  }, [signedOut, ticket, code]);

  const preview = useQuery({
    // The code stays in this browser's memory: the key is never sent anywhere.
    queryKey: ["getInvitationPreview", code],
    queryFn: () => getInvitationPreview({ code: code ?? "" }),
    enabled: Boolean(isSignedIn) && Boolean(code),
    retry: false,
  });
  const me = useQuery({
    ...getGetMeQueryOptions(),
    enabled: Boolean(isSignedIn),
    retry: false,
  });
  // The version in force that a new person accepts with the invitation (frame 1.15); the footer's "Terms" reads the same answer.
  const termsInForce = useQuery(termsInForceQueryOptions());

  const join = useMutation({
    mutationFn: (person?: NewPerson) =>
      acceptInvitation({ code: code ?? "", ...person }),
    // The version in force changed while the person read it: they read the new one before they accept it.
    onError: (error) => {
      if (codeOf(error) === ErrorCode.TermsVersionChanged)
        void queryClient.invalidateQueries({
          queryKey: getGetTermsInForceQueryKey(),
        });
    },
    onSuccess: async (tenant: MyTenantResponse) => {
      try {
        sessionStorage.removeItem(storedCode);
      } catch {
        // Nothing was kept.
      }
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: getListMyTenantsQueryKey() }),
        queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() }),
      ]);
      showToast({ title: t("invitations.joined", { tenant: tenant.name }) });
      await router.push(`/${tenant.slug}`);
    },
  });

  const leave = () => void signOut({ redirectUrl: "/sign-in" });
  // Another account: the page keeps the code, signs out and signs the person in again here.
  const switchAccount = () => {
    if (code) keepCode(code);
    void signOut({ redirectUrl: page });
  };
  const email = user?.primaryEmailAddress?.emailAddress ?? "";

  if (ticketError)
    return (
      <InvitationFrame>
        <p role="alert" className="text-body text-destructive-text">
          {t(`signIn.errors.${ticketError}`)}
        </p>
      </InvitationFrame>
    );
  if (code === undefined) return <Skeleton label={t("common.loading")} />;
  if (code === "")
    return (
      <InvitationFrame footer>
        <InvitationEnded onSignOut={leave} />
      </InvitationFrame>
    );
  if (signedOut && !ticket)
    return (
      <SignIn
        title={t("invitations.signInTitle")}
        text={t("invitations.signInText")}
        secondFactorTitle={t("signInPage.secondFactorTitle")}
        secondFactorText={t("signInPage.secondFactorText")}
        brand={t("meta.title")}
        onLanguageChange={setLocale}
        privacyUrl={brand.privacyUrl}
        termsUrl={termsUrl}
        redirectTo={page}
        onUnexpectedError={reportError}
      />
    );
  if (!isSignedIn)
    return (
      <>
        <Skeleton label={t("invitations.settingUp")} />
        {/* Clerk's bot protection draws its check here during the ticket sign-up (Clerk, "Custom sign-up flow"). */}
        <div id="clerk-captcha" />
      </>
    );

  const previewCode = codeOf(preview.error);
  if (previewCode === ErrorCode.InvitationEmailMismatch)
    return (
      <InvitationFrame>
        <WrongEmail email={email} onSwitchAccount={switchAccount} />
      </InvitationFrame>
    );
  if (
    previewCode === ErrorCode.InvitationNotFound ||
    previewCode === ErrorCode.InvitationCodeRequired
  )
    return (
      <InvitationFrame footer>
        <InvitationEnded onSignOut={leave} />
      </InvitationFrame>
    );
  if (preview.error) return <ProblemScreen error={preview.error} />;
  const notAUserYet = codeOf(me.error) === ErrorCode.UserNotFound;
  if (me.error && !notAUserYet) return <ProblemScreen error={me.error} />;
  if (
    !preview.data ||
    (!me.data && !notAUserYet) ||
    (notAUserYet && termsInForce.isPending)
  )
    return <Skeleton label={t("common.loading")} />;

  const timeZone = me.data?.timeZone;
  const { state } = preview.data;
  if (state !== InvitationResponseState.Pending)
    return (
      <InvitationFrame footer>
        <InvitationEnded
          preview={{ ...preview.data, state }}
          timeZone={timeZone}
          onSignOut={leave}
        />
      </InvitationFrame>
    );

  // A failed join reads its answer as every screen does (F4): sign-in for a 401, the account's page for a suspended or deleted
  // user, the page of a system admin or a suspended organisation, the form's fields, else the message of the code.
  if (join.error instanceof ProblemDetailsError) {
    const kind = actionFor(join.error.status, join.error.problem).kind;
    if (
      kind === "sign-in" ||
      kind === "account-suspended" ||
      kind === "account-deleted"
    )
      return <ProblemScreen error={join.error} />;
  }
  const joinCode = codeOf(join.error);
  if (joinCode === ErrorCode.InvitationSystemAdminCannotJoin)
    return (
      <InvitationFrame>
        <SystemAdminCannotJoin onSwitchAccount={switchAccount} />
      </InvitationFrame>
    );
  if (joinCode === ErrorCode.TenantSuspended)
    return (
      <InvitationFrame>
        <TenantSuspended onSignOut={leave} />
      </InvitationFrame>
    );
  const action =
    join.error &&
    actionFor(
      join.error instanceof ProblemDetailsError ? join.error.status : 500,
      join.error instanceof ProblemDetailsError ? join.error.problem : null,
    );
  const fields = action?.kind === "field-errors" ? action.fields : {};
  const fieldErrors = {
    name: fields.name?.[0] && `validation.${fields.name[0]}`,
  };
  const key = action?.kind === "message" ? action.key : undefined;
  const message =
    key &&
    (t.has(key as "errors.server.error")
      ? t(key as "errors.server.error")
      : t("problems.errorText"));

  return (
    <InvitationFrame footer account>
      {notAUserYet ? (
        <NewPersonForm
          preview={preview.data}
          email={email}
          terms={termsInForce.data ?? undefined}
          onJoin={(person) => join.mutate(person)}
          pending={join.isPending}
          error={message}
          fieldErrors={fieldErrors}
        />
      ) : (
        <JoinCard
          preview={preview.data}
          timeZone={timeZone}
          onJoin={() => join.mutate(undefined)}
          pending={join.isPending}
          error={message}
        />
      )}
    </InvitationFrame>
  );
}
