"use client";

import { apiDownload, problemOf } from "@repo/api-client";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { browserTimeZone } from "@repo/ui/lib/format-date-time";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { afterRefusal } from "@/shared/refresh";
import { useTenant } from "@/shared/tenant-context";

/** The list's filter as the screen shows it: the file follows it. */
export type ExportFilter = {
  q: string | undefined;
  roleId: string | undefined;
  sort: string | undefined;
  /** The name of the filter's role, or none without a role filter. */
  roleName: string | undefined;
};

/** Saves the file under the server's name (D12): the browser makes no second name. */
function save({ blob, name }: { blob: Blob; name: string }) {
  const link = document.createElement("a");
  link.href = URL.createObjectURL(blob);
  link.download = name;
  link.click();
  // A browser may still read the address after the click starts the download, so it goes on the next task.
  setTimeout(() => URL.revokeObjectURL(link.href));
}

/**
 * Export members (8.08, 8.10, 8.12, 8.16): the CSV file of the list as the screen filters and sorts it, from the web's server,
 * which records the export first (D11). `count` is the list's total for the filter. On success the file is saved and a toast
 * names it; on any failure, a refusal or an answer cut midway, the dialog stays open with the problem's own text (F4).
 */
export function ExportMembersDialog({
  count,
  filter,
  onClose,
}: {
  count: number;
  filter: ExportFilter;
  onClose: () => void;
}) {
  const t = useTranslations();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const { tenant, detail } = useTenant();
  const { q, roleId, sort, roleName } = filter;

  const exportFile = useMutation({
    mutationFn: () => {
      const query = new URLSearchParams(
        Object.entries({ q, roleId, sort, timeZone: browserTimeZone() }).filter(
          (entry): entry is [string, string] => Boolean(entry[1]),
        ),
      );
      return apiDownload(`/exports/tenants/${tenant.id}/members?${query}`, {
        method: "POST",
      });
    },
    onError: (error) => afterRefusal(queryClient, tenant.id, error),
  });

  const filterLine = (() => {
    if (q && roleName)
      return t("members.exportSearchRole", { q, role: roleName });
    if (q) return t("members.exportSearch", { q });
    if (roleName) return t("members.exportRole", { role: roleName });
    return t("members.exportAll");
  })();
  const failed = exportFile.isError;

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t("members.exportTitle")}
      description={
        failed
          ? undefined
          : q || roleName
            ? t("members.exportTextFiltered", { count })
            : t("members.exportText", { count, organisation: detail.name })
      }
      actionLabel={failed ? t("common.tryAgain") : t("members.exportCsv")}
      cancelLabel={t("common.cancel")}
      pending={exportFile.isPending}
      onConfirm={() =>
        exportFile.mutate(undefined, {
          onSuccess: (file) => {
            save(file);
            onClose();
            showToast({
              title: t("members.exportedTitle"),
              description: t("members.exportedText", { name: file.name }),
            });
          },
        })
      }
    >
      {failed ? (
        <p className="text-body text-destructive-text">
          {`${t("members.exportFailed")} ${failureText(problemOf(exportFile.error))}`}
        </p>
      ) : (
        <p className="text-small text-muted-foreground">{filterLine}</p>
      )}
    </Dialog>
  );
}
