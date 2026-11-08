import { cn } from "../../lib/utils";
import { AvatarFallback, Avatar as AvatarPrimitive } from "../ui/avatar";

export type Person = {
  /** The person's id, which chooses their tint. */
  id: string;
  /** The person's one name, whole (the API's A7). */
  name: string | null;
  /** The letter when the person has no name yet, as an invited member. */
  email?: string | null;
};

const arabicArticle = "ال";

/**
 * The avatar's letters: the first letter of the name's first and last word, one letter for a name of one word, else the email's
 * first letter; nothing for a deleted user, who has neither. A word's letter comes after the Arabic article "ال", which is
 * written joined to the word it defines: "عبد الرحمن الهاشمي" is "عه". Only the drawing reads the words: the name itself stays whole.
 */
export function initialsOf(person: Omit<Person, "id">): string {
  const words = (person.name ?? "").split(/\s+/).filter(Boolean);
  const ends = words.length > 1 ? [words[0], words.at(-1)] : words;
  return ends.length > 0
    ? ends
        .map((word = "") =>
          (word.length > 2 && word.startsWith(arabicArticle)
            ? word.slice(arabicArticle.length)
            : word
          ).charAt(0),
        )
        .join("")
    : (person.email ?? "").charAt(0).toLocaleUpperCase();
}

/** The six tints of the Avatar component, in its order. */
const tints = {
  owner: "bg-role-owner-subtle text-role-owner-text",
  info: "bg-info-subtle text-info-text",
  success: "bg-success-subtle text-success-text",
  warning: "bg-warning-subtle text-warning-text",
  destructive: "bg-destructive-subtle text-destructive-text",
  neutral: "bg-neutral text-neutral-text",
};

export type Tint = keyof typeof tints;

/** A person's tint, from their id, so one person keeps one tint everywhere (the Avatar component, 45:334). */
export function tintOf(id: string): Tint {
  const hash = [...id].reduce(
    (sum, letter) => (sum * 31 + (letter.codePointAt(0) ?? 0)) >>> 0,
    0,
  );
  const names = Object.keys(tints) as Tint[];
  return names[hash % names.length] as Tint;
}

// The circle and its letters: status 12/16 when small, small-strong 13/18 when medium or large (45:328, 45:330, 45:332).
const sizes = {
  small: { box: "size-5.5", letters: "text-status" },
  medium: { box: "size-8", letters: "text-small font-medium" },
  large: { box: "size-10", letters: "text-small font-medium" },
};

/**
 * A person's round avatar with their initials on the person's tint (the design's Avatar): small, 22 px, in a table row; medium,
 * 32 px, on a phone card; large, 40 px. A class sets another size; the user menu's button fills it. The name beside it says who
 * it is, so screen readers skip it. An organisation's initial is square and is not this component.
 */
export function Avatar({
  person,
  size = "small",
  className,
}: {
  person: Person;
  size?: keyof typeof sizes;
  className?: string;
}) {
  const { box, letters } = sizes[size];
  return (
    <AvatarPrimitive
      aria-hidden="true"
      className={cn(box, "rounded-full after:hidden", className)}
    >
      <AvatarFallback className={cn(letters, tints[tintOf(person.id)])}>
        {initialsOf(person)}
      </AvatarFallback>
    </AvatarPrimitive>
  );
}
