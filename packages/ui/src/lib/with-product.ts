type Tree = { [key: string]: unknown };

const isTree = (value: unknown): value is Tree =>
  typeof value === "object" && value !== null && !Array.isArray(value);

/**
 * The product's words over the template's (F14): an object merges key by key, and any other value of the product replaces the
 * template's. The template ships each `messages/product/*.json` empty and never changes it, so a product adds its words there, such
 * as a meter's name under `plans.meters` or a permission's label under `permissions`, and the template's files stay its own.
 */
export function withProduct<T extends Tree>(template: T, product: Tree): T {
  const merged: Tree = { ...template };
  for (const [key, value] of Object.entries(product)) {
    const below = merged[key];
    merged[key] =
      isTree(below) && isTree(value) ? withProduct(below, value) : value;
  }
  return merged as T;
}
