import { describe, expect, it } from "vitest";
import { buildTimeZoneCities } from "./build-time-zone-cities.mjs";

// A small CLDR: the zone names of each language (cldr-dates-full) and the zones' aliases (cldr-bcp47). CLDR keeps a zone under its
// first alias, an older IANA name such as Asia/Calcutta; the browser and .NET may give either name (F18).
const zone = (exemplarCity: string) => ({ _type: "zone", exemplarCity });
const names = {
  en: { Asia: { Calcutta: zone("Kolkata") } },
  tr: { Asia: { Calcutta: zone("Kalküta") }, Europe: { Istanbul: zone("İstanbul") } },
  ar: { Asia: { Calcutta: zone("كالكتا") }, Europe: { Istanbul: zone("إسطنبول") } },
};
const aliases = {
  _description: "Time zone key",
  _alias: "timezone",
  inccu: { _description: "Kolkata, India", _alias: "Asia/Calcutta Asia/Kolkata", _iana: "Asia/Kolkata" },
  trist: { _description: "Istanbul, Türkiye", _alias: "Europe/Istanbul Asia/Istanbul Turkey" },
  aqams: { _deprecated: true, _description: "Amundsen-Scott Station, South Pole", _preferred: "aqmcm" },
  utc: { _description: "UTC (Coordinated Universal Time)", _alias: "Etc/UTC Etc/UCT UTC" },
};

describe("buildTimeZoneCities", () => {
  it("BuildTimeZoneCities_AZoneOfTwoNames_GivesBothNamesTheCityInEachLanguage", () => {
    const cities = JSON.parse(buildTimeZoneCities(names, aliases));

    expect(cities["Asia/Calcutta"]).toEqual({ en: "Kolkata", tr: "Kalküta", ar: "كالكتا" });
    expect(cities["Asia/Kolkata"]).toEqual(cities["Asia/Calcutta"]);
  });

  // English CLDR leaves out a city that its IANA name already spells, so the name gives it.
  it("BuildTimeZoneCities_ACityEnglishLeavesOut_TakesItFromTheZonesName", () => {
    const cities = JSON.parse(buildTimeZoneCities(names, aliases));

    expect(cities["Europe/Istanbul"]).toEqual({ en: "Istanbul", tr: "İstanbul", ar: "إسطنبول" });
  });

  // A name without a region, a deprecated key and the Etc zones name no city.
  it("BuildTimeZoneCities_ZonesWithoutACity_AreLeftOut", () => {
    const cities = JSON.parse(buildTimeZoneCities(names, aliases));

    expect(Object.keys(cities)).toEqual(["Asia/Calcutta", "Asia/Istanbul", "Asia/Kolkata", "Europe/Istanbul"]);
  });
});
