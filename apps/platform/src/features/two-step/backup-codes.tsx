"use client";

import { AuthCard } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import { Checkbox } from "@repo/ui/components/ui/checkbox";
import { useTranslations } from "next-intl";
import { useId, useState } from "react";

/**
 * The ten backup codes (the BackupCodes board), shown once: they live only in this screen's state, so leaving the page loses
 * them. "Download as text" builds the file in the browser. The person confirms they saved them before they go on.
 */
export function BackupCodes({
  codes,
  onContinue,
}: {
  codes: string[];
  onContinue: () => void;
}) {
  const t = useTranslations("twoStep");
  const savedId = useId();
  const [saved, setSaved] = useState(false);
  const text = `${codes.join("\n")}\n`;

  const download = () => {
    const url = URL.createObjectURL(new Blob([text], { type: "text/plain" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = t("fileName");
    link.click();
    URL.revokeObjectURL(url);
  };

  return (
    <>
      <div className="flex flex-col gap-2">
        <h1 className="text-page-title">{t("backupTitle")}</h1>
        <p className="text-body text-muted-foreground">{t("backupText")}</p>
      </div>
      <AuthCard>
        <ol
          aria-label={t("backupCodes")}
          dir="ltr"
          className="grid grid-cols-2 gap-x-6 gap-y-2 rounded-md bg-muted p-4 font-mono text-body"
        >
          {codes.map((code) => (
            <li key={code}>{code}</li>
          ))}
        </ol>
        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            onClick={() => void navigator.clipboard.writeText(text)}
          >
            {t("copyCodes")}
          </Button>
          <Button type="button" onClick={download}>
            {t("download")}
          </Button>
        </div>
        <div className="flex items-start gap-2 text-body">
          <Checkbox
            id={savedId}
            checked={saved}
            onCheckedChange={setSaved}
            className="mt-0.5"
          />
          <label htmlFor={savedId}>{t("saved")}</label>
        </div>
        <Button
          type="button"
          variant="primary"
          size="lg"
          className="w-full"
          disabled={!saved}
          onClick={onContinue}
        >
          {t("continue")}
        </Button>
      </AuthCard>
    </>
  );
}
