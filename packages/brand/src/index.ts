import identity from "../brand.json" with { type: "json" };

/**
 * The product's identity: its name, the address where a person asks for help, its privacy policy and its status page. A product
 * built from the template changes `brand.json`, the one place of these values.
 */
export type Brand = {
  /** The name a person sees, such as the text at the top of every email. */
  name: string;
  /** Where a person asks for help: an absolute `https` URL or a `mailto:` address. */
  supportUrl: string;
  /** The privacy policy: an absolute `https` URL. Every screen before sign-in links to it. */
  privacyUrl: string;
  /** The status page, if the product has one: an absolute `https` URL. The connection bar links to it while the server does not answer. */
  statusUrl?: string;
};

export const brand: Brand = identity;
