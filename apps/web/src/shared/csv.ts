// The CSV files of the exports (Members, the Activity log): RFC 4180 records, written as a stream while the rows are read.

/** A first character that a spreadsheet reads as the start of a formula (OWASP, "CSV Injection"). */
const formulaStart = /^[=+\-@\t\r]/;
const needsQuotes = /[",\r\n]/;

/** One field: a formula start becomes text with a leading quote (OWASP), and a field with a comma, quote or line break is quoted. */
export function csvCell(value: string): string {
  const text = formulaStart.test(value) ? `'${value}` : value;
  return needsQuotes.test(text) ? `"${text.replaceAll('"', '""')}"` : text;
}

const record = (cells: readonly string[]) =>
  `${cells.map(csvCell).join(",")}\r\n`;

/**
 * The file: a byte order mark, so a spreadsheet reads UTF-8, the header, then one record for each row. A row is read only when
 * the reader pulls, so a large list never sits in memory. A row source that fails errors the stream, and the answer is cut.
 */
export function csvStream(
  header: readonly string[],
  rows: AsyncIterable<readonly string[]>,
): ReadableStream<Uint8Array> {
  const encoder = new TextEncoder();
  const iterator = rows[Symbol.asyncIterator]();
  return new ReadableStream({
    start(controller) {
      controller.enqueue(encoder.encode(`﻿${record(header)}`));
    },
    async pull(controller) {
      const next = await iterator.next();
      if (next.done) controller.close();
      else controller.enqueue(encoder.encode(record(next.value)));
    },
    async cancel() {
      await iterator.return?.();
    },
  });
}
