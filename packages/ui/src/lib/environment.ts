/**
 * The environment an app runs in, from its run-time variable `APP_ENVIRONMENT` (F7): `production`, `staging`, `local`, or
 * `preview-<pull request>`. Every environment but production shows the environment strip, an amber tab icon and a tab title that
 * starts with its name (the Environment strip).
 */
export type Environment =
  | { kind: "production" }
  | { kind: "staging" }
  | { kind: "local" }
  | { kind: "preview"; number: number };

/** The variable's form, which each app's `env.ts` checks at start. */
export const environmentPattern =
  /^(production|staging|local|preview-[1-9]\d*)$/;

/** The environment of a value of the form above. */
export function environmentOf(value: string): Environment {
  const preview = /^preview-(\d+)$/.exec(value);
  if (preview) return { kind: "preview", number: Number(preview[1]) };
  if (value === "staging" || value === "local") return { kind: value };
  return { kind: "production" };
}

/** The message keys of an environment's strip and tab mark, under `appShell.environment`, with their values; none in production. */
export function environmentTexts(environment: Environment): {
  strip: "staging" | "local" | "preview";
  tab: "tabStaging" | "tabLocal" | "tabPreview";
  values: Record<string, number>;
} | null {
  switch (environment.kind) {
    case "production":
      return null;
    case "staging":
      return { strip: "staging", tab: "tabStaging", values: {} };
    case "local":
      return { strip: "local", tab: "tabLocal", values: {} };
    case "preview":
      return {
        strip: "preview",
        tab: "tabPreview",
        values: { number: environment.number },
      };
  }
}
