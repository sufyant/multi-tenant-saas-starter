"use client";

// /dev/components (F33): every component as the design system's previews show it, in their sample data, with a language and a
// theme switch. Both apps mount it outside production builds. The sample texts are literals on purpose: this page is outside F25.
import { NextIntlClientProvider } from "next-intl";
import { useTheme } from "next-themes";
import { type ReactNode, useState } from "react";
import ar from "../../messages/ar.json";
import en from "../../messages/en.json";
import tr from "../../messages/tr.json";
import { AppShell } from "./app-shell";
import { Banner } from "./banner";
import { Button } from "./button";
import { CodeInput } from "./code-input";
import { Combobox } from "./combobox";
import { Dialog } from "./dialog";
import { EmptyState } from "./empty-state";
import { Freshness } from "./freshness";
import {
  BuildingIcon,
  GlobeIcon,
  HistoryIcon,
  HomeIcon,
  Icon,
  PlusIcon,
  SettingsIcon,
  UsersIcon,
} from "./icon";
import { Input } from "./input";
import { OrgMenu } from "./org-menu";
import { ProviderButtons } from "./provider-buttons";
import { RowMenu } from "./row-menu";
import { CodeStep, EmailStep, SecondFactorStep, TextButton } from "./sign-in";
import { Skeleton } from "./skeleton";
import { Status } from "./status";
import { SubNav } from "./sub-nav";
import { Table, type TableColumn } from "./table";
import { Tabs } from "./tabs";
import { Tag } from "./tag";
import { Time } from "./time";
import { showToast } from "./toast";
import { UserMenu } from "./user-menu";

const at = "2026-10-16T13:32:00Z";
const messages = { en, tr, ar };
const noop = () => {};
const now = "2026-10-16T15:40:00Z";

const samples = {
  en: {
    dir: "ltr" as const,
    locale: "en-IE",
    timeZone: "Europe/Dublin",
    invite: "Invite member",
    export: "Export",
    cancel: "Cancel",
    remove: "Remove member",
    saving: "Saving…",
    active: "Active",
    suspended: "Suspended",
    updated: "Updated",
    members: "Members",
    invitations: "Invitations",
    email: "Email",
    emailHint: "The invitation goes to this address.",
    removeQuestion: "Remove Ayşe Yılmaz from Northwind Traders?",
    removeDescription:
      "She loses access at once. Her past activity stays in the log.",
    bannerTitle: "Atlas Logistics is suspended.",
    bannerText: "Members cannot sign in. Reason: contract under review.",
    emptyTitle: "No pending invitations",
    emptyText:
      "Invite people by email. They join Northwind Traders when they accept.",
    toast: "Invitation sent",
    settings: "Settings",
    overview: "Overview",
    you: "You",
  },
  tr: {
    dir: "ltr" as const,
    locale: "tr-TR",
    timeZone: "Europe/Istanbul",
    invite: "Üye davet et",
    export: "Dışa aktar",
    cancel: "Vazgeç",
    remove: "Üyeyi çıkar",
    saving: "Kaydediliyor…",
    active: "Etkin",
    suspended: "Askıda",
    updated: "Güncellendi",
    members: "Üyeler",
    invitations: "Davetler",
    email: "E-posta",
    emailHint: "Davet bu adrese gider.",
    removeQuestion: "Ayşe Yılmaz Northwind Traders'dan çıkarılsın mı?",
    removeDescription:
      "Erişimini hemen kaybeder. Geçmiş etkinliği kayıtta kalır.",
    bannerTitle: "Atlas Logistics askıya alındı.",
    bannerText: "Üyeler giriş yapamaz. Neden: sözleşme incelemede.",
    emptyTitle: "Bekleyen davet yok",
    emptyText:
      "Kişileri e-postayla davet edin. Kabul ettiklerinde Northwind Traders'a katılırlar.",
    toast: "Davet gönderildi",
    settings: "Ayarlar",
    overview: "Genel bakış",
    you: "Siz",
  },
  ar: {
    dir: "rtl" as const,
    locale: "ar-SA",
    timeZone: "Asia/Riyadh",
    invite: "دعوة عضو",
    export: "تصدير",
    cancel: "إلغاء",
    remove: "إزالة العضو",
    saving: "جارٍ الحفظ…",
    active: "نشط",
    suspended: "موقوف",
    updated: "آخر تحديث",
    members: "الأعضاء",
    invitations: "الدعوات",
    email: "البريد الإلكتروني",
    emailHint: "تُرسل الدعوة إلى هذا العنوان.",
    removeQuestion: "إزالة عائشة يلماز من Northwind Traders؟",
    removeDescription: "ستفقد الوصول فورًا. يبقى نشاطها السابق في السجل.",
    bannerTitle: "Atlas Logistics موقوفة.",
    bannerText: "لا يمكن للأعضاء تسجيل الدخول.",
    emptyTitle: "لا توجد دعوات معلّقة",
    emptyText: "ادعُ الأشخاص بالبريد الإلكتروني.",
    toast: "تم إرسال الدعوة",
    settings: "الإعدادات",
    overview: "نظرة عامة",
    you: "أنت",
  },
};

