import { formatTime, zoneLabel, zoneName } from "../../lib/format-date-time";
import type { ComboboxOption } from "../combobox";

/**
 * The time zones a person can choose, from the browser (`Intl.supportedValuesOf`), each labelled with its city and its offset,
 * as every time on screen names its zone, and a hint with its time now: `Dublin (UTC+0)`, `14:32` (F18). The search also finds
 * the zone's name in the UI language, `Ireland Time`, and its IANA name. The zones of
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
    label: zoneLabel(now, { locale, timeZone }),
    hint: formatTime(now, { locale, timeZone }),
    keywords: [timeZone, zoneName(now, { locale, timeZone })],
  }));
}
