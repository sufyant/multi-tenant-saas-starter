"use client";

import { useTranslations } from "next-intl";
import { useState } from "react";
import { formatDate, type Instant } from "../../lib/format-date-time";
import { useFailureText } from "../../lib/use-failure-text";
import { useIntentKey } from "../../lib/use-intent-key";
import { Banner } from "../banner";
import { Button } from "../button";
import { Dialog } from "../dialog";
import { Icon } from "../icon";
import { CircleXIcon } from "../icon/icons";
import { Pill } from "../pill";
import { InlineSkeleton } from "../skeleton";
import { showToast } from "../toast";
import { Card, Row } from "./account-security";
import type { ReadFailure } from "./email-change";

/**
 * Where the person's latest export stands, in this card's words; the app reads it from `GetMyDataExport`, whose status values
 * `packages/ui` does not name (F3): none yet, in progress, available until `expiresAt`, unfinished, or its file deleted.
 */
export type ExportState =
  | { kind: "loading" }
  | { kind: "error"; onRetry: () => void }
  | { kind: "none" }
  | { kind: "inProgress" }
  | { kind: "available"; id: string; expiresAt: string }
  | { kind: "unfinished" }
  | { kind: "deleted"; expiresAt: string };

export type YourDataProps = {
  state: ExportState;
  /** The address the link goes to. */
  email: string | null;
  /** `RequestDataExport` with the intent's Idempotency-Key (F48). */
  request: (key: string) => Promise<unknown>;
  /** `DownloadDataExport`: a link that works for 5 minutes, never stored. */
  download: (exportId: string) => Promise<{ url: string }>;
  readFailure: ReadFailure;
  timeZone?: string | null;
  /** Tests fix the clock. */
  now?: Instant;
};

/**
 * Your data (6.19 to 6.21, 6.32, F62): the person asks for a file of their own data, which the worker prepares in the background;
 * an email and a notification say when it is ready, and the person downloads it until its expiry. Both apps show it.
 */
export function YourData({
  state,
  email,
  request,
  download,
  readFailure,
  timeZone,
  now,
}: YourDataProps) {
  const t = useTranslations();
  const failureText = useFailureText();
  const { key, renew } = useIntentKey();
  const [asking, setAsking] = useState(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string>();
  const options = { locale: t("locale.format"), timeZone, now };

  const ask = async () => {
    setPending(true);
    setError(undefined);
    try {
      await request(key);
      renew();
      setAsking(false);
      showToast({
        title: t("accountSecurity.exportStarted"),
        description: t("accountSecurity.exportStartedText"),
      });
    } catch (reason) {
      const failure = readFailure(reason);
      if (failure.saved) renew();
      if (failure.handled) setAsking(false);
      else setError(failureText(failure.problem));
    }
    setPending(false);
  };

  const fetchFile = async (exportId: string) => {
    try {
      window.location.assign((await download(exportId)).url);
    } catch (reason) {
      const failure = readFailure(reason);
      if (!failure.handled)
        showToast({
          title: t("accountSecurity.notDone"),
          description: failureText(failure.problem),
          tone: "destructive",
        });
    }
  };

  const exportButton = (label: "exportTitle" | "exportAgain") => (
    <Button onClick={() => setAsking(true)}>
      {t(`accountSecurity.${label}`)}
    </Button>
  );

  const line = (() => {
    switch (state.kind) {
      case "loading":
        return { text: <InlineSkeleton /> };
      case "error":
        return {
          text: (
            <span className="flex items-center gap-2">
              <Icon icon={CircleXIcon} />
              {t("accountSecurity.exportFailed")}
            </span>
          ),
          action: (
            <Button onClick={state.onRetry}>
              {t("accountSecurity.tryAgain")}
            </Button>
          ),
        };
      case "inProgress":
        return {
          pill: <Pill tone="warning">{t("accountSecurity.preparing")}</Pill>,
          text: t("accountSecurity.exportStartedText"),
        };
      case "available":
        return {
          pill: <Pill tone="success">{t("accountSecurity.ready")}</Pill>,
          text: t("accountSecurity.readyText", {
            date: formatDate(state.expiresAt, options),
          }),
          action: (
            <>
              {exportButton("exportAgain")}
              <Button
                variant="primary"
                onClick={() => void fetchFile(state.id)}
              >
                {t("accountSecurity.download")}
              </Button>
            </>
          ),
        };
      case "unfinished":
        return {
          text: t("accountSecurity.unfinished"),
          action: exportButton("exportTitle"),
        };
      case "deleted":
        return {
          text: t("accountSecurity.deletedFile", {
            date: formatDate(state.expiresAt, options),
          }),
          action: exportButton("exportTitle"),
        };
      default:
        return {
          text: t("accountSecurity.exportHint"),
          action: exportButton("exportTitle"),
        };
    }
  })();

  return (
    <Card
      title={t("accountSecurity.yourDataTitle")}
      text={t("accountSecurity.yourDataText")}
    >
      <Row action={line.action}>
        <p className="flex items-center gap-2 text-body">
          {t("accountSecurity.exportTitle")}
          {line.pill}
        </p>
        <p className="text-small text-muted-foreground">{line.text}</p>
      </Row>
      {asking && (
        <Dialog
          open
          onOpenChange={(isOpen) => {
            if (isOpen) return;
            setAsking(false);
            setError(undefined);
          }}
          title={t("accountSecurity.exportTitle")}
          description={t.rich("accountSecurity.exportDialogText", {
            email: () => <bdi dir="ltr">{email}</bdi>,
          })}
          actionLabel={t("accountSecurity.requestExport")}
          action="primary"
          cancelLabel={t("common.cancel")}
          pending={pending}
          onConfirm={() => void ask()}
        >
          {error && <Banner tone="destructive" title={error} />}
        </Dialog>
      )}
    </Card>
  );
}
