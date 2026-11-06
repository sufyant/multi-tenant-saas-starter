"use client";

import { useReverification, useSession, useUser } from "@clerk/nextjs";
import { type ReactNode, useCallback, useEffect, useState } from "react";
import { ConfirmIdentity, type VerificationLevel } from "../confirm-identity";

/**
 * The one folder that reads and changes the identity provider for Account and security (F62): the sign-in methods, the sessions
 * and the authenticator. Every change goes through Clerk's reverification: when Clerk asks for a recent check, "Confirm it's
 * you" opens, and the change runs again after it (the maintainer's decision of 2026-11-06).
 */

export type Provider = "google" | "microsoft";

export const providers: Provider[] = ["google", "microsoft"];

const strategies = {
  google: "oauth_google",
  microsoft: "oauth_microsoft",
} as const;

type IdentityUser = NonNullable<ReturnType<typeof useUser>["user"]>;

export type IdentitySession = Awaited<
  ReturnType<IdentityUser["getSessions"]>
>[number];

export type SessionsState =
  | { status: "loading" }
  | { status: "error" }
  | { status: "success"; sessions: IdentitySession[] };

/** The signed-in person at the identity provider, and this browser's session. */
export function useIdentity() {
  const { user } = useUser();
  const { session } = useSession();
  return { user, session };
}

/**
 * The person's active sessions, this one first, then by their last activity; `load` reads them again. Clerk keeps the list in a
 * cache, so a signed-out session leaves the list through `drop`, not through a new read.
 */
export function useSessions() {
  const { user, session } = useIdentity();
  const [state, setState] = useState<SessionsState>({ status: "loading" });
  const load = useCallback(async () => {
    if (!user) return;
    setState({ status: "loading" });
    try {
      const all = await user.getSessions();
      const current = (each: IdentitySession) =>
        Number(each.id === session?.id);
      setState({
        status: "success",
        sessions: [...all].sort(
          (a, b) =>
            current(b) - current(a) ||
            b.lastActiveAt.getTime() - a.lastActiveAt.getTime(),
        ),
      });
    } catch {
      setState({ status: "error" });
    }
  }, [user, session?.id]);
  useEffect(() => {
    void load();
  }, [load]);
  const drop = (gone: IdentitySession[]) =>
    setState((now) =>
      now.status === "success"
        ? {
            status: "success",
            sessions: now.sessions.filter((each) => !gone.includes(each)),
          }
        : now,
    );
  return { state, load, drop };
}

/** The account the person connected for a provider, if any, and only a verified one. */
export function connectedAccount(
  user: IdentityUser | null | undefined,
  provider: Provider,
) {
  return user?.externalAccounts.find(
    (account) =>
      account.provider === provider &&
      account.verification?.status === "verified",
  );
}

/** An attempt that the provider did not finish: the person refused, or closed the provider's page. */
export function unfinishedAccounts(
  user: IdentityUser | null | undefined,
  provider: Provider,
) {
  return (
    user?.externalAccounts.filter(
      (account) =>
        account.provider === provider &&
        account.verification?.status !== "verified",
    ) ?? []
  );
}

/**
 * Wraps a change to the identity provider in Clerk's reverification. It gives the change to call and the step to render: the
 * step shows only while Clerk waits for the check.
 */
export function useSensitive<Args extends unknown[], Result>(
  change: (...args: Args) => Promise<Result>,
  onUnexpectedError?: (error: Error) => void,
): { run: (...args: Args) => Promise<Result>; confirm: ReactNode } {
  const { session } = useSession();
  const [asked, setAsked] = useState<{
    level: VerificationLevel;
    complete: () => void;
    cancel: () => void;
  }>();
  const run = useReverification(change, {
    onNeedsReverification: ({ level, complete, cancel }) =>
      setAsked({ level: level ?? "first_factor", complete, cancel }),
  }) as (...args: Args) => Promise<Result>;
  const confirm =
    asked && session ? (
      <ConfirmIdentity
        session={session}
        level={asked.level}
        onUnexpectedError={onUnexpectedError}
        onVerified={() => {
          asked.complete();
          setAsked(undefined);
        }}
        onCancel={() => {
          asked.cancel();
          setAsked(undefined);
        }}
      />
    ) : null;
  return { run, confirm };
}

/** Sends the person to the provider to connect it; Clerk brings them back to `returnUrl`. */
export async function startConnecting(
  user: IdentityUser,
  provider: Provider,
  returnUrl: string,
) {
  const account = await user.createExternalAccount({
    strategy: strategies[provider],
    redirectUrl: returnUrl,
  });
  const next = account.verification?.externalVerificationRedirectURL;
  if (next) window.location.assign(next.href);
}
