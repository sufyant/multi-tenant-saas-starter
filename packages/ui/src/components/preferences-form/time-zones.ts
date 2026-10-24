import { formatTime, utcOffset, zoneName } from "../../lib/format-date-time";
import type { ComboboxOption } from "../combobox";

/**
 * The time zones a person can choose, from the browser (`Intl.supportedValuesOf`), each labelled with its city and its name in
 * the UI language, and a hint with its UTC offset and its time now: `Dublin, Ireland Time`, `UTC+1 · 14:32` (F18). The zones of
 * the device and of the saved value join the list when the browser leaves them out, as it does for `UTC`.
 */
export function timeZoneOptions({
  locale,
  now,
  extra,
}: {
  locale: string;
  now: Date;
  /** Zones that must be in the list: the device's and the saved one. */
  extra: string[];
}): ComboboxOption[] {
  const zones = Intl.supportedValuesOf("timeZone");
  const missing = extra.filter((zone) => !zones.includes(zone));
  return [...missing, ...zones].map((timeZone) => ({
    id: timeZone,
    label: zoneName(now, { locale, timeZone }),
    hint: `${utcOffset(now, timeZone)} · ${formatTime(now, { locale, timeZone })}`,
    keywords: [timeZone],
  }));
}
