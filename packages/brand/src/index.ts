import identity from "../brand.json" with { type: "json" };

/**
 * The product's identity: its name and the address where a person asks for help. A product built from the template changes
 * `brand.json`, the one place of these values.
 */
export type Brand = {
  /** The name a person sees, such as the text at the top of every email. */
  name: string;
  /** Where a person asks for help: an absolute `https` URL or a `mailto:` address. */
  supportUrl: string;
};

export const brand: Brand = identity;
