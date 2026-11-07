import type { Language } from "@repo/api-client";
import { brand } from "@repo/brand";
import { Button, Heading, Layout, Paragraph } from "../layout/layout";
import { format, messages } from "../texts";

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
    button: texts.button,
    footer: format(texts.footer, values),
  };
};

export const deletionWarningSubject = (props: DeletionWarningProps): string =>
  textsOf(props).subject;

/** The plain-text part: the same texts in the same order, the brand first, the button as its label, a colon and the support address. */
export function deletionWarningText(props: DeletionWarningProps): string {
  const texts = textsOf(props);
  return [
    brand.name,
    texts.heading,
    texts.body,
    texts.contact,
    `${texts.button}:\n${props.supportUrl}`,
    texts.footer,
  ].join("\n\n");
}

/** The warning to each Owner before a closed tenant's purge (13.07, 13.14). */
export function DeletionWarning(props: DeletionWarningProps) {
  const texts = textsOf(props);
  const { language } = props;
  return (
    <Layout
      language={language}
      preheader={texts.preheader}
      footer={texts.footer}
    >
      <Heading language={language}>{texts.heading}</Heading>
      <Paragraph language={language}>
        {texts.body} {texts.contact}
      </Paragraph>
      <Button language={language} href={props.supportUrl}>
        {texts.button}
      </Button>
    </Layout>
  );
}
