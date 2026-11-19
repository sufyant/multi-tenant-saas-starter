"use client";

import {
  getGetPriceChangeQueryKey,
  getGetPriceChangeQueryOptions,
  moveToNewPrice,
  type PlanResponse,
  problemOf,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { daysAfter } from "@repo/ui/lib/format-date-time";
import { formatMoney } from "@repo/ui/lib/format-money";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { useState } from "react";
import { useDates } from "@/shared/use-dates";

/** The notice before a payer moves to a new price (the API's B13): 30 days. */
const noticeDays = 30;

/**
 * The customers on a plan's old price (6.15) and "Move to the new price" (6.16; B13): a plan whose last price change moved only new
 * customers says how many still pay the old price, what they pay and since when new customers pay the new one. The move tells them
 * today and moves them after 30 days' notice; a yearly payer keeps its price until it renews. While billing is off, or nobody pays an
 * old price, nothing shows.
 */
export function OldPriceBanner({ plan }: { plan: PlanResponse }) {
  const t = useTranslations();
  const locale = useLocale();
  const dates = useDates();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const change = useQuery({
    ...getGetPriceChangeQueryOptions(plan.id),
    retry: false,
  });
  const [asking, setAsking] = useState(false);
  const move = useMutation({
    mutationFn: () => moveToNewPrice(plan.id),
    onSuccess: (answer) =>
      queryClient.setQueryData(getGetPriceChangeQueryKey(plan.id), answer),
  });
  const old = change.data?.onOldPrice;
  if (!old || change.data?.movesAt) return null;
  const money = (minor: number) => formatMoney(minor, plan.currency, locale);
  const newPrice = money(plan.prices.perPersonMonthly);
  return (
    <>
      <Banner
        tone="info"
        title={t("priceChange.oldTitle", { count: old.count })}
        action={
          <Button onClick={() => setAsking(true)}>
            {t("priceChange.move")}
          </Button>
        }
      >
        {t("priceChange.oldText", {
          old: money(old.oldPrices.perPersonMonthly),
          new: newPrice,
          date: dates.date(old.since),
        })}
      </Banner>
      {asking && (
        <Dialog
          open
          onOpenChange={(open) => !open && setAsking(false)}
          title={t("priceChange.moveTitle", {
            count: old.count,
            new: newPrice,
          })}
          description={t("priceChange.moveText", {
            new: newPrice,
            date: dates.date(daysAfter(new Date(), noticeDays)),
            yearly: change.data?.yearlyPayers ?? 0,
            yearlyPrice: money(old.oldPrices.perPersonYearly),
          })}
          cancelLabel={t("common.cancel")}
          actionLabel={t("priceChange.moveButton", { count: old.count })}
          pending={move.isPending}
          onConfirm={() =>
            move.mutate(undefined, {
              onSuccess: (answer) => {
                setAsking(false);
                showToast({
                  title: t("priceChange.moved"),
                  description: answer.movesAt
                    ? t("priceChange.scheduled", {
                        date: dates.date(answer.movesAt),
                      })
                    : undefined,
                });
              },
              onError: (error) =>
                showToast({
                  tone: "destructive",
                  title: t("priceChange.moveFailed"),
                  description: failureText(problemOf(error)),
                }),
            })
          }
        />
      )}
    </>
  );
}
