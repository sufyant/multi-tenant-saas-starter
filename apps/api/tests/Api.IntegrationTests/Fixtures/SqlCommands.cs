using System.Collections.Concurrent;
using System.Diagnostics;

namespace Api.IntegrationTests.Fixtures;

// The SQL commands one request sends, counted from Npgsql's spans. The request carries a trace id of its own (traceparent), and every
// span of its handlers and their commands is a child of the request's span, so another test's commands never count. A list that sends a
// query for each row sends more commands for a longer page; a list that reads its page in one query sends as many for any page.
internal sealed class SqlCommands : IDisposable
{
    private readonly ConcurrentDictionary<ActivityTraceId, byte> _traces = new();
    private readonly ConcurrentQueue<ActivityTraceId> _commands = new();
    private readonly ActivityListener _listener;

    public SqlCommands()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Npgsql",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                _traces.ContainsKey(options.Parent.TraceId) ? ActivitySamplingResult.AllDataAndRecorded : ActivitySamplingResult.None,
            ActivityStopped = span =>
            {
                // A span with no query text is no command, such as the opening of a physical connection.
                if (span.GetTagItem("db.query.text") is not null || span.GetTagItem("db.statement") is not null)
                {
                    _commands.Enqueue(span.TraceId);
                }
            },
        };
        ActivitySource.AddActivityListener(_listener);
    }

    // The request is sent twice, and only the second is counted: the first opens the connections and loads the database's types, which
    // a pool does once.
    public async Task<int> CountAsync(Func<HttpClient> client, Func<HttpClient, Task<HttpResponseMessage>> send)
    {
        (await send(client())).EnsureSuccessStatusCode();
        var traceId = ActivityTraceId.CreateRandom();
        _traces[traceId] = 0;
        var traced = client();
        traced.DefaultRequestHeaders.Add("traceparent", $"00-{traceId}-{ActivitySpanId.CreateRandom()}-01");

        (await send(traced)).EnsureSuccessStatusCode();

        return _commands.Count(command => command == traceId);
    }

    public void Dispose() => _listener.Dispose();
}
