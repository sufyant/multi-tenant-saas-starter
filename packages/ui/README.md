# @repo/ui

The look and the shared parts of every frontend app: the design tokens of the Starter design system, the global CSS, the shared messages and the architecture rules. Rules F20 to F37 in `docs/frontend/ARCHITECTURE.md` say what each part must do.

## Where the files come from

- `tokens/tokens.json` is the source of every design token. It is a copy of `tokens.json` of the Starter design system. A product keeps the token names and may change the values.
- `scripts/build-tokens.mjs` writes `src/styles/tokens.gen.css` from it: light colours and shadows on `:root`, dark ones on `.dark`, every other token on `:root`, and the breakpoints in a Tailwind `@theme` block (F20).
- `src/styles/globals.css` imports `tokens.gen.css` and maps the tokens into Tailwind's theme, so `bg-primary`, `text-muted-foreground`, `rounded-md`, `h-control-md`, `shadow-md` and `tablet:` work.
- `messages/{en,tr,ar}.json` hold the shared texts. Today that is one message for each error code of the API (`errors.<code>`, F16). The apps merge them with their own messages at load (F14).
- `test/architecture-rules.ts` holds the architecture rules that Biome cannot check, as pure functions of a folder (F36). `src/architecture.test.ts` runs them on this package; each app's `architecture.test.ts` runs them on the app.

Never edit `src/styles/tokens.gen.css`. Change `tokens/tokens.json` and run `pnpm generate` (or `pnpm ui:tokens`) at the repository root. Commit both files. The CI job `generated` fails when the committed CSS is not what the script writes.

## Commands

Run from this folder, or with `pnpm --filter @repo/ui` from the root.

- `pnpm generate:tokens`: write `src/styles/tokens.gen.css`.
- `pnpm test`: the token script's tests, the rules' tests and the architecture tests of this package.
- `pnpm check-types`: TypeScript, the scripts included.

## Tools and licenses

| Package | Use | License |
| --- | --- | --- |
| `tailwindcss` | The CSS framework (development only) | MIT |
| `tw-animate-css` | The animation utilities of shadcn/ui | MIT |
| `shadcn` | The CSS of shadcn/ui (`shadcn/tailwind.css`) and its CLI | MIT |
| `class-variance-authority` | The variants of a component | Apache-2.0 |
| `lucide-react` | The icons, only through the `Icon` component (F22) | ISC |
| `radix-ui`, `@radix-ui/react-slot`, `cn` | The shadcn scaffold in `src/components/ui/`, until the components move to Base UI | MIT |
| `@clerk/nextjs` | A peer dependency that no file imports. It goes when the components move to Base UI | MIT |
| `vitest` | The tests (development only) | MIT |
| `typescript` | The types, and the parser of the architecture rules (development only) | Apache-2.0 |

`.github/dependency-review-config.yml` holds the licenses a dependency may have.
