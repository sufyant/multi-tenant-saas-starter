import { NotificationStream } from "@/shared/notification-stream";

export default function SignedInLayout({ children }: LayoutProps<"/">) {
  // One live stream of notifications for the tab, across every signed-in page: the organisations' and those without one.
  return (
    <>
      <NotificationStream />
      {children}
    </>
  );
}
