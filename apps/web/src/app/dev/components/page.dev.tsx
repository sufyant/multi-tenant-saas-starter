import { Language } from "@repo/api-client";
import { DevComponentsPage } from "@repo/ui/dev-components-page";

// /dev/components (F33). The `.dev.tsx` extension is a page extension only outside production builds (next.config.ts), so a
// production build has no such route.
export default function DevComponents() {
  return <DevComponentsPage languages={Object.values(Language)} />;
}
