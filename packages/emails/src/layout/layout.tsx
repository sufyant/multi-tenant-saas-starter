import type { Language } from "@repo/api-client";
import { brand } from "@repo/brand";
import type { CSSProperties, ReactNode } from "react";
import { Body, Container, Head, Html, Preview } from "react-email";
import { directionOf } from "../texts";
import { colours } from "./colours";

// The system font stacks of the email boards. An email loads no web font, so the stack names the fonts each platform has; Arabic
// takes the platforms' Arabic fonts first.
const latinFonts =
  "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif";
const arabicFonts =
  "-apple-system, BlinkMacSystemFont, 'Segoe UI', Tahoma, 'Geeza Pro', Arial, sans-serif";

// A client that follows the reader's dark theme reads these rules; the others keep the light colours, which every element carries
// inline. A class names each colour role, so the rule overrides the inline colour.
const darkTheme = `
:root { color-scheme: light dark; supported-color-schemes: light dark; }
@media (prefers-color-scheme: dark) {
  .e-canvas { background-color: ${colours.canvas.dark} !important; }
  .e-card { background-color: ${colours.card.dark} !important; border-color: ${colours.border.dark} !important; }
  .e-rule { border-color: ${colours.border.dark} !important; }
  .e-text { color: ${colours.text.dark} !important; }
  .e-muted { color: ${colours.muted.dark} !important; }
  .e-link { color: ${colours.primary.dark} !important; }
  .e-button { background-color: ${colours.primary.dark} !important; color: ${colours.onPrimary.dark} !important; }
}
`;

/** What a part of an email needs to look as the boards draw it in its language. */
export type Look = {
  font: string;
  align: "left" | "right";
  /** Line heights: Arabic needs taller lines. */
  line: { small: string; body: string; heading: string; footer: string };
};

export function lookOf(language: Language): Look {
  const rtl = directionOf(language) === "rtl";
  return {
    font: rtl ? arabicFonts : latinFonts,
    align: rtl ? "right" : "left",
    line: rtl
      ? { small: "24px", body: "28px", heading: "36px", footer: "20px" }
      : { small: "20px", body: "24px", heading: "30px", footer: "18px" },
  };
}

type LayoutProps = {
  language: Language;
  /** The line an inbox shows after the subject. */
  preheader: string;
  footer: ReactNode;
  children: ReactNode;
};

/** Every email: the brand as text at the top, one 600px column with a card, and the footer under a rule in the card. */
export function Layout({ language, preheader, footer, children }: LayoutProps) {
  const look = lookOf(language);
  const cell: CSSProperties = { fontFamily: look.font, textAlign: look.align };
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
                  padding: "40px 0",
                  backgroundColor: colours.canvas.light,
                }}
              >
                <Container style={{ width: "100%", maxWidth: "600px" }}>
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
                          className="e-text"
                          style={{
                            ...cell,
                            padding: "0 4px 16px",
                            fontSize: "18px",
                            lineHeight: "24px",
                            fontWeight: 600,
                            color: colours.text.light,
                          }}
                        >
                          {brand.name}
                        </td>
                      </tr>
                      <tr>
                        <td
                          className="e-card"
                          style={{
                            ...cell,
                            backgroundColor: colours.card.light,
                            border: `1px solid ${colours.border.light}`,
                            borderRadius: "8px",
                            padding: "40px",
                          }}
                        >
                          {children}
                          <table
                            role="presentation"
                            width="100%"
                            cellPadding={0}
                            cellSpacing={0}
                            border={0}
                            style={{
                              marginTop: "32px",
                              borderCollapse: "collapse",
                            }}
                          >
                            <tbody>
                              <tr>
                                <td
                                  className="e-rule e-muted"
                                  style={{
                                    ...cell,
                                    borderTop: `1px solid ${colours.border.light}`,
                                    paddingTop: "20px",
                                    fontSize: "12px",
                                    lineHeight: look.line.footer,
                                    color: colours.muted.light,
                                  }}
                                >
                                  {footer}
                                </td>
                              </tr>
                            </tbody>
                          </table>
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
