"use client";

import { useTranslations } from "next-intl";
import { type ReactNode, useEffect, useRef, useState } from "react";
import { formatRelative, type Instant } from "../../lib/format-date-time";
import { Button } from "../button";
import { CopyButton } from "../copy-button";
import { Dialog } from "../dialog";
import { Icon } from "../icon";
import { CircleXIcon, MailIcon } from "../icon/icons";
import { Label } from "../label";
import { Pill } from "../pill";
import { InlineSkeleton } from "../skeleton";
import { showToast } from "../toast";
import {
  connectedAccount,
  type IdentitySession,
  type Provider,
  providers,
  startConnecting,
  unfinishedAccounts,
  useIdentity,
  useSensitive,
  useSessions,
} from "./identity";

export type AccountSecurityProps = {
  /** The line under the page's title, in the app's words: the person's name and where the settings apply. */
  intro: string;
  /** The address our API keeps (A7). */
  email: string | null;
  /** The app's words for two-step verification; on the platform it is required and has no action. */
  twoStep: { text: string; required: boolean };
  /** This page's path: the provider brings the person back to it after Connect. */
  returnPath: string;
  /** The person's time zone, for the time of a session's last activity (F18). */
  timeZone?: string | null;
  /** The cards that read our API, under the identity provider's: Your data and Delete my account (step 36). */
  children?: ReactNode;
  onUnexpectedError?: (error: Error) => void;
  /** Tests fix the clock. */
  now?: Instant;
};

const connectedParameter = "connected";

/**
 * Settings › Account and security of both apps (F62, the Account and security screens): the email, the sign-in methods,
 * two-step verification and the sessions, then the app's own cards. The identity provider's parts read and change Clerk through
 * `identity.tsx`; the apps give their words and our API's data.
 */
export function AccountSecurity({
  intro,
  email,
  twoStep,
  returnPath,
  timeZone,
  children,
  onUnexpectedError,
  now,
}: AccountSecurityProps) {
  const t = useTranslations("accountSecurity");
  const shell = useTranslations("appShell");
  return (
    <section className="flex min-w-0 max-w-[760px] flex-col gap-4">
      <div>
        <h1 className="text-page-title">{shell("accountAndSecurity")}</h1>
        <p className="mt-1 text-body text-muted-foreground">{intro}</p>
      </div>
      <Card title={t("emailTitle")} text={t("emailText")}>
        <Row>
          <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-2">
            <span className="hidden size-8 items-center justify-center rounded-md bg-muted tablet:flex">
              <Icon icon={MailIcon} />
            </span>
            <bdi dir="ltr" className="min-w-0 break-all text-body">
              {email}
            </bdi>
            {email && <CopyButton value={email} label={t("emailTitle")} />}
            <Pill tone="success">{t("verified")}</Pill>
          </div>
        </Row>
      </Card>
      <SignInMethods
        returnPath={returnPath}
        onUnexpectedError={onUnexpectedError}
      />
      <TwoStep {...twoStep} />
      <Sessions
        timeZone={timeZone}
        now={now}
        onUnexpectedError={onUnexpectedError}
      />
      {children}
    </section>
  );
}

/** A card of the page: its title and text, then its rows. `lead` is a first sentence that a phone leaves out (6.07). */
export function Card({
  title,
  lead,
  text,
  children,
}: {
  title: string;
  lead?: string;
  text: string;
  children: ReactNode;
}) {
  const id = useRef(`card-${title.replaceAll(" ", "-")}`).current;
  return (
    <section
      aria-labelledby={id}
      className="flex flex-col rounded-lg border border-border bg-card"
    >
      <div className="flex flex-col gap-1 px-5 pt-4 pb-3">
        <h2 id={id} className="text-section-title">
          {title}
        </h2>
        <p className="text-small text-muted-foreground">
          {lead && <span className="hidden tablet:inline">{lead} </span>}
          {text}
        </p>
      </div>
      {children}
    </section>
  );
}

/** A row of a card: its words, and its action at the end, under them on a phone. */
export function Row({
  children,
  action,
  as: Tag = "div",
}: {
  children: ReactNode;
  action?: ReactNode;
  as?: "div" | "li";
}) {
  return (
    <Tag className="flex flex-col gap-3 border-t border-border px-5 py-3 tablet:flex-row tablet:items-center tablet:justify-between">
      <div className="min-w-0">{children}</div>
      {action && <div className="shrink-0">{action}</div>}
    </Tag>
  );
}

