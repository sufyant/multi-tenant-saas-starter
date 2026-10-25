import type { Language } from "@repo/api-client";
import { brand } from "@repo/brand";
import { Heading, Link, Text } from "react-email";
import { colours } from "../layout/colours";
import { Layout, lookOf } from "../layout/layout";
import { format, linkParts, messages, withoutLinkTags } from "../texts";

/** The values of the warning before a closed tenant's purge. The render script passes a placeholder for each, which the API fills. */
export type DeletionWarningProps = {
  language: Language;
  tenantName: string;
  /** The day of the purge, in the Owner's time zone. */
  purgesOn: string;
  /** The moment of the purge, in the Owner's time zone with its label. */
  purgesAt: string;
  /** Where the Owner asks to reopen the tenant: the brand's support contact. */
  supportUrl: string;
};

const textsOf = ({
  language,
  supportUrl: _,
  ...values
}: DeletionWarningProps) => {
  const texts = messages[language].deletionWarning;
  return {
    subject: format(texts.subject, values),
    preheader: texts.preheader,
    heading: format(texts.heading, values),
    body: format(texts.body, values),
    contact: texts.contact,
    footer: format(texts.footer, values),
  };
};

export const deletionWarningSubject = (props: DeletionWarningProps): string =>
  textsOf(props).subject;

/** The plain-text part: the same texts in the same order, the brand first, the contact sentence and the support address under it. */
export function deletionWarningText(props: DeletionWarningProps): string {
  const texts = textsOf(props);
  return [
    brand.name,
    texts.heading,
    texts.body,
    `${withoutLinkTags(texts.contact)}\n${props.supportUrl}`,
    texts.footer,
  ].join("\n\n");
}

/** The warning to each Owner before a closed tenant's purge (the email boards). */
export function DeletionWarning(props: DeletionWarningProps) {
  const texts = textsOf(props);
  const look = lookOf(props.language);
  const [before, contact, after] = linkParts(texts.contact);
  const paragraph = {
    fontFamily: look.font,
    fontSize: "16px",
    lineHeight: look.line.body,
    color: colours.text.light,
  };
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
        {texts.body}
      </Text>
      <Text className="e-text" style={{ ...paragraph, margin: "16px 0 0" }}>
        {before}
        <Link
          href={props.supportUrl}
          className="e-link"
          style={{ color: colours.primary.light, textDecoration: "underline" }}
        >
          {contact}
        </Link>
        {after}
      </Text>
    </Layout>
  );
}
