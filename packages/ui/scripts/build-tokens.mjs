// Writes src/styles/tokens.gen.css from tokens/tokens.json, the design tokens of the Starter design system (F20 in
// docs/frontend/ARCHITECTURE.md). Node only, no dependency. `pnpm generate` at the repository root runs it; CI fails when the
// committed file differs (D1).
import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

const sourcePath = "packages/ui/tokens/tokens.json";
const outputPath = "packages/ui/src/styles/tokens.gen.css";

/**
 * The CSS of the tokens: light colours and shadows on :root, dark ones on .dark, and every other token on :root. A colour value
 * `{name}` names another token and becomes `var(--name)`. The breakpoints also go into a Tailwind `@theme` block, because a media
 * query cannot read a variable. The `@theme inline` block maps the tokens into Tailwind's theme (themeLines).
 * @param {any} tokens The content of tokens.json.
 * @returns {string}
 */
export function buildTokensCss(tokens) {
  const colors = tokens.color.tokens;
  const names = new Set(
    colors.map((/** @type {{ name: string }} */ token) => token.name),
  );
  /** @param {string} name @param {string | undefined} value @param {string} theme */
  const color = (name, value, theme) => {
    if (value === undefined) throw new Error(`${name} has no ${theme} value`);
    const reference = /^\{(.+)\}$/.exec(value);
    if (!reference) return value;
    if (!names.has(reference[1]))
      throw new Error(`${name} names an unknown token ${value}`);
    return `var(--${reference[1]})`;
  };
  /** @param {string} name @param {string | number} value */
  const line = (name, value) => `  --${name}: ${value};`;

  const light = [];
  const dark = [];
  for (const token of colors) {
    light.push(line(token.name, color(token.name, token.value.light, "light")));
    dark.push(line(token.name, color(token.name, token.value.dark, "dark")));
  }
  for (const token of tokens.shadow.tokens) {
    light.push(line(token.name, token.value.light));
    dark.push(line(token.name, token.value.dark));
  }

  const other = [];
  for (const group of ["spacing", "radius", "size", "breakpoint", "zIndex"]) {
    for (const token of tokens[group].tokens)
      other.push(line(token.name, token.value));
  }
  for (const [name, value] of Object.entries(tokens.type.families))
    other.push(line(`font-${name}`, value));
  for (const group of tokens.type.groups) {
    for (const style of group.styles) {
      other.push(line(`font-${style.name}-size`, style.fontSize));
      other.push(line(`font-${style.name}-line-height`, style.lineHeight));
      other.push(line(`font-${style.name}-weight`, style.fontWeight));
      other.push(
        line(`font-${style.name}-tracking`, style.letterSpacing ?? "normal"),
      );
    }
  }
  const breakpoints = tokens.breakpoint.tokens.map(
    (/** @type {{ name: string, value: string }} */ token) =>
      line(`breakpoint-${token.name.replace(/^bp-/, "")}`, token.value),
  );

  return `/* Generated from ${sourcePath} by packages/ui/scripts/build-tokens.mjs. Do not edit.
   Change tokens.json, then run \`pnpm generate\` (or \`pnpm ui:tokens\`) at the repository root. */

:root {
${[...light, ...other].join("\n")}
}

.dark {
${dark.join("\n")}
}

@theme {
${breakpoints.join("\n")}
}

/* shadcn's variable names are the token names, so each Tailwind theme variable reads the token of the same meaning. A token whose
   name Tailwind already uses (radius-md, shadow-md, font-sans) keeps its name: this file is outside every cascade layer, so its
   value wins over Tailwind's default in the theme layer. A shadow is named here too, because Tailwind writes its default shadow into
   the utility. */
@theme inline {
${themeLines(tokens).join("\n")}
}
`;
}

/**
 * The lines of the `@theme inline` block, in groups: the colours, the sizes, the shadows, the families, the text styles and the
 * layers. A size whose name ends with `-max` is a maximum width, so it is a container; every other size is a spacing. A text style
 * whose name extends another's (`body-strong`, `body-ar`) is a variant of it and gets no utility of its own. A text style names its
 * weight when it is not the normal 400, and its tracking when it has one. The spacing scale, the radii and the breakpoints are not
 * here: the first two keep Tailwind's names, and the breakpoints are in the `@theme` block above.
 * @param {any} tokens The content of tokens.json.
 * @returns {string[]}
 */
function themeLines(tokens) {
  /** @param {string} name @param {string} token */
  const line = (name, token) => `  --${name}: var(--${token});`;
  /** @type {{ name: string, fontWeight: number, letterSpacing?: string }[]} */
  const styles = tokens.type.groups.flatMap(
    (/** @type {{ styles: any[] }} */ group) => group.styles,
  );
  const baseStyles = styles.filter(
    (style) => !styles.some((other) => style.name.startsWith(`${other.name}-`)),
  );

  return [
    tokens.color.tokens.map((/** @type {{ name: string }} */ token) =>
      line(`color-${token.name}`, token.name),
    ),
    tokens.size.tokens.map((/** @type {{ name: string }} */ token) =>
      line(
        `${token.name.endsWith("-max") ? "container" : "spacing"}-${token.name}`,
        token.name,
      ),
    ),
    tokens.shadow.tokens.map((/** @type {{ name: string }} */ token) =>
      line(token.name, token.name),
    ),
    Object.keys(tokens.type.families).map((name) =>
      line(`font-${name}`, `font-${name}`),
    ),
    baseStyles.flatMap((style) => [
      line(`text-${style.name}`, `font-${style.name}-size`),
      line(`text-${style.name}--line-height`, `font-${style.name}-line-height`),
      ...(style.fontWeight === 400
        ? []
        : [
            line(
              `text-${style.name}--font-weight`,
              `font-${style.name}-weight`,
            ),
          ]),
      ...(style.letterSpacing === undefined
        ? []
        : [
            line(
              `text-${style.name}--letter-spacing`,
              `font-${style.name}-tracking`,
            ),
          ]),
    ]),
    tokens.zIndex.tokens.map((/** @type {{ name: string }} */ token) =>
      line(`z-index-${token.name.replace(/^z-/, "")}`, token.name),
    ),
  ]
    .filter((group) => group.length > 0)
    .flatMap((group, index) => (index === 0 ? group : ["", ...group]));
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const root = new URL("../../../", import.meta.url);
  const tokens = JSON.parse(readFileSync(new URL(sourcePath, root), "utf8"));
  writeFileSync(new URL(outputPath, root), buildTokensCss(tokens));
}
