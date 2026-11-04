import type { Language } from "@repo/api-client";
import { brand } from "@repo/brand";
import { Heading, Link, Text } from "react-email";
import { colours } from "../layout/colours";
import { Layout, lookOf } from "../layout/layout";
import { format, linkParts, messages, withoutLinkTags } from "../texts";

/**
 * The emails about the person's own account: a code for a new address or for the deletion of the account, the notice to the old address
 * after a change, and the confirmation of a deletion (the API's section 6, "Verification codes"). Each kind has its texts in `account` of
 * the messages.
 */
export type AccountEmailKind =
  | "emailChangeCode"
  | "deletionCode"
  | "emailChanged"
  | "accountDeleted";

/** The values of an account email. The render script passes a placeholder for each value the kind uses, which the API fills. */
export type AccountEmailProps = {
  language: Language;
  kind: AccountEmailKind;
  /** The six digits of a code. */
  verificationCode?: string;
  /** The new address, masked, of the notice to the old address. */
  newEmail?: string;
  /** Where the person asks for help: the brand's support contact. */
  supportUrl?: string;
};

const textsOf = ({ language, kind, newEmail }: AccountEmailProps) => {
  const texts = messages[language].account[kind];
  const values: Record<string, string> =
    newEmail === undefined ? {} : { newEmail };
  return {
    subject: texts.subject,
    preheader: texts.preheader,
    heading: texts.heading,
    body: format(texts.body, values),
    footer: texts.footer,
  };
};

const hasLink = (text: string) => text.includes("<link>");

export const accountEmailSubject = (props: AccountEmailProps): string =>
  textsOf(props).subject;

/** The plain-text part: the same texts in the same order, the brand first, the code on a line of its own, the support address under a link. */
export function accountEmailText(props: AccountEmailProps): string {
  const texts = textsOf(props);
  const body = hasLink(texts.body)
    ? `${withoutLinkTags(texts.body)}\n${props.supportUrl}`
    : texts.body;
  return [
    brand.name,
    texts.heading,
    body,
    ...(props.verificationCode === undefined ? [] : [props.verificationCode]),
    texts.footer,
  ].join("\n\n");
}

/** An email about the person's own account (the email boards). */
export function AccountEmail(props: AccountEmailProps) {
  const texts = textsOf(props);
  const look = lookOf(props.language);
  const paragraph = {
    fontFamily: look.font,
    fontSize: "16px",
    lineHeight: look.line.body,
    color: colours.text.light,
  };
  const [before, link, after] = hasLink(texts.body)
    ? linkParts(texts.body)
    : [texts.body, "", ""];
  return (
    <Layout
      language={props.language}
      preheader={texts.preheader}
      footer={texts.footer}
    >
      <Heading
        as="h1"
        className="e-text"
        style={{
          fontFamily: look.font,
          margin: 0,
          fontSize: "22px",
          lineHeight: look.line.heading,
          fontWeight: 600,
          color: colours.text.light,
        }}
      >
        {texts.heading}
      </Heading>
      <Text className="e-text" style={{ ...paragraph, margin: "12px 0 0" }}>
        {before}
        {link === "" ? null : (
          <Link
            href={props.supportUrl}
            className="e-link"
            style={{
              color: colours.primary.light,
              textDecoration: "underline",
            }}
          >
            {link}
          </Link>
        )}
        {after}
      </Text>
      {props.verificationCode === undefined ? null : (
        <Text
          className="e-text"
          dir="ltr"
          style={{
            ...paragraph,
            margin: "24px 0 0",
            fontFamily:
              "ui-monospace, SFMono-Regular, Menlo, Consolas, monospace",
            fontSize: "32px",
            lineHeight: "40px",
            fontWeight: 600,
            letterSpacing: "8px",
          }}
        >
          {props.verificationCode}
        </Text>
      )}
    </Layout>
  );
}
