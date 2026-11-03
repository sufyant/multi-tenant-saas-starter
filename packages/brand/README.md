# @repo/brand

The product's identity, in one file: `brand.json` holds the product's name, its support contact, its privacy policy and its status page. A product built from the template changes this one file, so a merge from the template does not conflict with it.

`src/index.ts` exports it with its type, `Brand`. The emails of `packages/emails` read it when they are rendered, and both web apps read it at build time.

| Field | What it is |
| --- | --- |
| `name` | The name a person sees: the text at the top of every email |
| `supportUrl` | Where a person asks for help: an absolute `https` URL or a `mailto:` address. The deletion warning's "Contact us" links to it, and the API refuses any other kind of link. Each "Contact support" of the apps opens it; a `mailto:` address gets "Version: a1b2c3d" in the body |
| `privacyUrl` | The privacy policy: an absolute `https` URL. The footer of every screen before sign-in links to it as "Privacy". The example address is a placeholder; a product sets its own |
| `statusUrl` | Optional. The status page: an absolute `https` URL. While the server does not answer, the connection bar shows "Check the status page" with it. Without it the link does not show |

## Tools and licenses

| Package | Use | License |
| --- | --- | --- |
| `typescript` | The types (development only) | Apache-2.0 |
