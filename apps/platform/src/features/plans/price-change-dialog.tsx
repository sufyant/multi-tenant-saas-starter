"use client";

import {
  getGetPriceChangeQueryOptions,
  type PlanPriceChangeRequest,
  PlanPriceChangeRequestWho,
  type PlanPricesRequest,
  type PlanResponse,
} from "@repo/api-client";
import { Dialog } from "@repo/ui/components/dialog";
import { Input } from "@repo/ui/components/input";
import { RadioGroup, RadioGroupItem } from "@repo/ui/components/ui/radio-group";
import { daysAfter, formatDate } from "@repo/ui/lib/format-date-time";
import { formatMoney } from "@repo/ui/lib/format-money";
import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { useId, useState } from "react";

/** The notice before a payer moves to a new price (the API's B13): 30 days. */
const noticeDays = 30;

const { EveryoneAfterNotice, EveryoneFrom, NewCustomersOnly } =
  PlanPriceChangeRequestWho;

/**
 * "Who pays the new price, and when?" (6.14; B13): the save of a plan whose prices change while organisations pay for it. Everyone after
 * 30 days' notice, everyone from a set day at least 30 days away, or new customers only, the default (the thread's choice of 40b-backend,
 * part 9); how many organisations it affects and how many pay yearly, from GetPriceChange.
 */
export function PriceChangeDialog({
  plan,
  prices,
  pending,
  onCancel,
  onConfirm,
}: {
  plan: PlanResponse;
  prices: PlanPricesRequest;
  pending: boolean;
  onCancel: () => void;
  onConfirm: (change: PlanPriceChangeRequest) => void;
}) {
  const t = useTranslations();
  const locale = useLocale();
  const fieldId = useId();
  const change = useQuery(getGetPriceChangeQueryOptions(plan.id));
  const [who, setWho] = useState<PlanPriceChangeRequestWho>(NewCustomersOnly);
  const earliest = daysAfter(new Date(), noticeDays).slice(0, 10);
  const [day, setDay] = useState(
    daysAfter(new Date(), 6 * noticeDays + 3).slice(0, 10),
  );
  const money = (minor: number) => formatMoney(minor, plan.currency, locale);
  const perPerson = prices.perPersonMonthly !== plan.prices.perPersonMonthly;
  const wrongDay = who === EveryoneFrom && day < earliest;
  const options = [
    {
      value: EveryoneAfterNotice,
      label: t("priceChange.afterNotice"),
      text: t("priceChange.afterNoticeText", {
        new: money(prices.perPersonMonthly),
        date: formatDate(daysAfter(new Date(), noticeDays), { locale }),
      }),
    },
    {
      value: EveryoneFrom,
      label: t("priceChange.from"),
      text: t("priceChange.fromText"),
    },
    {
      value: NewCustomersOnly,
      label: t("priceChange.newOnly"),
      text: t("priceChange.newOnlyText", {
        old: money(plan.prices.perPersonMonthly),
      }),
    },
  ];
  return (
    <Dialog
      size="wide"
      open
      onOpenChange={(open) => !open && onCancel()}
      title={
        perPerson
          ? t("priceChange.title", {
              plan: plan.name,
              old: money(plan.prices.perPersonMonthly),
              new: money(prices.perPersonMonthly),
              base: money(prices.baseMonthly),
            })
          : t("priceChange.titleBase", {
              plan: plan.name,
              old: money(plan.prices.baseMonthly),
              new: money(prices.baseMonthly),
            })
      }
      description={t("priceChange.question")}
      cancelLabel={t("common.cancel")}
      actionLabel={t("priceChange.save")}
      actionDisabled={wrongDay}
      actionReasonId={`${fieldId}-note`}
      pending={pending}
      onConfirm={() =>
        onConfirm({
          who,
          from: who === EveryoneFrom ? `${day}T00:00:00Z` : null,
        })
      }
    >
      <RadioGroup
        value={who}
        onValueChange={(value) => setWho(value as PlanPriceChangeRequestWho)}
        aria-label={t("priceChange.question")}
      >
        {options.map((option) => (
          // biome-ignore lint/a11y/noLabelWithoutControl: the radio is a button inside the label, which names it.
          <label
            key={option.value}
            className={`flex cursor-pointer items-start gap-3 rounded-md border p-3 ${
              who === option.value ? "border-primary" : "border-border"
            }`}
          >
            <RadioGroupItem value={option.value} className="mt-0.5" />
            <span className="flex flex-col gap-0.5">
              <span className="text-body font-medium">{option.label}</span>
              <span className="text-small text-muted-foreground">
                {option.text}
              </span>
            </span>
          </label>
        ))}
      </RadioGroup>
      {who === EveryoneFrom && (
        <Input
          id={fieldId}
          type="date"
          label={t("priceChange.fromDay")}
          min={earliest}
          value={day}
          onChange={(event) => setDay(event.target.value)}
          error={wrongDay ? t("priceChange.invalidDay") : undefined}
          className="tablet:w-60"
        />
      )}
      {change.data && (
        <div className="flex flex-col gap-0.5 rounded-md bg-muted p-3 text-small">
          <p className="font-medium">
            {t("priceChange.affects", { count: change.data.payers })}
          </p>
          {change.data.yearlyPayers > 0 && (
            <p className="text-muted-foreground">
              {t("priceChange.yearly", { count: change.data.yearlyPayers })}
            </p>
          )}
        </div>
      )}
    </Dialog>
  );
}
