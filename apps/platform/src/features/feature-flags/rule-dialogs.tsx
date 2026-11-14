"use client";

import {
  addFeatureFlagTenant,
  type FeatureFlagDetailResponse,
  FeatureFlagResponseType,
  type FeatureFlagTenantResponse,
  type GetFeatureFlagChangePreviewRequest,
  getFeatureFlagChangePreview,
  getListTenantsQueryOptions,
  problemOf,
  removeFeatureFlagTenant,
  schemas,
  setFeatureFlagEveryoneElse,
  setFeatureFlagPercentage,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Combobox } from "@repo/ui/components/combobox";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { type ReactNode, useState } from "react";
import {
  refreshFlag,
  useEnvironmentName,
  usePercentText,
} from "@/shared/feature-flags";
import { isolated } from "@/shared/isolated";

/** One change of a flag's rules, as the preview and the commands take it (12.06, 12.08, 12.09, 12.24 to 12.28). */
export type RuleChange =
  | { kind: "setPercentage" }
  | { kind: "add" }
  | { kind: "remove"; tenant: FeatureFlagTenantResponse }
  | { kind: "everyoneElse"; on: boolean };

const noChange: GetFeatureFlagChangePreviewRequest = {
  tenantToAdd: null,
  tenantToRemove: null,
  percentage: null,
  everyoneElse: null,
};

/** How many organisations one change turns on or off, before it runs (`GetFeatureFlagChangePreview`); none until a value is chosen. */
function usePreview(
  flagKey: string,
  change: GetFeatureFlagChangePreviewRequest | null,
) {
  return useQuery({
    queryKey: ["getFeatureFlagChangePreview", flagKey, change],
    queryFn: () => getFeatureFlagChangePreview(flagKey, change ?? noChange),
    enabled: change !== null,
    staleTime: 0,
  });
}

/** The command of one change, which answers the flag's page; its cache and the lists follow (F47). */
function useRuleCommand(run: () => Promise<FeatureFlagDetailResponse>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: run,
    onSuccess: (next) => refreshFlag(queryClient, next),
  });
}

/** A refused or failed change, in the dialog (F47): the API's message, with what to do next. */
function Failure({ error, flagKey }: { error: Error | null; flagKey: string }) {
  const t = useTranslations("featureFlags");
  const failureText = useFailureText();
  if (!error) return null;
  return (
    <Banner
      tone="destructive"
      title={t.rich("notDone", { flag: flagKey, ...isolated })}
    >
      {failureText(problemOf(error))}
    </Banner>
  );
}

/**
 * The confirmations of one rule's change (12.06, 12.08, 12.09, 12.21, 12.22, 12.24 to 12.28): each names the environment of the
 * environment strip and how many organisations the change affects, from the API's preview, before it runs. The text is the same
 * for every flag (39-design). A kill switch turned off for everyone is red.
 */
export function RuleDialog({
  detail,
  change,
  onClose,
}: {
  detail: FeatureFlagDetailResponse;
  change: RuleChange;
  onClose: () => void;
}) {
  switch (change.kind) {
    case "setPercentage":
      return <PercentageDialog detail={detail} onClose={onClose} />;
    case "add":
      return <AddDialog detail={detail} onClose={onClose} />;
    case "remove":
      return (
        <RemoveDialog
          detail={detail}
          tenant={change.tenant}
          onClose={onClose}
        />
      );
    case "everyoneElse":
      return (
        <EveryoneElseDialog detail={detail} on={change.on} onClose={onClose} />
      );
  }
}

const percentages = Array.from({ length: 101 }, (_, index) => index);

/** Change the percentage (12.06, 12.07): any whole percentage, the count it affects, and the toast that says the new one. */
function PercentageDialog({
  detail,
  onClose,
}: {
  detail: FeatureFlagDetailResponse;
  onClose: () => void;
}) {
  const t = useTranslations("featureFlags");
  const common = useTranslations("common");
  const environment = useEnvironmentName();
  const percent = usePercentText();
  const { key, percentage } = detail.flag;
  const [chosen, setChosen] = useState<number | null>(null);
  const preview = usePreview(
    key,
    chosen === null || chosen === percentage
      ? null
      : { ...noChange, percentage: chosen },
  );
  const command = useRuleCommand(() =>
    setFeatureFlagPercentage(key, { percentage: chosen ?? percentage }),
  );
  const value = chosen ?? percentage;
  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t("percentageTitle", { environment })}
      description={t.rich("percentageText", {
        flag: key,
        percentage: percent(percentage),
        ...isolated,
      })}
      actionLabel={t("percentageAction", { percentage: percent(value) })}
      cancelLabel={common("cancel")}
      pending={command.isPending}
      onConfirm={() =>
        chosen === null || chosen === percentage
          ? onClose()
          : command.mutate(undefined, {
              onSuccess: (next) => {
                onClose();
                showToast({
                  title: t.rich("changedTitle", { flag: key, ...isolated }),
                  description: t("changedPercentage", {
                    percentage: percent(next.flag.percentage),
                  }),
                });
              },
            })
      }
    >
      <Combobox
        label={t("percentageField")}
        value={String(value)}
        options={percentages.map((item) => ({
          id: String(item),
          label: percent(item),
          keywords: [String(item)],
        }))}
        onValueChange={(id) => setChosen(Number(id))}
        searchPlaceholder={t("percentageField")}
        emptyLabel={t("percentageField")}
        hint={
          preview.data && (
            <>
              {t("percentageAffects", { count: preview.data.affectedCount })}
              {value > percentage &&
                ` ${t("percentageStay", { percentage: percent(percentage) })}`}
            </>
          )
        }
      />
      <Failure error={command.error} flagKey={key} />
    </Dialog>
  );
}

