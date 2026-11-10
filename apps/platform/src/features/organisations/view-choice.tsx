"use client";

import { Combobox } from "@repo/ui/components/combobox";
import { Tabs, type TabsItem } from "@repo/ui/components/tabs";
import { useTranslations } from "next-intl";

/**
 * The tabs of a page from bp-tablet, and one choice of them on a phone, where the tabs do not fit on one line (4.43, 4.44): "All ·
 * 10". Both read and write the same value, which the page keeps in the URL (F9).
 */
export function ViewChoice({
  label,
  items,
  current,
  onValueChange,
}: {
  label: string;
  items: TabsItem[];
  current: string;
  onValueChange: (id: string) => void;
}) {
  const t = useTranslations("organisations");
  return (
    <>
      <div className="max-tablet:hidden">
        <Tabs
          label={label}
          items={items}
          current={current}
          onValueChange={onValueChange}
        />
      </div>
      <Combobox
        className="tablet:hidden"
        label={<span className="sr-only">{label}</span>}
        value={current}
        options={items.map((item) => ({
          id: item.id,
          label:
            item.count === undefined
              ? item.label
              : t("viewWithCount", { view: item.label, count: item.count }),
        }))}
        onValueChange={onValueChange}
        searchPlaceholder={t("viewSearch")}
        emptyLabel={t("viewEmpty")}
      />
    </>
  );
}