type Sample = (typeof samples)[keyof typeof samples];
type Member = {
  id: number;
  name: string;
  email: string;
  initials: string;
  role: string;
  active: boolean;
  seen: string;
};

const members: Member[] = [
  {
    id: 1,
    name: "Ayşe Yılmaz",
    email: "ayse@northwind.io",
    initials: "AY",
    role: "Owner",
    active: true,
    seen: "2026-10-16T15:30:00Z",
  },
  {
    id: 2,
    name: "Mehmet Kaya",
    email: "mehmet@northwind.io",
    initials: "MK",
    role: "Admin",
    active: true,
    seen: "2026-10-16T12:20:00Z",
  },
  {
    id: 3,
    name: "İbrahim Bulut",
    email: "ibrahim@northwind.io",
    initials: "İB",
    role: "Member",
    active: true,
    seen: "2026-10-14T08:10:00Z",
  },
  {
    id: 4,
    name: "John Byrne",
    email: "john@northwind.io",
    initials: "JB",
    role: "Member",
    active: false,
    seen: "2026-09-22T10:00:00Z",
  },
];

const languages = [
  { id: "en-IE", label: "English (Ireland)", hint: "16/10/2026" },
  { id: "en-US", label: "English (United States)", hint: "10/16/2026" },
  {
    id: "tr-TR",
    label: "Türkçe",
    hint: "16.10.2026",
    lang: "tr-TR",
    keywords: ["Turkish"],
  },
  {
    id: "ar-SA",
    label: "العربية",
    hint: "16/10/2026",
    lang: "ar-SA",
    dir: "rtl" as const,
    keywords: ["Arabic"],
  },
];

function Section({ name, children }: { name: string; children: ReactNode }) {
  return (
    <section
      aria-labelledby={`dev-${name}`}
      className="flex flex-col gap-3 border-b border-border pb-8"
    >
      <h2
        id={`dev-${name}`}
        className="font-mono text-mono text-muted-foreground"
      >
        {name}
      </h2>
      <div className="flex flex-wrap items-start gap-3">{children}</div>
    </section>
  );
}

function memberColumns(sample: Sample): TableColumn<Member>[] {
  return [
    {
      key: "name",
      header: "Name",
      sortable: true,
      render: (row) => (
        <div className="flex flex-col">
          <span className="flex items-center gap-1.5 font-medium">
            <bdi>{row.name}</bdi>
            {row.id === 1 && <Tag>{sample.you}</Tag>}
          </span>
          <bdi dir="ltr" className="text-small text-muted-foreground">
            {row.email}
          </bdi>
        </div>
      ),
      cardRender: (row) => <bdi>{row.name}</bdi>,
    },
    { key: "role", header: "Role" },
    {
      key: "status",
      header: "Status",
      card: "end",
      render: (row) => (
        <Status tone={row.active ? "success" : "warning"}>
          {row.active ? sample.active : sample.suspended}
        </Status>
      ),
    },
    {
      key: "seen",
      header: "Last active",
      secondary: true,
      render: (row) => (
        <Time
          at={row.seen}
          now={now}
          show="relative"
          locale={sample.locale}
          timeZone={sample.timeZone}
        />
      ),
    },
  ];
}

export type DevComponentsPageProps = {
  /** The languages of the contract, from the generated `Language`: the page offers each one. */
  languages: readonly string[];
};

