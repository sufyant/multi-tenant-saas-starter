import { Button as ButtonPrimitive } from "@base-ui/react/button";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "../../lib/utils";

// shadcn/ui's Button on Base UI, restyled with the Starter tokens (the Button README): one action colour, a hairline secondary, a
// red destructive, 32px on desktop, 44px on a coarse pointer and 48px on a coarse pointer from bp-tablet, pressed at 0.97.
const buttonVariants = cva(
  "group/button inline-flex shrink-0 items-center justify-center gap-2 rounded-md border border-transparent text-sm font-medium whitespace-nowrap outline-none select-none transition-[background-color,border-color,transform] duration-[120ms] ease-out focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background active:scale-[0.97] motion-reduce:active:scale-100 disabled:pointer-events-none disabled:opacity-50 aria-invalid:border-destructive [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4",
  {
    variants: {
      variant: {
        default: "bg-primary text-primary-foreground hover:bg-primary-hover",
        outline:
          "border-input bg-card text-foreground hover:bg-accent aria-expanded:bg-accent",
        secondary: "bg-secondary text-secondary-foreground hover:bg-accent",
        ghost: "text-foreground hover:bg-accent aria-expanded:bg-accent",
        destructive:
          "bg-destructive text-destructive-foreground hover:bg-destructive/90",
        link: "text-primary underline-offset-4 hover:underline",
      },
      size: {
        default:
          "h-control-md px-3 pointer-coarse:h-control-lg tablet:pointer-coarse:h-control-xl",
        lg: "h-control-lg px-4 tablet:pointer-coarse:h-control-xl",
        xl: "h-control-xl px-4",
        icon: "size-control-md pointer-coarse:size-control-lg tablet:pointer-coarse:size-control-xl",
        "icon-sm":
          "size-control-md pointer-coarse:size-control-lg tablet:pointer-coarse:size-control-xl",
        "icon-xs": "size-6 pointer-coarse:size-control-lg",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  },
);

function Button({
  className,
  variant = "default",
  size = "default",
  ...props
}: ButtonPrimitive.Props & VariantProps<typeof buttonVariants>) {
  return (
    <ButtonPrimitive
      data-slot="button"
      className={cn(buttonVariants({ variant, size, className }))}
      {...props}
    />
  );
}

export { Button, buttonVariants };
