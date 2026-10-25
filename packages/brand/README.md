# @repo/brand

The product's identity, in one file: `brand.json` holds the product's name and its support contact. A product built from the template changes this one file, so a merge from the template does not conflict with it.

`src/index.ts` exports it with its type, `Brand`. The emails of `packages/emails` read it when they are rendered. The web apps move to it in a later step.

| Field | What it is |
| --- | --- |
| `name` | The name a person sees: the text at the top of every email |
| `supportUrl` | Where a person asks for help: an absolute `https` URL or a `mailto:` address. The deletion warning's "Contact us" links to it, and the API refuses any other kind of link |

## Tools and licenses

| Package | Use | License |
| --- | --- | --- |
| `typescript` | The types (development only) | Apache-2.0 |