/** Add an organisation to rule 1 (12.08): one of the organisations the search finds, which is not listed yet. */
function AddDialog({
  detail,
  onClose,
}: {
  detail: FeatureFlagDetailResponse;
  onClose: () => void;
}) {
  const t = useTranslations("featureFlags");
  const common = useTranslations("common");
  const environment = useEnvironmentName();
  const { key } = detail.flag;
  const [chosen, setChosen] = useState<string | null>(null);
  // Every organisation that may get a flag, by name, as many as one page holds (F9).
  const organisations = useQuery(
    getListTenantsQueryOptions({
      pageSize: schemas.listTenantsQueryPageSizeMax,
    }),
  );
  const listed = new Set(detail.listed.map((item) => item.tenantId));
  const options = (organisations.data?.items ?? [])
    .filter((item) => !listed.has(item.id))
    .map((item) => ({ id: item.id, label: item.name, keywords: [item.slug] }));
  const preview = usePreview(
    key,
    chosen ? { ...noChange, tenantToAdd: chosen } : null,
  );
  const [missing, setMissing] = useState(false);
  const command = useRuleCommand(() => addFeatureFlagTenant(key, chosen ?? ""));
  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t("addTitle", { environment })}
      description={t.rich("addText", { flag: key, ...isolated })}
      actionLabel={t("addOrganisation")}
      cancelLabel={common("cancel")}
      pending={command.isPending}
      onConfirm={() =>
        chosen === null
          ? setMissing(true)
          : command.mutate(undefined, {
              onSuccess: () => {
                onClose();
                showToast({
                  title: t.rich("changedTitle", { flag: key, ...isolated }),
                });
              },
            })
      }
    >
      <Combobox
        label={t("organisationField")}
        value={chosen ?? undefined}
        options={options}
        onValueChange={(id) => {
          setMissing(false);
          setChosen(options.some((item) => item.id === id) ? id : null);
        }}
        error={missing ? t("chooseOrganisation") : undefined}
        searchPlaceholder={t("searchOrganisations")}
        emptyLabel={t("noOrganisation")}
        hint={
          preview.data && t("affects", { count: preview.data.affectedCount })
        }
      />
      <Failure error={command.error} flagKey={key} />
    </Dialog>
  );
}

/** Remove a listed organisation (12.09): it follows the other rules from then on, and may lose the feature. */
function RemoveDialog({
  detail,
  tenant,
  onClose,
}: {
  detail: FeatureFlagDetailResponse;
  tenant: FeatureFlagTenantResponse;
  onClose: () => void;
}) {
  const t = useTranslations("featureFlags");
  const common = useTranslations("common");
  const environment = useEnvironmentName();
  const percent = usePercentText();
  const { key, percentage } = detail.flag;
  const organisation = tenant.name;
  const command = useRuleCommand(() =>
    removeFeatureFlagTenant(key, tenant.tenantId),
  );
  return (
    <Dialog
      open
      tone="destructive"
      onOpenChange={(open) => !open && onClose()}
      title={t.rich("unlistTitle", { organisation, environment, ...isolated })}
      description={t.rich("unlistText", {
        organisation,
        percentage: percent(percentage),
        ...isolated,
      })}
      actionLabel={t("remove")}
      cancelLabel={common("cancel")}
      pending={command.isPending}
      onConfirm={() =>
        command.mutate(undefined, {
          onSuccess: () => {
            onClose();
            showToast({
              title: t.rich("changedTitle", { flag: key, ...isolated }),
            });
          },
        })
      }
    >
      <Failure error={command.error} flagKey={key} />
    </Dialog>
  );
}

/**
 * Rule 3, everyone else on or off (12.24 to 12.28): a release flag's "Turn on for everyone" names the organisations it reaches
 * beyond the first two rules; a kill switch's "Turn off for everyone" is red and lasts until someone turns it back on.
 */
function EveryoneElseDialog({
  detail,
  on,
  onClose,
}: {
  detail: FeatureFlagDetailResponse;
  on: boolean;
  onClose: () => void;
}) {
  const t = useTranslations("featureFlags");
  const common = useTranslations("common");
  const environment = useEnvironmentName();
  const percent = usePercentText();
  const { key, percentage } = detail.flag;
  const killSwitch = detail.flag.type === FeatureFlagResponseType.KillSwitch;
  const preview = usePreview(key, { ...noChange, everyoneElse: on });
  const command = useRuleCommand(() => setFeatureFlagEveryoneElse(key, { on }));
  const flag = key;
  const count = preview.data?.affectedCount;
  const text = (() => {
    if (count === undefined) return t("counting");
    if (on)
      return killSwitch
        ? t("killOnText", { count })
        : t("onText", { count, percentage: percent(percentage) });
    return killSwitch
      ? t("killOffText", { count })
      : t("offConfirmText", { count });
  })();
  const title: ReactNode = on
    ? killSwitch
      ? t.rich("killOnTitle", { flag, environment, ...isolated })
      : t.rich("onTitle", { flag, environment, ...isolated })
    : t.rich("offConfirmTitle", { flag, environment, ...isolated });
  return (
    <Dialog
      open
      tone={!on && killSwitch ? "destructive" : "default"}
      onOpenChange={(open) => !open && onClose()}
      title={title}
      description={text}
      actionLabel={on ? t("turnOn") : t("turnOff")}
      cancelLabel={common("cancel")}
      pending={command.isPending}
      onConfirm={() =>
        command.mutate(undefined, {
          onSuccess: () => {
            onClose();
            showToast({ title: t.rich("changedTitle", { flag, ...isolated }) });
          },
        })
      }
    >
      <Failure error={command.error} flagKey={key} />
    </Dialog>
  );
}
