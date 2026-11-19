"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  actionFor,
  getGetBillingQueryKey,
  ProblemDetailsError,
  problemOf,
  schemas,
  setBillingEmail,
} from "@repo/api-client";
import { Button } from "@repo/ui/components/button";
import { Input } from "@repo/ui/components/input";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useTenant } from "@/shared/tenant-context";

// The field holds text; an empty one removes the address (the API's B14), so the form sends null for it.
const Form = z.object({
  address: z.union([
    z.literal(""),
    schemas.SetBillingEmailBody.shape.address.unwrap(),
  ]),
});
type Form = z.infer<typeof Form>;

/**
 * Billing email (10.01; B14): an optional address that gets the invoices, and the payment emails besides the people who manage
 * billing. Save stays disabled until the address changes; an empty field removes it.
 */
export function BillingEmailCard({ address }: { address: string | null }) {
  const t = useTranslations();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const { tenant } = useTenant();
  const form = useForm<Form>({
    resolver: zodResolver(Form),
    values: { address: address ?? "" },
  });
  const save = useMutation({
    mutationFn: (value: string | null) =>
      setBillingEmail(tenant.id, { address: value }),
    onSuccess: () =>
      queryClient.invalidateQueries({
        queryKey: getGetBillingQueryKey(tenant.id),
      }),
  });
  const submit = form.handleSubmit(({ address: value }) => {
    const next = value.trim() === "" ? null : value.trim();
    save.mutate(next, {
      onSuccess: () =>
        showToast({
          title: t(next ? "billing.emailSaved" : "billing.emailRemoved"),
        }),
      onError: (error) => {
        if (error instanceof ProblemDetailsError) {
          const action = actionFor(error.status, error.problem);
          if (action.kind === "field-errors") {
            const code = action.fields.address?.[0];
            if (code) {
              form.setError("address", { message: `validation.${code}` });
              return;
            }
          }
        }
        showToast({
          tone: "destructive",
          title: t("billing.emailFailed"),
          description: failureText(problemOf(error)),
        });
      },
    });
  });
  const error = form.formState.errors.address?.message;
  return (
    <section className="overflow-clip rounded-lg border border-border bg-card">
      <div className="flex flex-col gap-0.5 border-b border-border p-5">
        <h2 id="billing-email" className="text-section-title">
          {t("billing.email")}
        </h2>
        <p className="text-small text-muted-foreground">
          {t("billing.emailText")}
        </p>
      </div>
      <form
        noValidate
        onSubmit={(event) => void submit(event)}
        className="flex flex-col gap-2 p-5 tablet:flex-row tablet:items-start"
      >
        <Input
          label={<span className="sr-only">{t("billing.email")}</span>}
          type="email"
          autoComplete="email"
          placeholder={t("billing.emailPlaceholder")}
          maxLength={schemas.setBillingEmailBodyAddressMax}
          className="w-full tablet:w-90"
          error={
            error?.startsWith("validation.")
              ? t(error as "validation.required")
              : error
          }
          {...form.register("address")}
        />
        <Button
          type="submit"
          disabled={!form.formState.isDirty || save.isPending}
        >
          {t("billing.save")}
        </Button>
      </form>
    </section>
  );
}
