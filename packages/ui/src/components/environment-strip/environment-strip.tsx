"use client";

import { useTranslations } from "next-intl";
import { type Environment, environmentTexts } from "../../lib/environment";
import { FlaskConicalIcon, Icon } from "../icon";

/** The strip's text and the tab's mark of each environment but production. */
export function useEnvironmentText() {
  const t = useTranslations("appShell.environment");
  return (environment: Environment) => {
    const texts = environmentTexts(environment);
    return (
      texts && {
        strip: t(texts.strip, texts.values),
        tab: t(texts.tab, texts.values),
      }
    );
  };
}

/**
 * The full-width strip at the very top of every page outside production (the Environment strip): a soft amber tint and a flask,
 * 24 px on a desktop and 28 px under a phone's safe area. It takes its own place, so it pushes the layout down and covers nothing.
 * Production shows nothing.
 */
export function EnvironmentStrip({
  environment,
}: {
  environment: Environment;
}) {
  const text = useEnvironmentText()(environment);
  if (!text) return null;
  return (
    <div
      data-slot="environment-strip"
      className="sticky top-0 z-sticky flex h-[calc(28px+env(safe-area-inset-top))] shrink-0 items-end justify-center gap-1.5 border-b border-warning bg-warning-subtle px-4 pb-1.5 text-body font-medium text-warning-text tablet:h-6 tablet:items-center tablet:pb-0 tablet:text-small"
    >
      <Icon icon={FlaskConicalIcon} size={14} className="tablet:size-3" />
      <span className="truncate">{text.strip}</span>
    </div>
  );
}
