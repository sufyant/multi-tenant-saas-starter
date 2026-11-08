"use client";

import { apiDownload, problemOf } from "@repo/api-client";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { browserTimeZone } from "@repo/ui/lib/format-date-time";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useFormatter, useTranslations } from "next-intl";
import { csvRecords } from "@/shared/csv";
import { afterRefusal } from "@/shared/refresh";
import { saveFile } from "@/shared/save-file";
import { useTenant } from "@/shared/tenant-context";
import { activityFileCap } from "./file-cap";

/**
 * Export the activity log (12.08): the CSV file of the newest entries, up to `activityFileCap`, from the web's server, which
 * records the export first. On success the file is saved and a toast names it, or says that the file holds the cap (the
 * maintainer's decision of 2026-11-07); on any failure, a refusal or an answer cut midway, the dialog stays open with the
 * problem's own text (F4), as the members' export does.
 */
export function ExportActivityDialog({ onClose }: { onClose: () => void }) {
  const t = useTranslations();
  const format = useFormatter();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const { tenant, detail } = useTenant();
  const cap = format.number(activityFileCap);

  const exportFile = useMutation({
    mutationFn: async () => {
      const query = new URLSearchParams({ timeZone: browserTimeZone() });
      const file = await apiDownload(
        `/exports/tenants/${tenant.id}/audit-log?${query}`,
        { method: "POST" },
      );
      return {
        file,
        capped: csvRecords(await file.blob.text()) >= activityFileCap,
      };
    },
    onError: (error) => afterRefusal(queryClient, tenant.id, error),
  });
  const failed = exportFile.isError;

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t("activity.exportTitle")}
      description={
        failed
          ? undefined
          : t("activity.exportText", { organisation: detail.name, cap })
      }
      actionLabel={failed ? t("common.tryAgain") : t("activity.exportCsv")}
      cancelLabel={t("common.cancel")}
      pending={exportFile.isPending}
      onConfirm={() =>
        exportFile.mutate(undefined, {
          onSuccess: ({ file, capped }) => {
            saveFile(file);
            onClose();
            showToast({
              title: t("activity.exportedTitle"),
              description: capped
                ? t("activity.exportedCapped", { cap })
                : t("activity.exportedText", { name: file.name }),
            });
          },
        })
      }
    >
      {failed ? (
        <p className="text-small text-destructive-text">
          {`${t("activity.exportFailed")} ${failureText(problemOf(exportFile.error))}`}
        </p>
      ) : (
        <p className="text-small text-muted-foreground">
          {t("activity.exportFormat")}
        </p>
      )}
    </Dialog>
  );
}
