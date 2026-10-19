"use client";

import { useAuth, useClerk, useSignUp, useUser } from "@clerk/nextjs";
import {
  acceptInvitation,
  ErrorCode,
  fieldErrorsOf,
  getGetMeQueryKey,
  getGetMeQueryOptions,
  getInvitationPreview,
  getListMyTenantsQueryKey,
  InvitationResponseState,
  type MyTenantResponse,
  ProblemDetailsError,
} from "@repo/api-client";
import {
  redirectParameter,
  type SignInErrorKey,
  signInErrorKey,
} from "@repo/ui/components/sign-in";
import { Skeleton } from "@repo/ui/components/skeleton";
import { showToast } from "@repo/ui/components/toast";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useEffect, useRef, useState } from "react";
import { ProblemScreen } from "@/shared/problem-screen";
import { InvitationEnded } from "./invitation-ended";
import { InvitationFrame } from "./invitation-header";
import { JoinCard } from "./join-card";
import { type Names, NewPersonForm } from "./new-person-form";
import { WrongEmail } from "./wrong-email";

const codeOf = (error: Error | null) =>
  error instanceof ProblemDetailsError ? error.problem?.code : undefined;

/** This page with its code and nothing else: no ticket, so a reload never signs up twice. */
const pageOf = (code: string) =>
  `/invitations/accept?code=${encodeURIComponent(code)}`;

/**
 * `/invitations/accept?code=...` (F44). A new person arrives from Clerk's invitation with `__clerk_ticket`: the ticket signs
 * them up and verifies the invited address. A signed-out person without a ticket signs in and comes back. A signed-in person sees
 * the invitation (GetInvitationPreview) and joins (AcceptInvitation). The code is the invitation's secret: it goes only into the
 * bodies of those two calls, never into a log, a toast or a message.
 */
export function AcceptInvitationPage({
  code,
  ticket,
}: {
  code: string;
  ticket?: string;
}) {
  const t = useTranslations();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { isLoaded, isSignedIn } = useAuth();
  const { user } = useUser();
  const { signOut } = useClerk();
  const { signUp } = useSignUp();
  const [ticketError, setTicketError] = useState<SignInErrorKey>();
  const signingUp = useRef(false);

  const page = pageOf(code);
  const signedOut = isLoaded && !isSignedIn;

  // The ticket sign-up (Clerk, "Sign-up with application invitations"): no names, no password; the session starts on this page.
  // biome-ignore lint/correctness/useExhaustiveDependencies: it runs once for a signed-out person with a ticket.
  useEffect(() => {
    if (!signedOut || signingUp.current) return;
    if (!ticket) {
      window.location.assign(
        `/sign-in?${redirectParameter}=${encodeURIComponent(page)}`,
      );
      return;
    }
    signingUp.current = true;
    void (async () => {
      const { error } = await signUp.ticket({ ticket });
      if (error) return setTicketError(signInErrorKey(error, "ticket"));
      if (signUp.status !== "complete") return setTicketError("generic");
      await signUp.finalize({
        navigate: ({ decorateUrl }) =>
          window.location.assign(decorateUrl(page)),
      });
    })();
  }, [signedOut, ticket]);

  const preview = useQuery({
    // The code stays in this browser's memory: the key is never sent anywhere.
    queryKey: ["getInvitationPreview", code],
    queryFn: () => getInvitationPreview({ code }),
    enabled: Boolean(isSignedIn) && code !== "",
    retry: false,
  });
  const me = useQuery({
    ...getGetMeQueryOptions(),
    enabled: Boolean(isSignedIn),
    retry: false,
  });

  const join = useMutation({
    mutationFn: (names?: Names) => acceptInvitation({ code, ...names }),
    onSuccess: async (tenant: MyTenantResponse) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: getListMyTenantsQueryKey() }),
        queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() }),
      ]);
      showToast({ title: t("invitations.joined", { tenant: tenant.name }) });
      router.push(`/${tenant.slug}`);
    },
  });

  const leave = () => void signOut({ redirectUrl: "/sign-in" });
  const email = user?.primaryEmailAddress?.emailAddress ?? "";

  if (ticketError)
    return (
      <InvitationFrame>
        <p role="alert" className="text-body text-destructive-text">
          {t(`signIn.errors.${ticketError}`)}
        </p>
      </InvitationFrame>
    );
  if (code === "")
    return (
      <InvitationFrame>
        <InvitationEnded onSignOut={leave} />
      </InvitationFrame>
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
        <WrongEmail
          email={email}
          onSwitchAccount={() =>
            void signOut({
              redirectUrl: `/sign-in?${redirectParameter}=${encodeURIComponent(page)}`,
            })
          }
        />
      </InvitationFrame>
    );
  if (
    previewCode === ErrorCode.InvitationNotFound ||
    previewCode === ErrorCode.InvitationCodeRequired
  )
    return (
      <InvitationFrame>
        <InvitationEnded onSignOut={leave} />
      </InvitationFrame>
    );
  if (preview.error) return <ProblemScreen error={preview.error} />;
  const notAUserYet = codeOf(me.error) === ErrorCode.UserNotFound;
  if (me.error && !notAUserYet) return <ProblemScreen error={me.error} />;
  if (!preview.data || (!me.data && !notAUserYet))
    return <Skeleton label={t("common.loading")} />;

  const timeZone = me.data?.timeZone;
  if (preview.data.state !== InvitationResponseState.Pending)
    return (
      <InvitationFrame>
        <InvitationEnded
          preview={preview.data}
          timeZone={timeZone}
          onSignOut={leave}
        />
      </InvitationFrame>
    );

  const joinError = join.error as Error | null;
  const joinCode = codeOf(joinError);
  const fields =
    joinError instanceof ProblemDetailsError && joinError.problem
      ? fieldErrorsOf(joinError.problem)
      : {};
  const fieldErrors = {
    firstName: fields.firstName?.[0] && `validation.${fields.firstName[0]}`,
    lastName: fields.lastName?.[0] && `validation.${fields.lastName[0]}`,
  };
  const message =
    joinError && Object.keys(fields).length === 0
      ? t.has(`errors.${joinCode}` as "errors.server.error")
        ? t(`errors.${joinCode}` as "errors.server.error")
        : t("problems.errorText")
      : undefined;

  return (
    <InvitationFrame>
      {notAUserYet ? (
        <NewPersonForm
          preview={preview.data}
          email={email}
          onJoin={(names) => join.mutate(names)}
          pending={join.isPending}
          error={message}
          fieldErrors={fieldErrors}
        />
      ) : (
        <JoinCard
          preview={preview.data}
          email={me.data?.email ?? email}
          timeZone={timeZone}
          onJoin={() => join.mutate(undefined)}
          pending={join.isPending}
          error={message}
        />
      )}
    </InvitationFrame>
  );
}
