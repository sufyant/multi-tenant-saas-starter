"use client";

import {
  DataExportResponseStatus,
  downloadDataExport,
  ErrorCode,
  getGetMyDataExportQueryKey,
  getGetMyDataExportQueryOptions,
  problemOf,
  requestDataExport,
} from "@repo/api-client";
import type {
  ExportState,
  YourDataProps,
} from "@repo/ui/components/account-security";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { useReadFailure } from "./use-read-failure";

/** How often the card reads the export again while the worker prepares it. */
const preparingPoll = 10_000;

/**
 * The calls of Your data (F62): `GetMyDataExport` through `useQuery` and `RequestDataExport` with the intent's Idempotency-Key
 * (F48) and `DownloadDataExport` through `useMutation` (F2). The contract's status becomes the card's state here, so
 * `packages/ui` names no status (F3); no export yet is the 404 `data_export.not_found`. A read that refuses the account reads
 * as a command's failure does: the platform's shell watches no reads (F47).
 */
export function useYourData(): Omit<YourDataProps, "email" | "timeZone"> {
  const queryClient = useQueryClient();
  const readFailure = useReadFailure();
  const latest = useQuery({
    ...getGetMyDataExportQueryOptions(),
    retry: false,
    refetchInterval: (query) =>
      query.state.data?.status === DataExportResponseStatus.Preparing
        ? preparingPoll
        : false,
  });
  const request = useMutation({
    mutationFn: (key: string) => requestDataExport({ "Idempotency-Key": key }),
    onSuccess: (sent) =>
      queryClient.setQueryData(getGetMyDataExportQueryKey(), sent),
  });
  const readError = latest.error;
  useEffect(() => {
    if (
      readError &&
      problemOf(readError)?.code !== ErrorCode.DataExportNotFound
    )
      readFailure(readError);
  }, [readError, readFailure]);
  const download = useMutation({
    mutationFn: (exportId: string) => downloadDataExport(exportId),
  });

  const state = ((): ExportState => {
    if (latest.error) {
      if (problemOf(latest.error)?.code === ErrorCode.DataExportNotFound)
        return { kind: "none" };
      return { kind: "error", onRetry: () => void latest.refetch() };
    }
    if (!latest.data) return { kind: "loading" };
    const { id, status, expiresAt } = latest.data;
    switch (status) {
      case DataExportResponseStatus.Preparing:
        return { kind: "inProgress" };
      case DataExportResponseStatus.Ready:
        return expiresAt
          ? { kind: "available", id, expiresAt }
          : { kind: "inProgress" };
      case DataExportResponseStatus.Failed:
        return { kind: "unfinished" };
      case DataExportResponseStatus.Expired:
        return expiresAt
          ? { kind: "fileDeleted", expiresAt }
          : { kind: "none" };
    }
  })();

  return {
    state,
    request: (key) => request.mutateAsync(key),
    download: (exportId) => download.mutateAsync(exportId),
    readFailure,
  };
}
