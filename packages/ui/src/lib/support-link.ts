/**
 * The address of "Contact support" (the App version pattern): the brand's support address, and for a `mailto:` address the body
 * "Version: a1b2c3d", so support knows which build the person uses. An `https` address stays as it is.
 */
export function supportLink(supportUrl: string, body: string | undefined) {
  if (!body || !supportUrl.startsWith("mailto:")) return supportUrl;
  const separator = supportUrl.includes("?") ? "&" : "?";
  return `${supportUrl}${separator}body=${encodeURIComponent(body)}`;
}

/** The short form of the image's commit, `SOURCE_REVISION`: its first seven characters, as Git shows it. */
export const shortRevision = (revision: string | undefined) =>
  revision ? revision.slice(0, 7) : undefined;
