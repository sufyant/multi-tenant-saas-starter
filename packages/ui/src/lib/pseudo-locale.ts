/**
 * A test locale (the pseudo-localization of the Long text pattern): every text gets accents, so a text that is not in the messages
 * stands out, and grows by at least 40%, so a box that cannot grow overflows (W3C Internationalization, "Text size in
 * translation"). Brackets mark both ends, so a cut text shows. Only the shell's Playwright tests use it.
 */
const accents: Record<string, string> = {
  a: "å",
  b: "ƀ",
  c: "ç",
  d: "ð",
  e: "é",
  f: "ƒ",
  g: "ĝ",
  h: "ĥ",
  i: "î",
  j: "ĵ",
  k: "ǩ",
  l: "ļ",
  m: "ɱ",
  n: "ñ",
  o: "ø",
  p: "þ",
  q: "ǫ",
  r: "ŕ",
  s: "š",
  t: "ţ",
  u: "û",
  v: "ṽ",
  w: "ŵ",
  x: "ẋ",
  y: "ý",
  z: "ž",
  A: "Å",
  B: "Ɓ",
  C: "Ç",
  D: "Ð",
  E: "É",
  F: "Ƒ",
  G: "Ĝ",
  H: "Ĥ",
  I: "Î",
  J: "Ĵ",
  K: "Ǩ",
  L: "Ļ",
  M: "Ṁ",
  N: "Ñ",
  O: "Ø",
  P: "Þ",
  Q: "Ǫ",
  R: "Ŕ",
  S: "Ŝ",
  T: "Ţ",
  U: "Û",
  V: "Ṽ",
  W: "Ŵ",
  X: "Ẋ",
  Y: "Ý",
  Z: "Ž",
};

/** The growth: 40% of the visible text, in words of at most five wide letters. */
const padding = (length: number) => {
  const count = Math.max(1, Math.ceil(length * 0.4));
  const words: string[] = [];
  for (let left = count; left > 0; left -= 5)
    words.push("ŵ".repeat(Math.min(5, left)));
  return words.join(" ");
};

/**
 * One ICU message in the test locale. A text at an even brace depth is shown (depth 0, or a plural's or select's branch at
 * depth 2); a text at an odd depth is an argument's name or a keyword, and a rich-text tag is code, so both stay.
 */
export function pseudoLocalize(message: string): string {
  let depth = 0;
  let inTag = false;
  let shown = 0;
  let out = "";
  for (const char of message) {
    if (char === "{") depth += 1;
    else if (char === "}") depth -= 1;
    else if (char === "<" && depth % 2 === 0) inTag = true;
    else if (char === ">" && inTag) inTag = false;
    else if (!inTag && depth % 2 === 0 && char !== "#") {
      shown += 1;
      out += accents[char] ?? char;
      continue;
    }
    out += char;
  }
  return `[${out} ${padding(shown)}]`;
}

type Messages = { [key: string]: unknown };

/** Every text of a message tree in the test locale, with the same keys. `locale.format`, the Intl API's locale, stays. */
export function pseudoMessages<T extends Messages>(messages: T): T {
  const walk = (value: unknown, path: string): unknown => {
    if (typeof value === "string")
      return path === "locale.format" ? value : pseudoLocalize(value);
    if (value && typeof value === "object")
      return Object.fromEntries(
        Object.entries(value).map(([key, inner]) => [
          key,
          walk(inner, path ? `${path}.${key}` : key),
        ]),
      );
    return value;
  };
  return walk(messages, "") as T;
}
