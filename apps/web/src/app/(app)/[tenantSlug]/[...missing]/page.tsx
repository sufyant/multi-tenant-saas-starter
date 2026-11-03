import { notFound } from "next/navigation";

// Any address under the shell that no page has: "Page not found" inside the shell, so the menu stays (the Page not found screens).
export default function MissingPage() {
  notFound();
}
