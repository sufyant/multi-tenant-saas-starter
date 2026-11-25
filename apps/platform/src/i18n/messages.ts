import { pseudoMessages } from "@repo/ui/lib/pseudo-locale";
import { withProduct } from "@repo/ui/lib/with-product";
import sharedAr from "@repo/ui/messages/ar.json";
import sharedEn from "@repo/ui/messages/en.json";
import sharedTr from "@repo/ui/messages/tr.json";
import ownAr from "../../messages/ar.json";
import ownEn from "../../messages/en.json";
import productAr from "../../messages/product/ar.json";
import productEn from "../../messages/product/en.json";
import productTr from "../../messages/product/tr.json";
import ownTr from "../../messages/tr.json";

// The app's messages merged with the shared ones of packages/ui (F14), for each language of the contract, with the product's words
// over them (`messages/product`, which the template ships empty).
export const messages = {
  en: withProduct({ ...sharedEn, ...ownEn }, productEn),
  tr: withProduct({ ...sharedTr, ...ownTr }, productTr),
  ar: withProduct({ ...sharedAr, ...ownAr }, productAr),
};

export type Messages = (typeof messages)["en"];

/** A language's messages, or for the test locale every text of them 40% longer with accents (F60). */
export const messagesFor = (locale: keyof typeof messages, pseudo: boolean) =>
  pseudo ? pseudoMessages(messages[locale]) : messages[locale];
