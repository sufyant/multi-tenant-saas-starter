import type { Language } from "@repo/api-client";
import { brand } from "@repo/brand";
import { Button, Heading, Link, Text } from "react-email";
import { colours } from "../layout/colours";
import { Layout, lookOf } from "../layout/layout";
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

/** The invitation to join a tenant with a role: from a member, or from the platform to a tenant's Owner (the email boards). */
export function Invitation(props: InvitationProps) {
  const texts = textsOf(props);
  const look = lookOf(props.language);
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
      <table
        role="presentation"
        cellPadding={0}
        cellSpacing={0}
        border={0}
        style={{ marginTop: "28px", borderCollapse: "separate" }}
      >
        <tbody>
          <tr>
            <td
              className="e-button"
              style={{
                backgroundColor: colours.primary.light,
                borderRadius: "6px",
              }}
            >
              <Button
                href={props.link}
                className="e-button"
                style={{
                  fontFamily: look.font,
                  display: "inline-block",
                  boxSizing: "border-box",
                  padding: "12px 24px",
                  fontSize: "16px",
                  lineHeight: "20px",
                  fontWeight: 600,
                  color: colours.onPrimary.light,
                  backgroundColor: colours.primary.light,
                  textDecoration: "none",
                  borderRadius: "6px",
                }}
              >
                {texts.button}
              </Button>
            </td>
          </tr>
        </tbody>
      </table>
      <Text
        className="e-muted"
        style={{
          fontFamily: look.font,
          margin: "16px 0 0",
          fontSize: "14px",
          lineHeight: look.line.small,
          color: colours.muted.light,
        }}
      >
        {texts.expiry}
      </Text>
      <Text
        className="e-muted"
        style={{
          fontFamily: look.font,
          margin: "28px 0 0",
          fontSize: "13px",
          lineHeight: look.line.small,
          color: colours.muted.light,
        }}
      >
        {texts.fallback}
      </Text>
      <Text
        dir="ltr"
        style={{
          fontFamily: look.font,
          margin: "4px 0 0",
          fontSize: "13px",
          lineHeight: "18px",
          textAlign: look.align,
          wordBreak: "break-all",
        }}
      >
        <Link
          href={props.link}
          className="e-link"
          style={{ color: colours.primary.light, textDecoration: "underline" }}
        >
          {props.link}
        </Link>
      </Text>
    </Layout>
  );
}
