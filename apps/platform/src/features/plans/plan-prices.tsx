"use client";

import {
  MeterSettingRequestOverage,
  type MeterSettingResponse,
  type PlanPricesResponse,
} from "@repo/api-client";
import { Button } from "@repo/ui/components/button";
import { Combobox } from "@repo/ui/components/combobox";
import { Input as InputBase } from "@repo/ui/components/ui/input";
import {
  InputGroup,
  InputGroupAddon,
  InputGroupInput,
} from "@repo/ui/components/ui/input-group";
import {
  currencySign,
  formatMoney,
  moneyText,
  parseMoney,
} from "@repo/ui/lib/format-money";
import { useLocale, useTranslations } from "next-intl";
import { useId } from "react";

/** A plan's four prices as the form holds them: the text of each field. */
export type PriceTexts = Record<keyof PlanPricesResponse, string>;

/** A meter's setting as the form holds it: the quota and the unit price as text, the rest as the plan has them. */
export type MeterDraft = {
  key: string;
  quota: string;
  overage: MeterSettingRequestOverage;
  unitPrice: string;
  unitSize: number;
};

export const priceKeys = [
  "baseMonthly",
  "baseYearly",
  "perPersonMonthly",
  "perPersonYearly",
] as const satisfies readonly (keyof PlanPricesResponse)[];

/** The size of the team the preview prices (6.13): a sample, as the frame draws it. */
export const previewPeople = 10;

/** A free plan's prices, for a new plan (CreatePlan's default). */
export const freePrices: PlanPricesResponse = {
  baseMonthly: 0,
  baseYearly: 0,
  perPersonMonthly: 0,
  perPersonYearly: 0,
};

export const priceTextsOf = (
  prices: PlanPricesResponse,
  currency: string,
): PriceTexts => ({
  baseMonthly: moneyText(prices.baseMonthly, currency),
  baseYearly: moneyText(prices.baseYearly, currency),
  perPersonMonthly: moneyText(prices.perPersonMonthly, currency),
  perPersonYearly: moneyText(prices.perPersonYearly, currency),
});

/** A meter that a plan adds (6.27): a quota of 0 that stops, so nothing is used or charged until the person sets more. */
export const addedMeter = (key: string, currency: string): MeterDraft => ({
  key,
  quota: "0",
  overage: MeterSettingRequestOverage.Stop,
  unitPrice: moneyText(0, currency),
  unitSize: 1,
});

/** The meters of the editor: every meter of the code, in its order, then a meter the plan still sets that the code no longer has. */
export const meterKeysOf = (
  code: readonly string[],
  drafts: readonly MeterDraft[],
) => [
  ...code,
  ...drafts.map((draft) => draft.key).filter((key) => !code.includes(key)),
];

export const meterDraftsOf = (
  meters: readonly MeterSettingResponse[],
  currency: string,
): MeterDraft[] =>
  meters.map((meter) => ({
    key: meter.key,
    quota: String(meter.quota),
    overage: meter.overage,
    unitPrice: moneyText(meter.unitPrice, currency),
    unitSize: meter.unitSize,
  }));

/** Whole months a year's price saves against twelve monthly ones, or 0 (6.13: "2 months free."). */
const monthsFree = (monthly: number | null, yearly: number | null) =>
  monthly && yearly !== null && yearly < monthly * 12
    ? Math.floor((monthly * 12 - yearly) / monthly)
    : 0;

/** The total of a month for the preview's team. */
const monthOf = (base: number, perPerson: number) =>
  base + perPerson * previewPeople;

/**
 * Prices (6.13): a base fee and a fee for each billed person, monthly and yearly, each a field in the deployment's one currency.
 * Each field says its period, a yearly price the months it saves, and an edited one the saved price ("Today €10. Changed.").
 * The preview prices a sample team for a month.
 */
