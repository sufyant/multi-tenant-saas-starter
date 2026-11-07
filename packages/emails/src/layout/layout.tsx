import type { Language } from "@repo/api-client";
import { brand } from "@repo/brand";
import type { CSSProperties, ReactNode } from "react";
import {
  Body,
  Container,
  Button as EmailButton,
  Heading as EmailHeading,
  Head,
  Html,
  Link,
  Preview,
  Text,
} from "react-email";
import { directionOf } from "../texts";
import { colours } from "./colours";

// The system font stacks of the email boards. An email loads no web font, so the stack names the fonts each platform has; Arabic
// takes the platforms' Arabic fonts first.
const latinFonts =
  "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif";
const arabicFonts =
  "-apple-system, BlinkMacSystemFont, 'Segoe UI', Tahoma, 'Geeza Pro', Arial, sans-serif";
const codeFonts = "ui-monospace, SFMono-Regular, Menlo, Consolas, monospace";

// A client that follows the reader's dark theme reads these rules; the others keep the light colours, which every element carries
// inline. A class names each colour role, so the rule overrides the inline colour.
const darkTheme = `
:root { color-scheme: light dark; supported-color-schemes: light dark; }
@media (prefers-color-scheme: dark) {
  .e-canvas { background-color: ${colours.canvas.dark} !important; }
  .e-card { background-color: ${colours.card.dark} !important; border-color: ${colours.border.dark} !important; }
  .e-text { color: ${colours.text.dark} !important; }
  .e-muted { color: ${colours.muted.dark} !important; }
  .e-link { color: ${colours.primary.dark} !important; }
  .e-button { background-color: ${colours.primary.dark} !important; color: ${colours.onPrimary.dark} !important; }
}
`;

// The space between two parts of the card, as the email boards draw it.
const gap = "16px";

/** What a part of an email needs to look as the boards draw it in its language. */
type Look = {
  font: string;
  align: "left" | "right";
  /** Arabic needs a larger heading and taller lines. */
  heading: { size: string; line: string };
  bodyLine: string;
  smallLine: string;
  button: { size: string; line: string; padding: string };
};

function lookOf(language: Language): Look {
  return directionOf(language) === "rtl"
    ? {
        font: arabicFonts,
        align: "right",
        heading: { size: "22px", line: "34px" },
        bodyLine: "24px",
        smallLine: "22px",
        button: { size: "14px", line: "24px", padding: "4px 12px" },
      }
    : {
        font: latinFonts,
        align: "left",
        heading: { size: "20px", line: "28px" },
        bodyLine: "20px",
        smallLine: "18px",
        button: { size: "13px", line: "18px", padding: "7px 12px" },
      };
}

type LayoutProps = {
  language: Language;
  /** The line an inbox shows after the subject. */
  preheader: string;
  /** Why the email came. */
  footer: string;
  children: ReactNode;
};

/**
 * Every email, as the email boards draw it: one card of at most 562px on the muted canvas, the brand as text on its first line, the
 * email's own parts, and the footer. The parts below this component give an email its heading, paragraphs, button and links, so an
 * email gives only its words and values.
 */
export function Layout({ language, preheader, footer, children }: LayoutProps) {
  const look = lookOf(language);
  return (
    <Html lang={language} dir={directionOf(language)}>
      <Head>
        <meta name="color-scheme" content="light dark" />
        <meta name="supported-color-schemes" content="light dark" />
        <style>{darkTheme}</style>
      </Head>
      {/* Body writes its own dir and lang, English left to right unless it is told. */}
      <Body
        lang={language}
        dir={directionOf(language)}
        className="e-canvas"
        style={{ margin: 0, padding: 0 }}
      >
        <Preview>{preheader}</Preview>
        {/* The canvas is a cell of its own: Body copies its inline style to a cell without the class, which the dark rules miss. */}
        <table
          role="presentation"
          width="100%"
          cellPadding={0}
          cellSpacing={0}
          border={0}
          style={{ borderCollapse: "collapse" }}
        >
          <tbody>
            <tr>
              <td
                className="e-canvas"
                style={{
                  padding: "40px 16px",
                  backgroundColor: colours.canvas.light,
                }}
              >
                <Container style={{ width: "100%", maxWidth: "562px" }}>
                  <table
                    role="presentation"
                    width="100%"
                    cellPadding={0}
                    cellSpacing={0}
                    border={0}
                    style={{ borderCollapse: "separate" }}
                  >
                    <tbody>
                      <tr>
                        <td
                          className="e-card"
                          style={{
                            fontFamily: look.font,
                            textAlign: look.align,
                            backgroundColor: colours.card.light,
                            border: `1px solid ${colours.border.light}`,
                            borderRadius: "8px",
                            padding: "32px",
                          }}
                        >
                          {/* The brand is a Latin name, so it keeps the Latin fonts in every language. */}
                          <Text
                            className="e-text"
                            style={{
                              margin: 0,
                              fontFamily: latinFonts,
                              fontSize: "14px",
                              lineHeight: "20px",
                              fontWeight: 500,
                              color: colours.text.light,
                            }}
                          >
                            {brand.name}
                          </Text>
                          {children}
                          <Small language={language}>{footer}</Small>
                        </td>
                      </tr>
                    </tbody>
                  </table>
                </Container>
              </td>
            </tr>
          </tbody>
        </table>
      </Body>
    </Html>
  );
}

