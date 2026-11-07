import type { Language } from "@repo/api-client";
import { brand } from "@repo/brand";
import { Code, Heading, InlineLink, Layout, Paragraph } from "../layout/layout";
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

/** An email about the person's own account, in the layout of every email. */
export function AccountEmail(props: AccountEmailProps) {
  const texts = textsOf(props);
  const { language } = props;
  const [before, link, after] = hasLink(texts.body)
    ? linkParts(texts.body)
    : [texts.body, "", ""];
  return (
    <Layout
      language={language}
      preheader={texts.preheader}
      footer={texts.footer}
    >
      <Heading language={language}>{texts.heading}</Heading>
      <Paragraph language={language}>
        {before}
        {link === "" ? null : (
          <InlineLink href={props.supportUrl}>{link}</InlineLink>
        )}
        {after}
      </Paragraph>
      {props.verificationCode === undefined ? null : (
        <Code language={language}>{props.verificationCode}</Code>
      )}
    </Layout>
  );
}
