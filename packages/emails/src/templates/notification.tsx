import type { Language } from "@repo/api-client";
import { brand } from "@repo/brand";
import { Button, Heading, Link, Text } from "react-email";
import { colours } from "../layout/colours";
import { Layout, lookOf } from "../layout/layout";
import { format, messages } from "../texts";

/**
 * The values of the one generic notification email (apps/api/docs/ARCHITECTURE.md, U2). The API renders a notification's title and body
 * in the recipient's language from the texts of `messages/*.json` (`notifications`), and fills them here with the link of its button.
 */
export type NotificationProps = {
  language: Language;
  title: string;
  body: string;
  /** Where the button leads: the web app or the platform app. */
  link: string;
};

const textsOf = ({ language }: NotificationProps) => {
  const texts = messages[language].notificationEmail;
  return {
    button: format(texts.button, { brandName: brand.name }),
    fallback: texts.fallback,
    footer: texts.footer,
  };
};

/** The subject is the notification's title. */
export const notificationSubject = (props: NotificationProps): string =>
  props.title;

/** The plain-text part: the brand, the title, the body, the button as its label, a colon and the link, and the footer. */
export function notificationText(props: NotificationProps): string {
  const texts = textsOf(props);
  return [
    brand.name,
    props.title,
    props.body,
    `${texts.button}:\n${props.link}`,
    texts.footer,
  ].join("\n\n");
}

/** A notification by email: its title, its body and one button, in the brand's layout. */
export function Notification(props: NotificationProps) {
  const texts = textsOf(props);
  const look = lookOf(props.language);
  return (
    <Layout
      language={props.language}
      preheader={props.body}
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
        {props.title}
      </Heading>
      <Text
        className="e-text"
        style={{
          fontFamily: look.font,
          margin: "12px 0 0",
          fontSize: "16px",
          lineHeight: look.line.body,
          color: colours.text.light,
        }}
      >
        {props.body}
      </Text>
      <table
        role="presentation"
        cellPadding={0}
        cellSpacing={0}
        border={0}
        style={{ marginTop: "24px", borderCollapse: "separate" }}
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
