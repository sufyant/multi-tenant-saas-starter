"use client";

import { AuthHeading } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import { BackupCodeList } from "@repo/ui/components/two-step";
import { Checkbox } from "@repo/ui/components/ui/checkbox";
import { useTranslations } from "next-intl";
import { useId, useState } from "react";

/**
 * The ten backup codes at the platform's gate (platform frame 1.04), shown once: they live only in this screen's state, so
 * leaving the page loses them. The person confirms they saved them before they go on.
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

  return (
    <>
      <AuthHeading title={t("backupTitle")} text={t("backupText")} />
      <BackupCodeList
        codes={codes}
        labels={{
          list: t("backupCodes"),
          copy: t("copyCodes"),
          download: t("download"),
          fileName: t("fileName"),
        }}
      />
      <div className="flex items-start justify-center gap-2 text-body">
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
        className="w-full"
        disabled={!saved}
        onClick={onContinue}
      >
        {t("continue")}
      </Button>
    </>
  );
}
