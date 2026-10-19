import { Button } from "../button";

export type Provider = "oauth_google" | "oauth_microsoft";

export type ProviderButtonsProps = {
  labels: Record<Provider, string>;
  onSelect: (provider: Provider) => void;
  disabled?: boolean;
};

const providers: { id: Provider; logo: string }[] = [
  { id: "oauth_google", logo: "/brands/google.svg" },
  { id: "oauth_microsoft", logo: "/brands/microsoft.svg" },
];

/**
 * Google and Microsoft, before the email code (the D5 boards). Each logo keeps its owner's colours, as the providers' brand rules
 * ask, so it is an image of the app (`public/brands/`), not a token. The label names the provider; the logo is decoration.
 */
export function ProviderButtons({
  labels,
  onSelect,
  disabled,
}: ProviderButtonsProps) {
  return (
    <div className="flex flex-col gap-3">
      {providers.map(({ id, logo }) => (
        <Button
          key={id}
          type="button"
          size="lg"
          className="w-full justify-center gap-2.5"
          disabled={disabled}
          onClick={() => onSelect(id)}
          icon={
            // biome-ignore lint/performance/noImgElement: a fixed 18px logo needs no optimisation, and packages/ui has no next/image.
            <img src={logo} alt="" width={18} height={18} />
          }
        >
          {labels[id]}
        </Button>
      ))}
    </div>
  );
}
