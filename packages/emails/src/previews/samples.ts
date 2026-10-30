import { Language } from "@repo/api-client";
import { brand } from "@repo/brand";
import type { DeletionWarningProps } from "../templates/deletion-warning";
import type { InvitationProps } from "../templates/invitation";
import type { NotificationProps } from "../templates/notification";

// The sample values of the previews: the design system's invented names and `.example` addresses, written as the API writes them in
// each language (the email boards).

export const invitation = {
  en: {
    language: Language.En,
    inviterName: "Ayşe Yılmaz",
    tenantName: "Northwind Traders",
    roleName: "Admin",
    expiresAt: "1 Nov 2026 at 13:32 Dublin (UTC+0)",
    link: "https://web.example/invitations/accept?code=k3Q9vXbT2mLp8Rz",
  },
  tr: {
    language: Language.Tr,
    inviterName: "Ayşe Yılmaz",
    tenantName: "Northwind Traders",
    roleName: "Yönetici",
    expiresAt: "1 Kas 2026, saat 16:32 Istanbul (UTC+3)",
    link: "https://web.example/invitations/accept?code=k3Q9vXbT2mLp8Rz",
  },
  ar: {
    language: Language.Ar,
    inviterName: "Ayşe Yılmaz",
    tenantName: "Northwind Traders",
    roleName: "مشرف",
    expiresAt: "1 نوفمبر 2026 الساعة 13:32 بتوقيت Dublin (UTC+0)",
    link: "https://web.example/invitations/accept?code=k3Q9vXbT2mLp8Rz",
  },
} satisfies Record<string, InvitationProps>;

/** The platform invites a tenant's Owner: the inviter is the brand. */
export const ownerInvitation: InvitationProps = {
  ...invitation.en,
  inviterName: brand.name,
  roleName: "Owner",
};

export const deletionWarning = {
  en: {
    language: Language.En,
    tenantName: "Northwind Traders",
    purgesOn: "10 Nov 2026",
    purgesAt: "10 Nov 2026 at 09:00 Istanbul (UTC+3)",
    supportUrl: brand.supportUrl,
  },
  tr: {
    language: Language.Tr,
    tenantName: "Northwind Traders",
    purgesOn: "10 Kas 2026",
    purgesAt: "10 Kas 2026, saat 09:00 Istanbul (UTC+3)",
    supportUrl: brand.supportUrl,
  },
  ar: {
    language: Language.Ar,
    tenantName: "Northwind Traders",
    purgesOn: "10 نوفمبر 2026",
    purgesAt: "10 نوفمبر 2026 الساعة 09:00 بتوقيت Istanbul (UTC+3)",
    supportUrl: brand.supportUrl,
  },
} satisfies Record<string, DeletionWarningProps>;

export const notification = {
  en: {
    language: Language.En,
    title: "Your role in Northwind Traders changed",
    body: "Grace Hopper changed your role from Member to Admin.",
    link: "https://app.example/",
    footer:
      "You got this email because email notifications for “Your access” are on.",
    preferencesLink: "https://app.example/settings/notifications",
  },
} satisfies Record<string, NotificationProps>;
