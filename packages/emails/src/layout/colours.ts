import tokens from "@repo/ui/tokens.json" with { type: "json" };

type Theme = "light" | "dark";

// A colour token of the design system in a theme, a reference such as {foreground} resolved (F20: tokens.json is the source of every
// colour).
function colour(name: string, theme: Theme): string {
  const token = tokens.color.tokens.find(
    (candidate) => candidate.name === name,
  );
  if (!token) {
    throw new Error(`tokens.json has no colour ${name}.`);
  }
  const value = (token.value as Record<Theme, string>)[theme];
  const reference = /^\{(.+)\}$/.exec(value)?.[1];
  return reference ? colour(reference, theme) : value;
}

const pair = (name: string) => ({
  light: colour(name, "light"),
  dark: colour(name, "dark"),
});

/** The colours of an email, as the email boards name them, from the design tokens. */
export const colours = {
  canvas: pair("muted"),
  card: pair("card"),
  border: pair("border"),
  text: pair("foreground"),
  muted: pair("muted-foreground"),
  primary: pair("primary"),
  onPrimary: pair("primary-foreground"),
};
