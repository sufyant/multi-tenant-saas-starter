import { TermsGate } from "@/features/terms/terms-gate";
import { NotificationStream } from "@/shared/notification-stream";

export default function SignedInLayout({ children }: LayoutProps<"/">) {
  // One live stream of notifications for the tab, across every signed-in page: the organisations' and those without one. Every
  // signed-in page passes the terms first.
  return (
    <>
      <NotificationStream />
      <TermsGate>{children}</TermsGate>
    </>
  );
}
