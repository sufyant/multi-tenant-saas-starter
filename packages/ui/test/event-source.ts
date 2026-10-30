/**
 * An EventSource that a test drives: it opens, sends an event and fails when the test says so. `install` puts it in place of the
 * global for one test; `latest` is the stream the code opened last.
 */
export class FakeEventSource extends EventTarget {
  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSED = 2;
  static opened: FakeEventSource[] = [];
  readyState = FakeEventSource.CONNECTING;
  closed = false;

  constructor(readonly url: string) {
    super();
    FakeEventSource.opened.push(this);
  }

  static latest(): FakeEventSource {
    const last = FakeEventSource.opened.at(-1);
    if (!last) throw new Error("No EventSource was opened.");
    return last;
  }

  open() {
    this.readyState = FakeEventSource.OPEN;
    this.dispatchEvent(new Event("open"));
  }

  send(type: string, data: string) {
    this.dispatchEvent(new MessageEvent(type, { data }));
  }

  fail() {
    this.readyState = FakeEventSource.CLOSED;
    this.dispatchEvent(new Event("error"));
  }

  close() {
    this.readyState = FakeEventSource.CLOSED;
    this.closed = true;
  }
}
