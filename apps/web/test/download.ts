import { vi } from "vitest";

/** The names of the files the page saves. jsdom has no object URLs and no downloads. */
export const savedFiles = () => {
  const names: string[] = [];
  Object.assign(URL, {
    createObjectURL: () => "blob:https://web.example.test/file",
    revokeObjectURL: () => {},
  });
  vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(function (
    this: HTMLAnchorElement,
  ) {
    names.push(this.download);
  });
  return names;
};
