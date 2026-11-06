"use client";

import { QRCodeSVG } from "qrcode.react";
import { useState } from "react";
import { Button } from "../button";
import { CodeInput } from "../code-input";
import { Input } from "../input";
import { groupsOfFour, type Totp } from "./use-totp-enrolment";

export type TotpFieldsLabels = {
  scan: string;
  qr: string;
  cannotScan: string;
  copyKey: string;
  copied: string;
  enterCode: string;
};

/**
 * The two steps of an enrolment (the TotpEnrol board): the QR code, which the browser draws, so the secret goes to no other
 * server and to no QR service; the typed key in groups of four, left to right, with a copy button; and the six digits. Each
 * place gives its words.
 */
export function TotpFields({
  totp,
  code,
  onCodeChange,
  error,
  disabled,
  labels,
}: {
  totp?: Totp;
  code: string;
  onCodeChange: (code: string) => void;
  error?: string;
  disabled?: boolean;
  labels: TotpFieldsLabels;
}) {
  const [copied, setCopied] = useState(false);
  const copy = async () => {
    await navigator.clipboard.writeText(totp?.secret ?? "");
    setCopied(true);
  };
  return (
    <ol className="flex list-outside list-decimal flex-col gap-4 ps-5 text-body">
      <li>
        {labels.scan}
        <div className="mt-3 flex flex-col gap-3">
          {/* Dark modules on a light ground in both themes, so every camera reads the code. */}
          <div className="self-start rounded-md bg-background p-3 text-foreground dark:bg-foreground dark:text-background">
            {totp?.uri ? (
              <QRCodeSVG
                value={totp.uri}
                size={160}
                fgColor="currentColor"
                bgColor="transparent"
                title={labels.qr}
              />
            ) : (
              <div className="size-40" />
            )}
          </div>
          <div className="flex flex-wrap items-end gap-2">
            <Input
              label={labels.cannotScan}
              readOnly
              value={groupsOfFour(totp?.secret ?? "")}
              dir="ltr"
              className="min-w-0 flex-1 basis-64 [&_input]:font-mono"
            />
            <Button
              type="button"
              onClick={() => void copy()}
              disabled={!totp?.secret}
            >
              {copied ? labels.copied : labels.copyKey}
            </Button>
          </div>
        </div>
      </li>
      <li>
        <CodeInput
          label={labels.enterCode}
          value={code}
          onChange={onCodeChange}
          error={error}
          disabled={disabled || !totp}
        />
      </li>
    </ol>
  );
}

/** The ten backup codes, left to right in two columns, with Copy and Download, which builds the file in the browser. */
export function BackupCodeList({
  codes,
  labels,
}: {
  codes: string[];
  labels: { list: string; copy: string; download: string; fileName: string };
}) {
  const text = `${codes.join("\n")}\n`;
  const download = () => {
    const url = URL.createObjectURL(new Blob([text], { type: "text/plain" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = labels.fileName;
    link.click();
    URL.revokeObjectURL(url);
  };
  return (
    <>
      <ol
        aria-label={labels.list}
        dir="ltr"
        className="grid grid-cols-2 gap-x-6 gap-y-2 rounded-md bg-muted p-4 font-mono text-body"
      >
        {codes.map((code) => (
          <li key={code}>{code}</li>
        ))}
      </ol>
      <div className="flex flex-wrap gap-2">
        <Button type="button" onClick={download}>
          {labels.download}
        </Button>
        <Button
          type="button"
          onClick={() => void navigator.clipboard.writeText(text)}
        >
          {labels.copy}
        </Button>
      </div>
    </>
  );
}
