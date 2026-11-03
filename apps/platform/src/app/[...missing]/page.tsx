import { notFound } from "next/navigation";

// Any address that no page has. It sits outside the route groups, so notFound() comes before the response starts and the answer is a
// 404; the root not-found.tsx shows "Page not found" inside the shell, or before sign-in when signed out (F59).
export default function MissingPage() {
  notFound();
}
