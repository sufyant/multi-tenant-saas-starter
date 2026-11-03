import { PlatformShell } from "@/features/shell/platform-shell";

export default function PlatformLayout({ children }: LayoutProps<"/">) {
  return <PlatformShell>{children}</PlatformShell>;
}