export function PricesArea({
  texts,
  saved,
  currency,
  errors,
  onChange,
}: {
  texts: PriceTexts;
  /** The saved prices; null for a new plan. */
  saved: PlanPricesResponse | null;
  currency: string;
  errors: Partial<Record<keyof PlanPricesResponse, boolean>>;
  onChange: (key: keyof PlanPricesResponse, text: string) => void;
}) {
  const t = useTranslations("plans");
  const locale = useLocale();
  const id = useId();
  const money = (minor: number) => formatMoney(minor, currency, locale);
  const value = (key: keyof PlanPricesResponse) =>
    parseMoney(texts[key], currency);
  const changed = (key: keyof PlanPricesResponse) =>
    saved !== null && value(key) !== saved[key];
  const unitOf = (key: keyof PlanPricesResponse) => {
    const units = {
      baseMonthly: t("perMonth"),
      baseYearly: t("perYear"),
      perPersonMonthly: t("perPersonMonth"),
      perPersonYearly: t("perPersonYear"),
    }[key];
    const free =
      key === "baseYearly"
        ? monthsFree(value("baseMonthly"), value("baseYearly"))
        : key === "perPersonYearly"
          ? monthsFree(value("perPersonMonthly"), value("perPersonYearly"))
          : 0;
    return [
      units,
      free > 0 && t("monthsFree", { count: free }),
      saved && changed(key) && t("changed", { current: money(saved[key]) }),
    ]
      .filter(Boolean)
      .join(" ");
  };
  const base = value("baseMonthly");
  const perPerson = value("perPersonMonthly");
  const preview =
    base === null || perPerson === null
      ? null
      : [
          t("previewText", {
            people: previewPeople,
            amount: money(monthOf(base, perPerson)),
          }),
          saved &&
            (changed("baseMonthly") || changed("perPersonMonthly")) &&
            t("previewToday", {
              current: money(
                monthOf(saved.baseMonthly, saved.perPersonMonthly),
              ),
            }),
        ]
          .filter(Boolean)
          .join(" ");
  const sign = currencySign(currency, locale);
  return (
    <section
      aria-labelledby={`${id}-title`}
      className="flex flex-col divide-y divide-border rounded-lg border border-border bg-card"
    >
      <div className="flex flex-col gap-0.5 px-5 py-3">
        <h2 id={`${id}-title`} className="text-body font-medium">
          {t("pricesTitle")}
        </h2>
        <p className="text-small text-muted-foreground">
          {t("pricesText", { zero: money(0) })}
        </p>
      </div>
      {priceKeys.map((key) => (
        <div
          key={key}
          className="flex flex-col gap-2 px-5 py-3 tablet:flex-row tablet:items-center tablet:gap-3"
        >
          <label
            htmlFor={`${id}-${key}`}
            className="text-body text-muted-foreground tablet:w-50 tablet:shrink-0"
          >
            {t(`priceLabels.${key}`)}
          </label>
          <InputGroup className="h-control-md w-full pointer-coarse:h-control-lg tablet:w-40">
            <InputGroupAddon align="inline-start">{sign}</InputGroupAddon>
            <InputGroupInput
              id={`${id}-${key}`}
              inputMode="decimal"
              autoComplete="off"
              value={texts[key]}
              aria-invalid={errors[key] ? true : undefined}
              aria-describedby={`${id}-${key}-unit`}
              onChange={(event) => onChange(key, event.target.value)}
            />
          </InputGroup>
          <p
            id={`${id}-${key}-unit`}
            className={
              errors[key]
                ? "text-small text-destructive-text"
                : "text-small text-muted-foreground"
            }
          >
            {errors[key] ? t("priceInvalid") : unitOf(key)}
          </p>
        </div>
      ))}
      {preview && (
        <div className="flex flex-col gap-2 px-5 py-3 tablet:flex-row tablet:items-center tablet:gap-3">
          <span className="text-body text-muted-foreground tablet:w-50 tablet:shrink-0">
            {t("preview")}
          </span>
          <p className="text-small text-muted-foreground">{preview}</p>
        </div>
      )}
    </section>
  );
}

/**
 * Meters (6.13, 6.26, 6.27): a row for each meter of the product's code (`GetMeters`). A meter the plan sets shows its setting:
 * the quota of a month, then Charge the extra at a unit price for each unit size, or Stop. A meter the plan does not set says that
 * its use stops, and Add to plan gives it a quota of 0 that stops. The template defines no meter, so a plan shows none; a meter's
 * name is the product's message `plans.meters.<key>` (`messages/product`), else its key.
 */
