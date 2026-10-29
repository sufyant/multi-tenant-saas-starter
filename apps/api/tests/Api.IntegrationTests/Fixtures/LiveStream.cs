namespace Api.IntegrationTests.Fixtures;

// An open stream of the signed-in user's live signals (U6), read one block at a time: an event, or a comment line.
internal sealed class LiveStream(HttpResponseMessage response, StreamReader reader) : IAsyncDisposable
{
    public const string Path = "/v1/me/notifications/stream";

    public const string KeepAlive = ": keep-alive";

    public HttpResponseMessage Response => response;

    public static async Task<LiveStream> OpenAsync(HttpClient client)
    {
        var response = await client.GetAsync(Path, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        return new LiveStream(response, new StreamReader(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken)));
    }

    // The next block, its lines joined by a line break; "closed" when the stream ends, and "nothing" when nothing comes in time.
    public async Task<string> NextAsync(TimeSpan within)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(within);
        List<string> lines = [];
        try
        {
            while (await reader.ReadLineAsync(timeout.Token) is { } line)
            {
                if (line.Length == 0)
                {
                    return string.Join('\n', lines);
                }

                lines.Add(line);
            }

            return "closed";
        }
        catch (OperationCanceledException) when (!TestContext.Current.CancellationToken.IsCancellationRequested)
        {
            return "nothing";
        }
    }

    // The next block that is not a comment line.
    public async Task<string> NextEventAsync(TimeSpan within)
    {
        var deadline = TimeProvider.System.GetTimestamp() + (long)(within.TotalSeconds * TimeProvider.System.TimestampFrequency);
        string block;
        do
        {
            block = await NextAsync(TimeProvider.System.GetElapsedTime(TimeProvider.System.GetTimestamp(), deadline).Duration());
        }
        while (block == KeepAlive);

        return block;
    }

    public async ValueTask DisposeAsync()
    {
        reader.Dispose();
        response.Dispose();
        await Task.CompletedTask;
    }
}
