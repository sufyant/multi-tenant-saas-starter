# Cloudflare settings

Cloudflare R2 holds the files the API keeps, such as a person's data export (`apps/api/docs/ARCHITECTURE.md`, section 7, "File storage"). `config.json` holds the settings of R2 that Cloudflare's API manages. `scripts/cloudflare/config.sh` plans and applies them (`pnpm cloudflare:plan staging`, `pnpm cloudflare:apply staging`, and the same for `production`).

The file holds no value: no account id, no token and no key. The script reads the token and the account from the environment.

## What the file holds

- `r2.jurisdiction`: `eu`. A bucket's jurisdiction is set when the bucket is created, and it cannot change. A bucket of the EU jurisdiction keeps its data in the EU, and every call to it names the jurisdiction (header `cf-r2-jurisdiction`). Source: Cloudflare R2 docs, "Data location", "Jurisdictional restrictions".
- `r2.bucket_prefix`: each environment has one private bucket, `starter-staging` and `starter-production`. `.railway/railway.ts` gives the API the same name, and the endpoint of the EU jurisdiction, `https://<account id>.eu.r2.cloudflarestorage.com`.
- `r2.lifecycle_rules`: the rules of each bucket, which the script writes as the bucket's whole list.
  - `delete-exports-after-7-days`: deletes every file under `exports/` 7 days after its upload. The API deletes an export's file itself when its 7 days end; this rule is the second guard. A rule matches a prefix only, with no wildcard, so the exports have a prefix of their own, `exports/users/{userId}/` (the maintainer's decision in step 36). Source: Cloudflare R2 docs, "Object lifecycles".
  - `abort-multipart-uploads-after-7-days`: R2's default rule, which every new bucket has. The script writes the whole list, so the list keeps it.

## What the API manages

Checked on 2026-11-04 against Cloudflare's API reference ("R2", "Buckets", "Lifecycle").

| Setting | The API manages it | Where it lives |
| --- | --- | --- |
| A bucket and its jurisdiction | Yes: create, read. The jurisdiction only at creation | `config.json` |
| A bucket's lifecycle rules | Yes: read, replace the whole list | `config.json` |
| Public access and the `r2.dev` address | Off for a new bucket; the script turns neither on | none |
| An API token scoped to one bucket | Created in the dashboard, "R2", "Manage API tokens" | Railway: `Host__Storage__AccessKeyId`, `Host__Storage__SecretAccessKey` |

## The tokens

- `CLOUDFLARE_API_TOKEN`: an account API token with the permission "Workers R2 Storage: Edit", for this script only. Keep it in a password manager. It never goes into the repository, a GitHub secret or Railway.
- `CLOUDFLARE_ACCOUNT_ID`: the account's id. It is no secret, and the endpoint names it.
- The API's keys are another token: an R2 token with "Object Read and Write", scoped to the one bucket of the environment. Its key id and its secret go into Railway as the shared variables `Host__Storage__AccessKeyId` and `Host__Storage__SecretAccessKey`, by hand.

## Decisions

- **The script creates a bucket and never deletes one.** Deleting a bucket deletes its files, and R2 refuses to delete a bucket that holds any.
- **The script never prints the token.** It writes the token into a header file that only the user can read.