function SignInMethods({
  returnPath,
  onUnexpectedError,
}: {
  returnPath: string;
  onUnexpectedError?: (error: Error) => void;
}) {
  const t = useTranslations("accountSecurity");
  const { user } = useIdentity();
  const [disconnecting, setDisconnecting] = useState<Provider>();
  const [pending, setPending] = useState(false);
  const failed = useFailure(onUnexpectedError);

  const connect = useSensitive(
    (provider: Provider) =>
      user
        ? startConnecting(
            user,
            provider,
            `${window.location.origin}${returnPath}?${connectedParameter}=${provider}`,
          )
        : Promise.resolve(),
    onUnexpectedError,
  );
  const disconnect = useSensitive(async (provider: Provider) => {
    await connectedAccount(user, provider)?.destroy();
    await user?.reload();
  }, onUnexpectedError);

  useBackFromProvider();

  return (
    <Card
      title={t("methodsTitle")}
      lead={t("methodsLead")}
      text={t("methodsText")}
    >
      <Row>
        <p className="flex items-center gap-2 text-body">
          {t("emailCode")}
          <Label>{t("alwaysOn")}</Label>
        </p>
        <p className="text-small text-muted-foreground">{t("emailCodeText")}</p>
      </Row>
      {providers.map((provider) => {
        const account = connectedAccount(user, provider);
        return (
          <Row
            key={provider}
            action={
              <Button
                onClick={() =>
                  account
                    ? setDisconnecting(provider)
                    : void connect.run(provider).catch(failed)
                }
              >
                {t(account ? "disconnect" : "connect")}
              </Button>
            }
          >
            <p className="text-body">{t(provider)}</p>
            <p className="text-small text-muted-foreground">
              {account ? (
                <bdi dir="ltr">{account.emailAddress}</bdi>
              ) : (
                t("notConnected")
              )}
            </p>
          </Row>
        );
      })}
      {disconnecting && (
        <Dialog
          open
          onOpenChange={(open) => !open && setDisconnecting(undefined)}
          title={t("disconnectTitle", { provider: t(disconnecting) })}
          description={t("disconnectText", { provider: t(disconnecting) })}
          actionLabel={t("disconnect")}
          pending={pending}
          onConfirm={async () => {
            setPending(true);
            try {
              await disconnect.run(disconnecting);
              showToast({
                title: t("disconnected", { provider: t(disconnecting) }),
                description: t("disconnectedText"),
              });
              setDisconnecting(undefined);
            } catch (reason) {
              failed(reason);
            }
            setPending(false);
          }}
        />
      )}
      {connect.confirm}
      {disconnect.confirm}
    </Card>
  );
}

/**
 * Back from the provider after Connect (`?connected=google`): a verified account says it is connected; an attempt the person
 * refused says so and is removed, so a later Connect starts clean (6.14, 6.15). The parameter then leaves the address.
 */
function useBackFromProvider() {
  const t = useTranslations();
  const { user } = useIdentity();
  const done = useRef(false);
  useEffect(() => {
    if (!user || done.current) return;
    const url = new URL(window.location.href);
    const provider = url.searchParams.get(connectedParameter) as Provider;
    if (!providers.includes(provider)) return;
    done.current = true;
    url.searchParams.delete(connectedParameter);
    window.history.replaceState(null, "", url.pathname + url.search);
    const name = t(`accountSecurity.${provider}`);
    if (connectedAccount(user, provider)) {
      showToast({
        title: t("accountSecurity.connected", { provider: name }),
        description: t("accountSecurity.connectedText", { provider: name }),
      });
      return;
    }
    showToast({
      title: t("accountSecurity.refused", { provider: name }),
      description: t("signIn.errors.oauthDenied"),
      tone: "destructive",
    });
    void Promise.all(
      unfinishedAccounts(user, provider).map((account) => account.destroy()),
    ).then(() => user.reload());
  }, [user, t]);
}

function TwoStep({ text, required }: { text: string; required: boolean }) {
  const t = useTranslations("accountSecurity");
  const { user } = useIdentity();
  const on = required || Boolean(user?.totpEnabled);
  return (
    <Card title={t("twoStepTitle")} text={text}>
      <Row>
        <p className="text-body">{t("authenticatorApp")}</p>
        <p className="text-small text-muted-foreground">
          {t(on ? "on" : "off")}
        </p>
      </Row>
    </Card>
  );
}

