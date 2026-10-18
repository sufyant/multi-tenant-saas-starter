import type { Messages } from "./messages";

// Types next-intl's keys from the English messages, so a wrong key fails the type check.
declare module "next-intl" {
  interface AppConfig {
    Messages: Messages;
  }
}
