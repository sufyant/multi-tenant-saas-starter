"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  actionFor,
  createTenant,
  ErrorCode,
  getGetMeQueryOptions,
  getListPlansQueryOptions,
  getListTenantsQueryKey,
  Language,
  ProblemDetailsError,
  problemOf,
  schemas,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Combobox } from "@repo/ui/components/combobox";
import { Dialog } from "@repo/ui/components/dialog";
import { Input } from "@repo/ui/components/input";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useIntentKey } from "@repo/ui/lib/use-intent-key";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { isolated } from "@/shared/isolated";
import { plansQuery, refreshPlans } from "@/shared/plans";
import { savedUnderKey } from "@/shared/saved-under-key";
import { emailText, nameText, slugText } from "./field-text";
import { LanguageChoice } from "./language-choice";
import { afterRefusal } from "./organisation";

// The generated schema of CreateTenant (F11). The API trims the name and needs 1 to 100 characters; the generated schema has the
// limit only, so the form adds the trim and the minimum.
const creation = schemas.CreateTenantBody.extend({
  name: z.string().trim().min(1).max(schemas.createTenantBodyNameMax),
});
type Creation = z.infer<typeof creation>;

/** The page of the organisation that a 201's `Location` names: the last segment of GetTenant's address is the tenant's id. */
export const pageOf = (location: string) =>
  `/organisations/${new URL(location, "https://platform.invalid").pathname.split("/").at(-1)}`;

/**
 * Create an organisation and invite its Owner (the CreateOrg board): Name, Slug, Owner's email, Owner's language, the system
 * admin's own by default, and the Plan, the default plan preselected and marked. One Idempotency-Key per intent (F48). After the 201 the page of the new organisation opens, from the
 * answer's `Location` (RFC 9110, section 10.2.2).
 */
export function CreateOrganisationDialog({ onClose }: { onClose: () => void }) {
  const t = useTranslations("organisations");
  const common = useTranslations("common");
  const all = useTranslations();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const router = useRouter();
  const me = useQuery(getGetMeQueryOptions());
  const plans = useQuery(getListPlansQueryOptions(plansQuery));
  const defaultPlan = plans.data?.items.find((plan) => plan.isDefault);
  const { key, renew } = useIntentKey();
  const form = useForm<Creation>({
    resolver: zodResolver(creation),
    defaultValues: {
      name: "",
      slug: "",
      ownerEmail: "",
      language: me.data?.language ?? Language.En,
    },
  });
  // The default plan, once the plans are read; a choice of the system admin stays.
  useEffect(() => {
    if (defaultPlan && !form.getValues("planId"))
      form.setValue("planId", defaultPlan.id);
  }, [defaultPlan, form]);
  // Another value of any field is another intent: it gets a new key.
  useEffect(
    () => form.subscribe({ formState: { values: true }, callback: renew }),
    [form, renew],
  );

  const create = useMutation({
    mutationFn: async ({ body, key }: { body: Creation; key: string }) => {
      let location: string | null = null;
      const created = await createTenant(
        body,
        { "Idempotency-Key": key },
        {
          onResponseHeaders: (headers) => (location = headers.get("Location")),
        },
      );
      return { created, location };
    },
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: getListTenantsQueryKey() }),
    onError: (error) => {
      afterRefusal(queryClient, null, error);
      if (savedUnderKey(error)) renew();
      if (!(error instanceof ProblemDetailsError)) return;
      const code = error.problem?.code;
      if (code === ErrorCode.TenantNameInvalid)
        form.setError("name", { message: code });
      if (code === ErrorCode.TenantSlugTaken)
        form.setError("slug", { message: "slugTaken" });
      // The plan was gone meanwhile: its text under the field, and the plans are read again (F47).
      if (code === ErrorCode.PlanNotFound) {
        form.setError("planId", { message: code });
        void refreshPlans(queryClient);
      }
      const action = actionFor(error.status, error.problem);
      if (action.kind !== "field-errors") return;
      for (const field of ["name", "slug", "ownerEmail"] as const) {
        const [fieldCode] = action.fields[field] ?? [];
        if (fieldCode) form.setError(field, { message: fieldCode });
      }
    },
  });

  const failure = (() => {
    const code = problemOf(create.error)?.code;
    if (
      !create.error ||
      code === ErrorCode.RequestValidationFailed ||
      code === ErrorCode.TenantNameInvalid ||
      code === ErrorCode.TenantSlugTaken ||
      code === ErrorCode.PlanNotFound
    )
      return null;
    return {
      title: t("notCreated"),
      text: failureText(problemOf(create.error)),
    };
  })();

  const submit = form.handleSubmit(
    (body) =>
      create.isPending ||
      create.mutate(
        { body, key },
        {
          onSuccess: ({ created, location }) => {
            renew();
            onClose();
            showToast({
              title: t.rich("created", { name: created.name, ...isolated }),
              description: t("createdText", { email: body.ownerEmail }),
            });
            if (location) router.push(pageOf(location));
          },
        },
      ),
  );
  const errors = form.formState.errors;
  const nameError = nameText(errors.name?.message);
  const slugError = slugText(errors.slug?.message);
  const emailError = emailText(errors.ownerEmail?.message);

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t("create")}
      description={t("createText")}
      actionLabel={t("createAction")}
      cancelLabel={common("cancel")}
      pending={create.isPending}
      onConfirm={() => void submit()}
    >
      <form
        noValidate
        onSubmit={(event) => void submit(event)}
        className="flex flex-col gap-3.5"
      >
        <Input
          label={t("name")}
          autoComplete="off"
          error={
            nameError && t(nameError, { max: schemas.createTenantBodyNameMax })
          }
          {...form.register("name")}
        />
        <Input
          label={t("slug")}
          dir="ltr"
          inputMode="url"
          autoComplete="off"
          maxLength={schemas.createTenantBodySlugMax}
          hint={t("slugHint")}
          error={slugError && t(slugError)}
          {...form.register("slug")}
        />
        <Input
          label={t("ownerEmail")}
          type="email"
          autoComplete="off"
          maxLength={schemas.createTenantBodyOwnerEmailMax}
          error={emailError && t(emailError)}
          {...form.register("ownerEmail")}
        />
        <LanguageChoice
          label={t("ownerLanguage")}
          value={form.watch("language") ?? Language.En}
          onChange={(language) => form.setValue("language", language)}
        />
        <Combobox
          label={t("plan")}
          hint={t("planHint")}
          error={errors.planId ? all("errors.plan.not_found") : undefined}
          value={form.watch("planId") ?? undefined}
          options={(plans.data?.items ?? []).map((plan) => ({
            id: plan.id,
            label: plan.name,
            hint: plan.isDefault ? t("planDefault") : undefined,
          }))}
          onValueChange={(planId) => {
            form.setValue("planId", planId);
            form.clearErrors("planId");
          }}
          searchPlaceholder={t("planSearch")}
          emptyLabel={t("planEmpty")}
        />
        {failure && (
          <Banner tone="destructive" title={failure.title}>
            {failure.text}
          </Banner>
        )}
      </form>
    </Dialog>
  );
}
