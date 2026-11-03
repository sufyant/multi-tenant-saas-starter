import { pseudoMessages } from "@repo/ui/lib/pseudo-locale";
import sharedAr from "@repo/ui/messages/ar.json";
import sharedEn from "@repo/ui/messages/en.json";
import sharedTr from "@repo/ui/messages/tr.json";
import ownAr from "../../messages/ar.json";
import ownEn from "../../messages/en.json";
import ownTr from "../../messages/tr.json";

// The app's messages merged with the shared ones of packages/ui (F14), for each language of the contract.
export const messages = {
  en: { ...sharedEn, ...ownEn },
  tr: { ...sharedTr, ...ownTr },
  ar: { ...sharedAr, ...ownAr },
};

export type Messages = (typeof messages)["en"];

/** A language's messages, or for the test locale every text of them 40% longer with accents (F60). */
export const messagesFor = (locale: keyof typeof messages, pseudo: boolean) =>
  pseudo ? pseudoMessages(messages[locale]) : messages[locale];
