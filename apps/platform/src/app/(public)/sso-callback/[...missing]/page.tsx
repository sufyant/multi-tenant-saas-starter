import { notFound } from "next/navigation";

// A path under an open route that no page has: "Page not found" outside the shell, not the shell's catch-all (F59).
export default function MissingPage() {
  notFound();
}
