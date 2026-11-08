import { describe, expect, it } from "vitest";
import { csvCell, csvRecords, csvStream } from "./csv";

const read = async (stream: ReadableStream<Uint8Array>) =>
  new TextDecoder("utf-8", { ignoreBOM: true }).decode(
    await new Response(stream).arrayBuffer(),
  );

async function* rowsOf(rows: string[][]) {
  for (const row of rows) yield row;
}

describe("csvCell", () => {
  it("CsvCell_PlainText_StaysAsItIs", () => {
    expect(csvCell("Omar Haddad")).toBe("Omar Haddad");
  });

  // RFC 4180, section 2: a field with a comma, a quote or a line break is quoted, and a quote inside is doubled.
  it.each([
    ["Haddad, Omar", '"Haddad, Omar"'],
    ['Omar "Oz" Haddad', '"Omar ""Oz"" Haddad"'],
    ["two\nlines", '"two\nlines"'],
  ])("CsvCell_%s_IsQuoted", (value, cell) => {
    expect(csvCell(value)).toBe(cell);
  });

  // OWASP, "CSV Injection": a cell that starts with = + - @, a tab or a carriage return is read as a formula; a leading quote
  // makes it text.
  it.each([
    [
      '=HYPERLINK("https://evil.example")',
      '"\'=HYPERLINK(""https://evil.example"")"',
    ],
    ["+1 555", "'+1 555"],
    ["-2+3", "'-2+3"],
    ["@SUM(A1)", "'@SUM(A1)"],
    ["\tcmd", "'\tcmd"],
    ["\r=1", '"\'\r=1"'],
  ])("CsvCell_FormulaStart_%s_IsEscaped", (value, cell) => {
    expect(csvCell(value)).toBe(cell);
  });

  it("CsvCell_FormulaSignInside_StaysAsItIs", () => {
    expect(csvCell("a=b")).toBe("a=b");
  });
});

describe("csvStream", () => {
  // A byte order mark, so a spreadsheet reads the file as UTF-8 (Arabic and Turkish names), and CRLF after each record (RFC 4180).
  it("CsvStream_HeaderAndRows_WritesOneRecordEach", async () => {
    const text = await read(
      csvStream(
        ["Name", "Email"],
        rowsOf([
          ["Omar Haddad", "omar@northwind.example"],
          ["عبد الرحمن", "a@northwind.example"],
        ]),
      ),
    );

    expect(text).toBe(
      "﻿Name,Email\r\nOmar Haddad,omar@northwind.example\r\nعبد الرحمن,a@northwind.example\r\n",
    );
  });

  it("CsvStream_NoRow_WritesTheHeaderOnly", async () => {
    expect(await read(csvStream(["Name"], rowsOf([])))).toBe("﻿Name\r\n");
  });

  // A large list is written while it is read: the stream asks for the next row only when the reader pulls.
  it("CsvStream_LargeList_ReadsTheRowsOnlyAsTheReaderPulls", async () => {
    let produced = 0;
    async function* many() {
      for (let row = 0; row < 10_000; row++) {
        produced++;
        yield [`member ${row}`];
      }
    }
    const reader = csvStream(["Name"], many()).getReader();

    await reader.read();
    await reader.read();

    expect(produced).toBeLessThan(10);
    await reader.cancel();
  });

  // A page that fails midway errors the stream, so the server cuts the answer and the browser never takes half a file.
  it("CsvStream_RowsFailMidway_ErrorsTheStream", async () => {
    async function* failing() {
      yield ["Omar Haddad"];
      throw new Error("page 2 failed");
    }

    await expect(read(csvStream(["Name"], failing()))).rejects.toThrow(
      "page 2 failed",
    );
  });
});

// The screen counts the records of a file it saved, to say when the Activity log's file holds its cap.
describe("csvRecords", () => {
  it("CsvRecords_FileOfTheStream_CountsTheRowsWithoutTheHeader", async () => {
    const file = await read(
      csvStream(
        ["Who", "What"],
        rowsOf([
          ["Omar", "joined"],
          ["Lena", "left"],
        ]),
      ),
    );

    expect(csvRecords(file)).toBe(2);
  });

  // RFC 4180, section 2: a line break inside a quoted field is part of the field, not the end of a record.
  it("CsvRecords_LineBreakInsideAQuotedField_IsNoRecord", async () => {
    const file = await read(
      csvStream(["What"], rowsOf([["one\r\ntwo"], ['say "hi"\r\n']])),
    );

    expect(csvRecords(file)).toBe(2);
  });

  it("CsvRecords_HeaderOnly_IsZero", async () => {
    expect(csvRecords(await read(csvStream(["What"], rowsOf([]))))).toBe(0);
  });
});
