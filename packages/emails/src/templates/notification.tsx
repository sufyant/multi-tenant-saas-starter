import type { Language } from "@repo/api-client";
import { brand } from "@repo/brand";
import { Button, Fallback, Heading, Layout, Paragraph } from "../layout/layout";
import { format, messages } from "../texts";

/**
 * The values of the one generic notification email (apps/api/docs/ARCHITECTURE.md, U2). The API renders a notification's title and body
 * in the recipient's language from the texts of `messages/*.json` (`notifications`), and fills them here with the link of its button and
 * the footer of its type, which says only why the email came (U5). Nobody chooses a type's channels (U4).
 */
export type NotificationProps = {
  language: Language;
  title: string;
  body: string;
  /** Where the button leads: the web app or the platform app. */
  link: string;
  /** Why the email came: the footer of the notification's type. */
  footer: string;
};

const textsOf = ({ language }: NotificationProps) => {
  const texts = messages[language].notificationEmail;
  return {
    button: format(texts.button, { brandName: brand.name }),
    fallback: texts.fallback,
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
    props.footer,
  ].join("\n\n");
}

/** A notification by email: its title, its body and one button, in the brand's layout (13.03 to 13.06, 13.10 to 13.13). */
export function Notification(props: NotificationProps) {
  const texts = textsOf(props);
  const { language } = props;
  return (
    <Layout language={language} preheader={props.body} footer={props.footer}>
      <Heading language={language}>{props.title}</Heading>
      <Paragraph language={language}>{props.body}</Paragraph>
      <Button language={language} href={props.link}>
        {texts.button}
      </Button>
      <Fallback language={language} href={props.link}>
        {texts.fallback}
      </Fallback>
    </Layout>
  );
}
