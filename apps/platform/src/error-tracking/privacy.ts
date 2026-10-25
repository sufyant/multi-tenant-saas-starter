import type { Breadcrumb, ErrorEvent } from "@sentry/nextjs";

// The one privacy filter of error tracking (F55). An event and its breadcrumbs leave the app without an email address, the
// invitation's code or a value that Clerk puts in a query: Clerk's ticket, its development browser token and its handshake values.

// An address, also inside a URL, where `@` is `%40` and a `+` in the local part may be `%2B`.
const emailAddress =
  /(?:[a-z0-9._+-]|%(?!40)[0-9a-f]{2})+(?:@|%40)[a-z0-9-]+(?:\.[a-z0-9-]+)*\.[a-z]{2,}/gi;

// The value of every query parameter whose name starts with `__clerk_`.
const clerkParameter = /([?&]__clerk_[a-z0-9_]*=)[^&#\s"'<>]*/gi;

// The `code` of the accept page's query (F44), in a full address or a path.
const invitationCode =
  /(\/invitations\/accept\?(?:[^#\s"'<>]*&)?code=)[^&#\s"'<>]*/gi;

/** A text with each address replaced by `[email]` and each code and Clerk query value by `[token]`. */
export function scrubText(text: string): string {
  return text
    .replace(clerkParameter, "$1[token]")
    .replace(invitationCode, "$1[token]")
    .replace(emailAddress, "[email]");
}

const scrubValue = (value: unknown) =>
  typeof value === "string" ? scrubText(value) : value;

// A request's query string, as the SDK may hold it: a string, a list of pairs or an object. The accept page's `code` is known only
// from the request's path.
function scrubQuery(
  query: unknown,
  onAcceptPage: boolean,
): NonNullable<ErrorEvent["request"]>["query_string"] {
  const secret = (name: string) =>
    name.startsWith("__clerk_") || (onAcceptPage && name === "code");
  if (typeof query === "string") {
    const scrubbed = scrubText(`?${query}`).slice(1);
    return onAcceptPage
      ? scrubbed.replace(/(^|&)(code=)[^&#]*/g, "$1$2[token]")
      : scrubbed;
  }
  if (Array.isArray(query))
    return query.map(([name, value]: [string, string]): [string, string] => [
      name,
      secret(name) ? "[token]" : scrubText(value),
    ]);
  if (query && typeof query === "object")
    return Object.fromEntries(
      Object.entries(query).map(([name, value]) => [
        name,
        secret(name) ? "[token]" : scrubValue(value),
      ]),
    ) as Record<string, string>;
  return undefined;
}

/** A breadcrumb without an address or a token: its message, and the `url`, `from` and `to` of its data. */
export function scrubBreadcrumb(breadcrumb: Breadcrumb): Breadcrumb {
  const data = breadcrumb.data && {
    ...breadcrumb.data,
    ...(breadcrumb.data.url !== undefined && {
      url: scrubValue(breadcrumb.data.url),
    }),
    ...(breadcrumb.data.from !== undefined && {
      from: scrubValue(breadcrumb.data.from),
    }),
    ...(breadcrumb.data.to !== undefined && {
      to: scrubValue(breadcrumb.data.to),
    }),
  };
  return {
    ...breadcrumb,
    ...(breadcrumb.message !== undefined && {
      message: scrubText(breadcrumb.message),
    }),
    ...(data && { data }),
  };
}

/** An event without an address or a token: its message, its exceptions' values, its request and its breadcrumbs. */
export function scrubEvent(event: ErrorEvent): ErrorEvent {
  const request = event.request && {
    ...event.request,
    ...(event.request.url !== undefined && {
      url: scrubText(event.request.url),
    }),
    ...(event.request.query_string !== undefined && {
      query_string: scrubQuery(
        event.request.query_string,
        /\/invitations\/accept(?:[?#]|$)/.test(event.request.url ?? ""),
      ),
    }),
  };
  return {
    ...event,
    ...(event.message !== undefined && { message: scrubText(event.message) }),
    ...(event.logentry && {
      logentry: {
        ...event.logentry,
        ...(event.logentry.message !== undefined && {
          message: scrubText(event.logentry.message),
        }),
        ...(event.logentry.params && {
          params: event.logentry.params.map(scrubValue),
        }),
      },
    }),
    ...(event.exception?.values && {
      exception: {
        ...event.exception,
        values: event.exception.values.map((exception) => ({
          ...exception,
          ...(exception.value !== undefined && {
            value: scrubText(exception.value),
          }),
        })),
      },
    }),
    ...(request && { request }),
    ...(event.breadcrumbs && {
      breadcrumbs: event.breadcrumbs.map(scrubBreadcrumb),
    }),
  };
}
