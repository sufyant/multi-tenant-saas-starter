import type { ReactNode } from "react";

/**
 * The tag of a message that isolates a name: `<bdi>{name}</bdi>`. In Arabic a Latin name would else reorder the punctuation around
 * it (W3C, "Inline markup and bidirectional text in HTML"). Pass it to `t.rich`: `t.rich("renameTitle", { name, ...isolated })`.
 */
export const isolated = {
  bdi: (chunks: ReactNode) => <bdi>{chunks}</bdi>,
};
