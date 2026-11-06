"use client";

import { useTranslations } from "next-intl";
import { type ReactNode, useEffect, useRef, useState } from "react";
import { formatRelative, type Instant } from "../../lib/format-date-time";
import { Button } from "../button";
import { CopyButton } from "../copy-button";
import { Dialog } from "../dialog";
import { Icon } from "../icon";
import { AtSignIcon, CircleXIcon, MailIcon } from "../icon/icons";
import { Label } from "../label";
import { Pill } from "../pill";
import { InlineSkeleton } from "../skeleton";
import { showToast } from "../toast";
import {
  authenticatorOn,
  connectedAccount,
  describeSession,
  disconnect,
  type IdentitySession,
  isCancelled,
  type Provider,
  providers,
  removeUnfinished,
  revoke,
  startConnecting,
  useIdentity,
  useSensitive,
  useSessions,
} from "./identity";

/** The most devices a sentence names; the rest are "n more" (F15). */
const namedDevices = 3;

export type AccountSecurityProps = {
  /** The line under the page's title, in the app's words: the person's name, isolated, and where the settings apply. */
  intro: ReactNode;
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

/** Each provider's logo keeps its owner's colours, as its brand rules ask: an image of the app (`public/brands/`), as on sign-in. */
const logos = {
  google: "/brands/google.svg",
  microsoft: "/brands/microsoft.svg",
} as const;

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
            <Tile>
              <Icon icon={MailIcon} />
            </Tile>
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

/** The small square before a row's words: an icon or a logo. A phone leaves it out (6.07). */
function Tile({ children }: { children: ReactNode }) {
  return (
    <span className="hidden size-8 shrink-0 items-center justify-center rounded-md bg-muted tablet:flex">
      {children}
    </span>
  );
}

/** A row's words after its tile. */
function WithTile({
  tile,
  children,
}: {
  tile: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="flex items-center gap-3">
      <Tile>{tile}</Tile>
      <div className="min-w-0">{children}</div>
    </div>
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
  const common = useTranslations("common");
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
  const removal = useSensitive(
    (provider: Provider) => disconnect(user, provider),
    onUnexpectedError,
  );

  useBackFromProvider();

  return (
    <Card
      title={t("methodsTitle")}
      lead={t("methodsLead")}
      text={t("methodsText")}
    >
      <Row>
        <WithTile tile={<Icon icon={AtSignIcon} />}>
          <p className="flex items-center gap-2 text-body">
            {t("emailCode")}
            <Label>{t("alwaysOn")}</Label>
          </p>
          <p className="text-small text-muted-foreground">
            {t("emailCodeText")}
          </p>
        </WithTile>
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
            <WithTile
              tile={
                // biome-ignore lint/performance/noImgElement: a fixed 18px logo needs no optimisation, and packages/ui has no next/image.
                <img src={logos[provider]} alt="" width={18} height={18} />
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
            </WithTile>
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
          cancelLabel={common("cancel")}
          pending={pending}
          onConfirm={async () => {
            setPending(true);
            try {
              await removal.run(disconnecting);
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
      {removal.confirm}
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
    void removeUnfinished(user, provider);
  }, [user, t]);
}

function TwoStep({ text, required }: { text: string; required: boolean }) {
  const t = useTranslations("accountSecurity");
  const { user } = useIdentity();
  const on = required || authenticatorOn(user);
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

  const signing = useSensitive(revoke, onUnexpectedError);

  const sessions = state.status === "success" ? state.sessions : [];
  const others = sessions.filter((each) => each.id !== session?.id);
  const options = { locale: t("locale.format"), timeZone, now };

  const signOut = async (one: IdentitySession) => {
    try {
      await signing.run(one);
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
            const seen = describeSession(each);
            const place = [seen.city, countryName(seen.country, options.locale)]
              .filter(Boolean)
              .join(", ");
            const when = current
              ? t("accountSecurity.activeNow")
              : t("accountSecurity.lastActive", {
                  time: formatRelative(seen.lastActiveAt, options),
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
          description={t.rich("accountSecurity.signOutOthersText", {
            devices: () => (
              <DeviceList
                names={others.map(name)}
                locale={options.locale}
                more={(count) => t("accountSecurity.moreDevices", { count })}
              />
            ),
          })}
          actionLabel={t("accountSecurity.signOut")}
          cancelLabel={t("common.cancel")}
          pending={pending}
          onConfirm={async () => {
            setPending(true);
            try {
              await signing.run(...others);
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
      {signing.confirm}
    </Card>
  );
}

/** "Safari on iPhone": the browser and the device that Clerk saw; "A device" when it saw neither. */
function useDeviceName() {
  const t = useTranslations("accountSecurity");
  return (each: IdentitySession) => {
    const { browser, device } = describeSession(each);
    if (browser && device) return t("device", { browser, device });
    return browser ?? device ?? t("someDevice");
  };
}

/**
 * Devices in a sentence: at most three by name, each isolated, then "n more" (F15). The list's words come from the Intl API in
 * the person's language.
 */
function DeviceList({
  names,
  locale,
  more,
}: {
  names: string[];
  locale: string;
  more: (count: number) => string;
}) {
  const shown = names.slice(0, namedDevices);
  const rest = names.length - shown.length;
  const items = rest > 0 ? [...shown, more(rest)] : shown;
  return new Intl.ListFormat(locale, { type: "conjunction" })
    .formatToParts(items)
    .map((part, index) =>
      part.type === "element" ? (
        // biome-ignore lint/suspicious/noArrayIndexKey: the parts of a formatted list have no id of their own.
        <bdi key={index}>{part.value}</bdi>
      ) : (
        part.value
      ),
    );
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
