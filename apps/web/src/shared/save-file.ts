import type { DownloadedFile } from "@repo/api-client";

/** Saves an export's file under the server's name (the maintainer's decision of 2026-11-07): the browser makes no second name. */
export function saveFile({ blob, name }: DownloadedFile) {
  const link = document.createElement("a");
  link.href = URL.createObjectURL(blob);
  link.download = name;
  link.click();
  // A browser may still read the address after the click starts the download, so it goes on the next task.
  setTimeout(() => URL.revokeObjectURL(link.href));
}
