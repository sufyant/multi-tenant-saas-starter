import { type ClassValue, clsx } from "clsx";
import { extendTailwindMerge } from "tailwind-merge";
import tokens from "../../tokens/tokens.json";

const styles = tokens.type.groups.flatMap((group) => group.styles);

/** The text styles that have a utility of their own (F20): `text-small`, `text-body`. A variant (`body-ar`) has none. */
const textStyles = styles
  .map((style) => style.name)
  .filter((name) => !styles.some((other) => name.startsWith(`${other.name}-`)));

// tailwind-merge does not know our text styles, so it takes `text-small` for a colour and drops it beside `text-muted-foreground`.
const twMerge = extendTailwindMerge({
  extend: { classGroups: { "font-size": [{ text: textStyles }] } },
});

/** Joins class names and lets a later Tailwind class win over an earlier one of the same kind. shadcn/ui's `cn`. */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
