import type { ReactNode } from "react";
import { Button as ButtonBase } from "../ui/button";

const variants = {
  primary: "default",
  secondary: "outline",
  ghost: "ghost",
  destructive: "destructive",
} as const;
const sizes = { md: "default", lg: "lg", xl: "xl" } as const;

export type ButtonProps = Omit<
  React.ComponentProps<typeof ButtonBase>,
  "variant" | "size"
> & {
  /** primary once per view; secondary is the default; destructive only to remove, close or revoke. */
  variant?: keyof typeof variants;
  /** md 32px (desktop), lg 44px (phone), xl 48px (tablet). A coarse pointer raises md to 44px, and to 48px from bp-tablet. */
  size?: keyof typeof sizes;
  /** A 16px icon before the label. Without children the button is square and needs an `aria-label`. */
  icon?: ReactNode;
};

/** The Button of the design system, on shadcn's Button (Base UI). A label starts with a verb, in sentence case. */
export function Button({
  variant = "secondary",
  size = "md",
  icon,
  children,
  ...props
}: ButtonProps) {
  const square =
    icon !== undefined && (children === undefined || children === null);
  return (
    <ButtonBase
      variant={variants[variant]}
      size={square ? "icon" : sizes[size]}
      {...props}
    >
      {icon}
      {children}
    </ButtonBase>
  );
}