type PartProps = { language: Language; children: ReactNode };

/** The email's heading: what happened. */
export function Heading({ language, children }: PartProps) {
  const look = lookOf(language);
  return (
    <EmailHeading
      as="h1"
      className="e-text"
      style={{
        margin: `${gap} 0 0`,
        fontFamily: look.font,
        fontSize: look.heading.size,
        lineHeight: look.heading.line,
        fontWeight: 600,
        color: colours.text.light,
      }}
    >
      {children}
    </EmailHeading>
  );
}

/** A paragraph of the email's text. */
export function Paragraph({ language, children }: PartProps) {
  const look = lookOf(language);
  return (
    <Text
      className="e-text"
      style={{
        margin: `${gap} 0 0`,
        fontFamily: look.font,
        fontSize: "14px",
        lineHeight: look.bodyLine,
        color: colours.text.light,
      }}
    >
      {children}
    </Text>
  );
}

/** A muted line: the footer, and the words before a link to copy. */
export function Small({ language, children }: PartProps) {
  const look = lookOf(language);
  return (
    <Text
      className="e-muted"
      style={{
        margin: `${gap} 0 0`,
        fontFamily: look.font,
        fontSize: "13px",
        lineHeight: look.smallLine,
        color: colours.muted.light,
      }}
    >
      {children}
    </Text>
  );
}

/** The email's one button: the Primary button of the design system, 32px high. */
export function Button({
  language,
  href,
  children,
}: PartProps & { href: string }) {
  const look = lookOf(language);
  return (
    <table
      role="presentation"
      cellPadding={0}
      cellSpacing={0}
      border={0}
      style={{ marginTop: gap, borderCollapse: "separate" }}
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
            <EmailButton
              href={href}
              className="e-button"
              style={{
                fontFamily: look.font,
                display: "inline-block",
                boxSizing: "border-box",
                padding: look.button.padding,
                fontSize: look.button.size,
                lineHeight: look.button.line,
                fontWeight: 500,
                color: colours.onPrimary.light,
                backgroundColor: colours.primary.light,
                textDecoration: "none",
                borderRadius: "6px",
              }}
            >
              {children}
            </EmailButton>
          </td>
        </tr>
      </tbody>
    </table>
  );
}

/**
 * The button's link written out, for a client that breaks the button: the words before it, then the link, left to right in every
 * language.
 */
export function Fallback({
  language,
  href,
  children,
}: PartProps & { href: string }) {
  const look = lookOf(language);
  return (
    <>
      <Small language={language}>{children}</Small>
      <Text
        dir="ltr"
        style={{
          margin: "4px 0 0",
          fontFamily: look.font,
          fontSize: "13px",
          lineHeight: "18px",
          textAlign: look.align,
          wordBreak: "break-all",
        }}
      >
        <InlineLink href={href}>{href}</InlineLink>
      </Text>
    </>
  );
}

/** A link inside a text. */
export function InlineLink({
  href,
  children,
}: {
  href: string | undefined;
  children: ReactNode;
}) {
  return (
    <Link
      href={href}
      className="e-link"
      style={{ color: colours.primary.light, textDecoration: "underline" }}
    >
      {children}
    </Link>
  );
}

/** A code the reader types: large, spaced, and left to right in every language. */
export function Code({ language, children }: PartProps) {
  const style: CSSProperties = {
    margin: `${gap} 0 0`,
    fontFamily: codeFonts,
    fontSize: "32px",
    lineHeight: "40px",
    fontWeight: 600,
    letterSpacing: "8px",
    textAlign: lookOf(language).align,
    color: colours.text.light,
  };
  return (
    <Text className="e-text" dir="ltr" style={style}>
      {children}
    </Text>
  );
}
