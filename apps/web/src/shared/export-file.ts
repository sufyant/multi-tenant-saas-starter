import { csvStream } from "./csv";

/** A call of the API as the signed-in person, through the BFF and its checks (F6). It answers the API's status and body. */
export type ExportApi = (
  path: string,
  method?: "GET" | "POST",
) => Promise<Response>;

/** The API through the BFF's `forward`, from a route handler of the web: the path is the API's, as the generated URLs write it. */
export const exportApi =
  (
    request: Request,
    forward: (request: Request) => Promise<Response>,
  ): ExportApi =>
  (path, method = "GET") =>
    forward(new Request(new URL(`/api${path}`, request.url), { method }));

/** A page of a list for the file. An error answer throws, so the stream errors and the answer is cut: no half file. */
export async function readPage<T>(api: ExportApi, path: string): Promise<T> {
  const answer = await api(path);
  if (!answer.ok) throw new Error(`${path} answered ${answer.status}.`);
  return (await answer.json()) as T;
}

/**
 * An export's answer (the members' file, the Activity log's): the audit command first, and no file when it fails, whose problem
 * passes as it came (F4); then the CSV, written while the rows are read. The name is the one place of the file's name (D12).
 */
export async function exportFile(
  api: ExportApi,
  {
    audit,
    name,
    header,
    rows,
  }: {
    /** The API path of the audit command: 204 lets the file come. */
    audit: string;
    name: string;
    header: readonly string[];
    rows: AsyncIterable<readonly string[]>;
  },
): Promise<Response> {
  const audited = await api(audit, "POST");
  if (audited.status !== 204) return audited;
  return new Response(csvStream(header, rows), {
    headers: {
      "Content-Type": "text/csv; charset=utf-8",
      "Content-Disposition": `attachment; filename="${name}"`,
      "Cache-Control": "no-store",
    },
  });
}
