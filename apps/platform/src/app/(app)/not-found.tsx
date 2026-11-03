import { NotFound } from "@repo/ui/components/not-found";

// An old link or a page that moved, inside the platform's shell: Organisations is the way on (the Page not found screens).
export default function PlatformNotFound() {
  return <NotFound kind="page" href="/" />;
}