export function MetersArea({
  keys,
  drafts,
  currency,
  errors,
  onChange,
  onAdd,
}: {
  /** Every meter of the editor, in its order (`meterKeysOf`). */
  keys: readonly string[];
  drafts: readonly MeterDraft[];
  currency: string;
  errors: Partial<Record<number, { quota?: boolean; unitPrice?: boolean }>>;
  onChange: (index: number, draft: MeterDraft) => void;
  onAdd: (key: string) => void;
}) {
  const t = useTranslations("plans");
  const locale = useLocale();
  const id = useId();
  if (keys.length === 0) return null;
  const nameOf = (key: string) => {
    // The product's own message, which the template's empty catalog never has.
    const message = `meters.${key}` as Parameters<typeof t>[0];
    return t.has(message) ? t(message) : key;
  };
  const sign = currencySign(currency, locale);
  const count = new Intl.NumberFormat(locale, { numberingSystem: "latn" });
  return (
    <section
      aria-labelledby={`${id}-title`}
      className="flex flex-col divide-y divide-border rounded-lg border border-border bg-card"
    >
      <div className="flex flex-col gap-0.5 px-5 py-3">
        <h2 id={`${id}-title`} className="text-body font-medium">
          {t("metersTitle")}
        </h2>
        <p className="text-small text-muted-foreground">{t("metersText")}</p>
      </div>
      {keys.map((key, row) => {
        const name = nameOf(key);
        const index = drafts.findIndex((draft) => draft.key === key);
        const draft = drafts[index];
        if (!draft)
          return (
            // biome-ignore lint/a11y/useSemanticElements: a fieldset's legend would draw a frame the card does not have; the group names the row.
            <div
              key={key}
              role="group"
              aria-labelledby={`${id}-${row}`}
              className="flex flex-col gap-2 px-5 py-3 tablet:flex-row tablet:items-center tablet:gap-3"
            >
              <span
                id={`${id}-${row}`}
                className="text-body text-muted-foreground tablet:w-50 tablet:shrink-0"
              >
                {name}
              </span>
              <p className="text-small text-muted-foreground">
                {t("meterNotOnPlan")}
              </p>
              <Button
                variant="secondary"
                className="self-start tablet:self-auto"
                onClick={() => onAdd(key)}
              >
                {t("meterAdd")}
              </Button>
            </div>
          );
        const error = errors[index];
        return (
          // biome-ignore lint/a11y/useSemanticElements: a fieldset's legend would draw a frame the card does not have; the group names the row.
          <div
            key={key}
            role="group"
            aria-labelledby={`${id}-${row}`}
            className="flex flex-col gap-2 px-5 py-3 tablet:flex-row tablet:flex-wrap tablet:items-center tablet:gap-3"
          >
            <span
              id={`${id}-${row}`}
              className="text-body text-muted-foreground tablet:w-50 tablet:shrink-0"
            >
              {name}
            </span>
            <InputBase
              aria-label={t("meterQuota", { meter: name })}
              inputMode="numeric"
              autoComplete="off"
              value={draft.quota}
              aria-invalid={error?.quota ? true : undefined}
              className="h-control-md w-full pointer-coarse:h-control-lg tablet:w-30"
              onChange={(event) =>
                onChange(index, { ...draft, quota: event.target.value })
              }
            />
            <span className="text-small text-muted-foreground">
              {t("meterThen")}
            </span>
            <Combobox
              variant="compact"
              label={<span className="sr-only">{t("meterOverage")}</span>}
              value={draft.overage}
              options={[
                {
                  id: MeterSettingRequestOverage.Charge,
                  label: t("meterCharge"),
                },
                { id: MeterSettingRequestOverage.Stop, label: t("meterStop") },
              ]}
              onValueChange={(choice) =>
                onChange(index, {
                  ...draft,
                  overage:
                    choice === MeterSettingRequestOverage.Stop
                      ? MeterSettingRequestOverage.Stop
                      : MeterSettingRequestOverage.Charge,
                })
              }
              searchPlaceholder={t("meterOverage")}
              emptyLabel={t("meterOverage")}
            />
            {draft.overage === MeterSettingRequestOverage.Charge && (
              <>
                <InputGroup className="h-control-md w-full pointer-coarse:h-control-lg tablet:w-20">
                  <InputGroupAddon align="inline-start">{sign}</InputGroupAddon>
                  <InputGroupInput
                    aria-label={t("meterUnitPrice", { meter: name })}
                    inputMode="decimal"
                    autoComplete="off"
                    value={draft.unitPrice}
                    aria-invalid={error?.unitPrice ? true : undefined}
                    onChange={(event) =>
                      onChange(index, {
                        ...draft,
                        unitPrice: event.target.value,
                      })
                    }
                  />
                </InputGroup>
                <span className="text-small text-muted-foreground">
                  {t("meterPer", { per: count.format(draft.unitSize) })}
                </span>
              </>
            )}
            {(error?.quota || error?.unitPrice) && (
              <p className="w-full text-small text-destructive-text">
                {error.quota ? t("numberInvalid") : t("priceInvalid")}
              </p>
            )}
          </div>
        );
      })}
    </section>
  );
}
