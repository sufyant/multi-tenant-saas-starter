import type { Language } from "@repo/api-client";
import { brand } from "@repo/brand";
import { Button, Fallback, Heading, Layout, Paragraph } from "../layout/layout";
import { format, messages } from "../texts";

/**
 * The values of an invitation email. The render script passes a placeholder for each, such as `{{inviterName}}`, which the API fills.
 * The inviter is a person's name, or the brand's name when the platform invites a tenant's Owner.
 */
export type InvitationProps = {
  language: Language;
  inviterName: string;
  tenantName: string;
  roleName: string;
  /** The moment the link stops working, written in the inviter's time zone with its label. */
  expiresAt: string;
  /** Where the person accepts. */
  link: string;
};

const textsOf = ({ language, ...values }: InvitationProps) => {
  const texts = messages[language].invitation;
  return {
    subject: format(texts.subject, values),
    preheader: format(texts.preheader, values),
    heading: format(texts.heading, values),
    button: texts.button,
    expiry: format(texts.expiry, values),
    fallback: texts.fallback,
    footer: texts.footer,
  };
};

export const invitationSubject = (props: InvitationProps): string =>
  textsOf(props).subject;

/** The plain-text part: the same texts in the same order, the brand first, the button as its label, a colon and the link. */
export function invitationText(props: InvitationProps): string {
  const texts = textsOf(props);
  return [
    brand.name,
    texts.heading,
    `${texts.button}:\n${props.link}`,
    texts.expiry,
    texts.footer,
  ].join("\n\n");
}

/** The invitation to join a tenant with a role: from a member, or from the platform to a tenant's Owner (13.01, 13.08, 13.15). */
export function Invitation(props: InvitationProps) {
  const texts = textsOf(props);
  const { language } = props;
  return (
    <Layout
      language={language}
      preheader={texts.preheader}
      footer={texts.footer}
    >
      <Heading language={language}>{texts.heading}</Heading>
      <Paragraph language={language}>{texts.expiry}</Paragraph>
      <Button language={language} href={props.link}>
        {texts.button}
      </Button>
      <Fallback language={language} href={props.link}>
        {texts.fallback}
      </Fallback>
    </Layout>
  );
}