function Sessions({
  timeZone,
  now,
  onUnexpectedError,
}: {
  timeZone?: string | null;
  now?: Instant;
  onUnexpectedError?: (error: Error) => void;
}) {
  const t = useTranslations();
  const { session } = useIdentity();
  const { state, load, drop } = useSessions();
  const [askingOthers, setAskingOthers] = useState(false);
  const [pending, setPending] = useState(false);
  const failed = useFailure(onUnexpectedError);
  const name = useDeviceName();

  const revoke = useSensitive(async (...sessions: IdentitySession[]) => {
    await Promise.all(sessions.map((each) => each.revoke()));
  }, onUnexpectedError);

  const sessions = state.status === "success" ? state.sessions : [];
  const others = sessions.filter((each) => each.id !== session?.id);
  const options = { locale: t("locale.format"), timeZone, now };

  const signOut = async (one: IdentitySession) => {
    try {
      await revoke.run(one);
      showToast({
        title: t("accountSecurity.signedOut", { device: name(one) }),
        description: t("accountSecurity.signedOutText"),
      });
      drop([one]);
    } catch (reason) {
      failed(reason);
    }
  };

  return (
    <Card
      title={t("accountSecurity.sessionsTitle")}
      lead={t("accountSecurity.sessionsLead")}
      text={t("accountSecurity.sessionsText")}
    >
      {state.status === "loading" && (
        <Row>
          <InlineSkeleton />
        </Row>
      )}
      {state.status === "error" && (
        <Row
          action={
            <Button onClick={() => void load()}>
              {t("accountSecurity.tryAgain")}
            </Button>
          }
        >
          <p className="flex items-center gap-2 text-small">
            <Icon icon={CircleXIcon} />
            {t("accountSecurity.sessionsFailed")}
          </p>
        </Row>
      )}
      {state.status === "success" && (
        <ul>
          {sessions.map((each) => {
            const current = each.id === session?.id;
            const activity = each.latestActivity;
            const place = [
              activity.city,
              countryName(activity.country, options.locale),
            ]
              .filter(Boolean)
              .join(", ");
            const when = current
              ? t("accountSecurity.activeNow")
              : t("accountSecurity.lastActive", {
                  time: formatRelative(each.lastActiveAt, options),
                });
            return (
              <Row
                key={each.id}
                as="li"
                action={
                  !current && (
                    <Button onClick={() => void signOut(each)}>
                      {t("accountSecurity.signOut")}
                    </Button>
                  )
                }
              >
                <p className="flex items-center gap-2 text-body">
                  {name(each)}
                  {current && <Label>{t("accountSecurity.thisDevice")}</Label>}
                </p>
                <p className="text-small text-muted-foreground">
                  {[place, when].filter(Boolean).join(" · ")}
                </p>
              </Row>
            );
          })}
        </ul>
      )}
      {state.status === "success" && others.length === 0 && (
        <Row>
          <p className="text-small text-muted-foreground">
            {t("accountSecurity.onlyThisDevice")}
          </p>
        </Row>
      )}
      {others.length > 0 && (
        <Row>
          <p className="text-body tablet:hidden">
            {t("accountSecurity.allOthers")}
          </p>
          <Button
            className="mt-2 tablet:mt-0"
            onClick={() => setAskingOthers(true)}
          >
            {t("accountSecurity.signOutOthers")}
          </Button>
        </Row>
      )}
      {askingOthers && (
        <Dialog
          open
          onOpenChange={(open) => !open && setAskingOthers(false)}
          title={t("accountSecurity.signOutOthersTitle", {
            count: others.length,
          })}
          description={t("accountSecurity.signOutOthersText", {
            devices: new Intl.ListFormat(options.locale, {
              type: "conjunction",
            }).format(others.map(name)),
          })}
          actionLabel={t("accountSecurity.signOut")}
          pending={pending}
          onConfirm={async () => {
            setPending(true);
            try {
              await revoke.run(...others);
              showToast({
                title: t("accountSecurity.signedOutOthers", {
                  count: others.length,
                }),
                description: t("accountSecurity.signedOutOthersText"),
              });
              setAskingOthers(false);
              drop(others);
            } catch (reason) {
              failed(reason);
            }
            setPending(false);
          }}
        />
      )}
      {revoke.confirm}
    </Card>
  );
}

/** "Safari on iPhone": the browser and the device that Clerk saw; "A device" when it saw neither. */
function useDeviceName() {
  const t = useTranslations("accountSecurity");
  return (each: IdentitySession) => {
    const { browserName, deviceType } = each.latestActivity;
    if (browserName && deviceType)
      return t("device", { browser: browserName, device: deviceType });
    return browserName ?? deviceType ?? t("someDevice");
  };
}

/** A country in the person's language when Clerk gives its two-letter code; any other value as Clerk gives it. */
function countryName(country: string | undefined, locale: string) {
  if (!country || !/^[A-Z]{2}$/.test(country)) return country;
  return new Intl.DisplayNames([locale], { type: "region" }).of(country);
}

/** A change the identity provider refused: a cancelled check says nothing; any other failure says it did not work. */
function useFailure(onUnexpectedError?: (error: Error) => void) {
  const t = useTranslations("accountSecurity");
  return (reason: unknown) => {
    if (isCancelled(reason)) return;
    if (reason instanceof Error) onUnexpectedError?.(reason);
    showToast({ title: t("notDone"), tone: "destructive" });
  };
}

/** Clerk's reverification rejects with this when the person cancels "Confirm it's you". */
const isCancelled = (reason: unknown) =>
  typeof reason === "object" &&
  reason !== null &&
  "code" in reason &&
  reason.code === "reverification_cancelled";