export function DevComponentsPage({
  languages: offered,
}: DevComponentsPageProps) {
  const [language, setLanguage] = useState<keyof typeof samples>(
    Object.keys(samples)[0] as keyof typeof samples,
  );
  const { theme = "system", setTheme } = useTheme();
  const sample = samples[language];
  const avatar = (row: Member) => (
    <span className="flex size-8 items-center justify-center rounded-full bg-muted text-small font-semibold">
      {row.initials}
    </span>
  );

  return (
    <div
      lang={language}
      dir={sample.dir}
      className="flex flex-col gap-8 bg-background p-6 text-foreground"
    >
      <header className="flex flex-wrap items-center gap-3">
        <h1 className="flex-1 text-page-title">Starter components</h1>
        <fieldset className="flex gap-1">
          <legend className="sr-only">Language</legend>
          {offered
            .filter((id): id is keyof typeof samples => id in samples)
            .map((id) => (
              <Button
                key={id}
                variant={id === language ? "primary" : "secondary"}
                onClick={() => setLanguage(id)}
              >
                {id}
              </Button>
            ))}
        </fieldset>
        <fieldset className="flex gap-1">
          <legend className="sr-only">Theme</legend>
          {["light", "dark", "system"].map((id) => (
            <Button
              key={id}
              variant={id === theme ? "primary" : "secondary"}
              onClick={() => setTheme(id)}
            >
              {id}
            </Button>
          ))}
        </fieldset>
      </header>

      <Section name="Button">
        <Button variant="primary" icon={<Icon icon={PlusIcon} />}>
          {sample.invite}
        </Button>
        <Button>{sample.export}</Button>
        <Button variant="ghost">{sample.cancel}</Button>
        <Button variant="destructive">{sample.remove}</Button>
        <Button variant="primary" disabled>
          {sample.saving}
        </Button>
        <Button size="lg">Phone 44</Button>
        <Button size="xl" variant="primary">
          Tablet 48
        </Button>
      </Section>

      <Section name="Icon">
        {Object.entries({
          HomeIcon,
          UsersIcon,
          BuildingIcon,
          HistoryIcon,
          SettingsIcon,
          GlobeIcon,
        }).map(([name, glyph]) => (
          <Icon key={name} icon={glyph} size={18} label={name} />
        ))}
      </Section>

      <Section name="Input">
        <Input
          className="w-72"
          label={sample.email}
          type="email"
          placeholder="name@company.com"
          hint={sample.emailHint}
        />
        <Input
          className="w-72"
          label={sample.email}
          type="email"
          defaultValue="ali@northwind"
          error="Enter an email address like name@company.com."
        />
        <Input
          className="w-72"
          label="Slug"
          defaultValue="northwind"
          lockedReason="Used in links. It can't be changed."
        />
      </Section>

      <Section name="CodeInput">
        <CodeInput label="Code" value="" onChange={noop} />
        <CodeInput
          label="Code"
          value="482190"
          onChange={noop}
          error="That code is not right. Check the email and try again."
        />
      </Section>

      <Section name="ProviderButtons">
        <div className="w-80">
          <ProviderButtons
            labels={{
              oauth_google: "Continue with Google",
              oauth_microsoft: "Continue with Microsoft",
            }}
            onSelect={noop}
          />
        </div>
      </Section>

      <NextIntlClientProvider
        locale={language}
        messages={messages[language]}
        timeZone={sample.timeZone}
      >
        <Section name="SignIn">
          <div className="flex w-[400px] max-w-full flex-col gap-6">
            <EmailStep
              title="Sign in to Starter"
              onSubmit={noop}
              onProvider={noop}
            />
          </div>
          <div className="flex w-[400px] max-w-full flex-col gap-6">
            <CodeStep
              email="ayse@northwind.io"
              onSubmit={noop}
              onResend={noop}
              onUseAnotherEmail={noop}
              error="codeExpired"
            />
          </div>
          <div className="flex w-[400px] max-w-full flex-col gap-6">
            <SecondFactorStep
              onSubmit={noop}
              exit={<TextButton>Sign out</TextButton>}
              error="locked"
            />
          </div>
        </Section>
      </NextIntlClientProvider>

      <Section name="Status">
        <Status tone="info">Provisioning</Status>
        <Status tone="success">{sample.active}</Status>
        <Status tone="warning">{sample.suspended}</Status>
        <Status tone="neutral">Closed</Status>
        <Status tone="destructive">Cancelled</Status>
      </Section>

      <Section name="Tag">
        <span className="flex items-center gap-1.5">
          Ayşe Yılmaz <Tag>{sample.you}</Tag>
        </span>
      </Section>

      <Section name="Time">
        <Time at={at} locale={sample.locale} timeZone={sample.timeZone} />
        <Time
          at={at}
          now={now}
          show="relative"
          locale={sample.locale}
          timeZone={sample.timeZone}
        />
        <Time
          at="2026-12-03T09:14:00Z"
          locale={sample.locale}
          timeZone={sample.timeZone}
        />
      </Section>

      <Section name="Freshness">
        <Freshness
          at={at}
          locale={sample.locale}
          timeZone={sample.timeZone}
          label={sample.updated}
        />
        <Freshness
          at="2026-10-16T09:05:00Z"
          locale={sample.locale}
          timeZone={sample.timeZone}
          label={sample.updated}
          stale
        />
      </Section>

      <Section name="Banner">
        <div className="w-full">
          <Banner
            tone="warning"
            title={sample.bannerTitle}
            action={<Button>Reactivate</Button>}
          >
            {sample.bannerText}
          </Banner>
        </div>
        <div className="w-full">
          <Banner tone="info" title="Shannon Studio is being set up.">
            The first owner gets an invitation when setup finishes.
          </Banner>
        </div>
      </Section>

      <Section name="EmptyState">
        <div className="w-full max-w-xl">
          <EmptyState
            title={sample.emptyTitle}
            description={sample.emptyText}
            action={
              <Button variant="primary" icon={<Icon icon={PlusIcon} />}>
                {sample.invite}
              </Button>
            }
          />
        </div>
        <div className="w-full max-w-xl">
          <EmptyState
            kind="error"
            title="Couldn’t load members"
            description="The connection dropped. Your data is safe. Try again."
            code="Ref 7f3a9c · 13:32 UTC"
            action={<Button>Try again</Button>}
          />
        </div>
      </Section>

      <Section name="Skeleton">
        <div className="w-full max-w-xl">
          <Skeleton rows={3} label="Loading members" />
        </div>
      </Section>

      <Section name="Tabs">
        <Tabs
          label={sample.members}
          current="members"
          items={[
            { id: "members", label: sample.members, count: 6 },
            { id: "invitations", label: sample.invitations, count: 3 },
          ]}
        />
      </Section>

      <Section name="SubNav">
        <SubNav
          label={sample.settings}
          current="members"
          groups={[
            {
              label: "Organisation",
              items: [
                { id: "general", label: "General", href: "#general" },
                { id: "members", label: sample.members, href: "#members" },
                { id: "activity", label: "Activity", href: "#activity" },
              ],
            },
            {
              label: sample.you,
              items: [
                {
                  id: "preferences",
                  label: "Preferences",
                  href: "#preferences",
                },
              ],
            },
          ]}
        />
      </Section>

      <Section name="Table">
        <div className="w-full">
          <Table
            label={sample.members}
            columns={memberColumns(sample)}
            rows={members}
            rowKey="id"
            rowCount={members.length}
            page={1}
            pageSize={20}
            pager={{
              previous: "Previous",
              next: "Next",
              page: (page, count) => `Page ${page} of ${count}`,
            }}
            cardLead={avatar}
            actionsLabel="Actions"
            actions={(row) => (
              <RowMenu
                label={`Actions for ${row.name}`}
                items={[{ label: "Change role" }]}
              />
            )}
          />
        </div>
        <div className="w-[360px] rounded-lg border border-border p-3">
          <Table
            label={`${sample.members} (phone)`}
            columns={memberColumns(sample)}
            rows={members}
            rowKey="id"
            rowCount={members.length}
            page={1}
            pageSize={20}
            pager={{
              previous: "Previous",
              next: "Next",
              page: (page, count) => `Page ${page} of ${count}`,
            }}
            cardLead={avatar}
            actions={(row) => (
              <RowMenu
                label={`Actions for ${row.name}`}
                items={[{ label: "Change role" }]}
              />
            )}
          />
        </div>
      </Section>

      <Section name="RowMenu">
        <RowMenu
          label="Actions for Ayşe Yılmaz"
          items={[
            {
              label: "Change role",
              disabled: true,
              reason: "An organisation needs at least one Owner.",
            },
            { separator: true },
            {
              label: "Remove from organisation",
              tone: "destructive",
              disabled: true,
              reason: "An organisation needs at least one Owner.",
            },
          ]}
        />
      </Section>

      <Section name="Combobox">
        <Combobox
          className="w-72"
          label="Language and format"
          value="en-IE"
          options={languages}
          searchPlaceholder="Search languages"
          emptyLabel="No language found"
        />
        <Combobox
          variant="compact"
          icon={GlobeIcon}
          label="Language"
          value="en-IE"
          options={[{ id: "en-IE", label: "English" }]}
          searchPlaceholder="Search languages"
          emptyLabel="No language found"
        />
      </Section>

      <Section name="Dialog">
        <Dialog
          trigger={<Button variant="destructive">{sample.remove}</Button>}
          tone="destructive"
          title={sample.removeQuestion}
          description={sample.removeDescription}
          actionLabel={sample.remove}
          cancelLabel={sample.cancel}
        />
        <Dialog
          trigger={<Button>Close tenant</Button>}
          tone="destructive"
          title="Close Northwind Traders?"
          description="Members cannot sign in. Pending invitations are cancelled. You can reopen it within 30 days."
          confirmText="northwind"
          confirmLabel="Type northwind to confirm"
          actionLabel="Close tenant"
          cancelLabel={sample.cancel}
        />
      </Section>

      <Section name="Toast">
        <Button
          onClick={() =>
            showToast({
              title: sample.toast,
              description: "ali@northwind.io has 7 days to accept.",
            })
          }
        >
          {sample.toast}
        </Button>
        <Button
          onClick={() =>
            showToast({
              tone: "destructive",
              title: "Couldn’t send the invitation",
              description:
                "This tenant has sent 50 invitations today. Try again tomorrow.",
            })
          }
        >
          Couldn’t send
        </Button>
      </Section>

      <Section name="OrgMenu">
        <OrgMenu
          name="Northwind Traders"
          slug="northwind"
          initials="N"
          memberRole="Owner"
          switchLabel="Switch organisation"
          switchHref="#choose"
        />
      </Section>

      <Section name="UserMenu">
        <UserMenu
          name="Ayşe Yılmaz"
          email="ayse@northwind.io"
          initials="AY"
          label="Account"
          languageName={
            languages.find((option) => option.id === sample.locale)?.label ??
            sample.locale
          }
          languageLabel="Language"
          preferencesHref="#preferences"
          theme={theme}
          themes={[
            { id: "light", label: "Light" },
            { id: "dark", label: "Dark" },
            { id: "system", label: "System" },
          ]}
          themeLabel="Theme"
          onThemeChange={setTheme}
          signOutLabel="Sign out"
          onSignOut={() => {}}
        />
      </Section>

      <Section name="AppShell">
        <div className="h-[420px] w-full overflow-hidden rounded-lg border border-border">
          <AppShell
            nav={[
              {
                id: "overview",
                label: sample.overview,
                icon: HomeIcon,
                href: "#overview",
              },
              {
                id: "settings",
                label: sample.settings,
                icon: SettingsIcon,
                href: "#settings",
                placement: "bottom",
              },
            ]}
            current="settings"
            logo="S"
            logoLabel="Home"
            logoHref="#home"
            crumbs={[
              { label: "Northwind Traders" },
              { label: sample.settings },
              { label: sample.members },
            ]}
            orgMenu={
              <OrgMenu
                name="Northwind Traders"
                slug="northwind"
                initials="N"
                memberRole="Owner"
                switchLabel="Switch organisation"
                switchHref="#choose"
              />
            }
            userMenu={
              <span className="flex size-8 items-center justify-center rounded-full bg-muted text-small font-semibold">
                AY
              </span>
            }
            labels={{
              navigation: "Main",
              menu: "Open menu",
              search: "Search",
              searchShortcut: "⌘K",
              breadcrumb: "Breadcrumb",
              commandTitle: "Go to",
              commandPlaceholder: "Search pages",
              commandEmpty: "No page found",
              close: "Close",
            }}
            onNavigate={() => {}}
          >
            <h1 className="text-page-title">{sample.members}</h1>
            <Freshness
              at={at}
              locale={sample.locale}
              timeZone={sample.timeZone}
              label={sample.updated}
            />
          </AppShell>
        </div>
      </Section>
    </div>
  );
}
