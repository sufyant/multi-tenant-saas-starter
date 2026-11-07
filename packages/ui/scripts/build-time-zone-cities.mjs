// Writes src/lib/time-zone-cities.gen.json: each time zone's city in English, Turkish and Arabic, from the exemplarCity of Unicode
// CLDR (cldr-dates-full), under every IANA name of the zone (cldr-bcp47). Both apps read it through zoneLabel and zoneName, and
// packages/emails copies it into the API's emails (F18 in docs/frontend/ARCHITECTURE.md). The CLDR packages are build-time only.
// `pnpm generate` at the repository root runs it; CI fails when the committed file differs (D1).
import { readFileSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const languages = ["en", "tr", "ar"];
const outputPath = "packages/ui/src/lib/time-zone-cities.gen.json";

/**
 * The cities as JSON, by IANA name in order, each `{ en, tr, ar }`. CLDR keeps a zone under its first alias, which may be an older
 * IANA name (Asia/Calcutta), so every alias of the zone gets its city. English CLDR leaves out a city that the zone's name already
 * spells, so the zone's name gives it then; another language without a city of its own takes the English one. The Etc zones, a
 * deprecated key and a name without a region name no city.
 * @param {Record<string, any>} namesByLanguage The zone tree of `timeZoneNames.json` of each language.
 * @param {Record<string, any>} aliases The keys of `bcp47/timezone.json`.
 * @returns {string}
 */
export function buildTimeZoneCities(namesByLanguage, aliases) {
  /** @type {Record<string, Record<string, string>>} */
  const cities = {};
  for (const key of Object.values(aliases)) {
    if (typeof key !== "object" || key._deprecated || !key._alias) continue;
    const names = key._alias
      .split(" ")
      .filter(
        (/** @type {string} */ name) =>
          name.includes("/") && !name.startsWith("Etc/"),
      );
    if (names.length === 0) continue;
    const [cldrName] = names;
    const latest = key._iana ?? cldrName;
    const english =
      exemplarCity(namesByLanguage.en, cldrName) ??
      latest.split("/").pop().replaceAll("_", " ");
    const city = Object.fromEntries(
      languages.map((language) => [
        language,
        exemplarCity(namesByLanguage[language], cldrName) ?? english,
      ]),
    );
    for (const name of new Set([...names, latest])) cities[name] = city;
  }
  const sorted = Object.fromEntries(
    Object.keys(cities)
      .sort()
      .map((name) => [name, cities[name]]),
  );
  return `${JSON.stringify(sorted, null, 2)}\n`;
}

/**
 * The exemplar city of a zone in a language's zone tree, or undefined.
 * @param {Record<string, any>} tree
 * @param {string} name
 * @returns {string | undefined}
 */
function exemplarCity(tree, name) {
  /** @type {any} */
  let node = tree;
  for (const part of name.split("/")) node = node?.[part];
  return node?.exemplarCity;
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const require = createRequire(import.meta.url);
  const read = (/** @type {string} */ path) =>
    JSON.parse(readFileSync(require.resolve(path), "utf8"));
  const namesByLanguage = Object.fromEntries(
    languages.map((language) => [
      language,
      read(`cldr-dates-full/main/${language}/timeZoneNames.json`).main[language]
        .dates.timeZoneNames.zone,
    ]),
  );
  const aliases = read("cldr-bcp47/bcp47/timezone.json").keyword.u.tz;
  const root = new URL("../../../", import.meta.url);
  writeFileSync(
    new URL(outputPath, root),
    buildTimeZoneCities(namesByLanguage, aliases),
  );
}
