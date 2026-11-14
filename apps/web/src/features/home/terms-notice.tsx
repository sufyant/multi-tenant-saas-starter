"use client";

import {
  dismissTermsNotice,
  getGetMyTermsQueryKey,
  getGetMyTermsQueryOptions,
  type MyTermsResponse,
  MyTermsResponseAction,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useTermsDay } from "@/shared/terms-day";

/**
 * Home's notice of a version that requires no acceptance and sent its email (frame 4.03): the day it applies from, Read the terms,
 * and a close button, which closes it for good (DismissTermsNotice). It closes with the API's answer (F47); a failed close leaves it
 * where it is, for another try. It has no summary: the email has none either, and the link leads to the full terms.
 */
export function TermsNotice() {
  const t = useTranslations();
  const day = useTermsDay();
  const queryClient = useQueryClient();
  const terms = useQuery(getGetMyTermsQueryOptions());
  const dismiss = useMutation({
    mutationFn: (versionId: string) => dismissTermsNotice(versionId),
    onSuccess: (next: MyTermsResponse) =>
      queryClient.setQueryData(getGetMyTermsQueryKey(), next),
  });
  const version = terms.data?.version;
  if (terms.data?.action !== MyTermsResponseAction.Notice || !version)
    return null;
  return (
    <Banner
      title={t("terms.title")}
      action={
        <Button
          render={<a href={version.url} target="_blank" rel="noreferrer" />}
          nativeButton={false}
          className="shrink-0"
        >
          {t("terms.read")}
        </Button>
      }
      onClose={() => dismiss.mutate(version.id)}
      closeLabel={t("common.close")}
    >
      {t("terms.noticeText", { date: day(version.effectiveDate) })}
    </Banner>
  );
}
