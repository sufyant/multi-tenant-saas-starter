"use client";

import {
  getGetUsageQueryKey,
  problemOf,
  setSpendingCap,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { Input } from "@repo/ui/components/input";
import { showToast } from "@repo/ui/components/toast";
import { currencySign, moneyText, parseMoney } from "@repo/ui/lib/format-money";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { afterRefusal } from "@/shared/refresh";
import { useTenant } from "@/shared/tenant-context";

/**
 * Set or change the spending cap (11.03; the API's E9): an amount a month, excl. VAT, in the deployment's currency. While a cap is set,
 * an empty field removes it. The API checks the range itself (`usage.spending_cap_invalid`); a refusal stays in the dialog (F47).
 */
export function SpendingCapDialog({
  amount,
  currency,
  onClose,
}: {
  /** The cap now, in minor units; none when no cap is set. */
  amount: number | null;
  currency: string;
  onClose: () => void;
}) {
  const t = useTranslations();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const { tenant } = useTenant();
  const locale = t("locale.format");
  const [value, setValue] = useState(
    amount === null ? "" : moneyText(amount, currency),
  );
  const [invalid, setInvalid] = useState(false);
  const save = useMutation({
    mutationFn: (next: number | null) =>
      setSpendingCap(tenant.id, { amount: next }),
    onSuccess: () =>
      queryClient.invalidateQueries({
        queryKey: getGetUsageQueryKey(tenant.id),
      }),
  });
  const removing = amount !== null && value.trim() === "";
  const submit = () => {
    const next = removing ? null : parseMoney(value, currency);
    if (!removing && (next === null || next <= 0)) {
      setInvalid(true);
      return;
    }
    save.mutate(next, {
      onSuccess: () => {
        showToast({
          title: t(
            next === null ? "usage.dialog.removed" : "usage.dialog.saved",
          ),
        });
        onClose();
      },
      onError: (error) => afterRefusal(queryClient, tenant.id, error),
    });
  };
  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t(
        amount === null ? "usage.dialog.setTitle" : "usage.dialog.changeTitle",
      )}
      description={t("usage.dialog.text")}
      actionLabel={t(removing ? "usage.dialog.remove" : "usage.dialog.set")}
      cancelLabel={t("common.cancel")}
      onConfirm={submit}
      pending={save.isPending}
    >
      <Input
        label={t("usage.dialog.label")}
        addon={currencySign(currency, locale)}
        inputMode="decimal"
        autoComplete="off"
        value={value}
        onChange={(event) => {
          setValue(event.target.value);
          setInvalid(false);
        }}
        hint={t(
          amount === null ? "usage.dialog.hint" : "usage.dialog.changeHint",
        )}
        error={invalid ? t("usage.dialog.invalid") : undefined}
      />
      {save.error && (
        <Banner tone="destructive" title={t("usage.dialog.failed")}>
          {failureText(problemOf(save.error))}
        </Banner>
      )}
    </Dialog>
  );
}
