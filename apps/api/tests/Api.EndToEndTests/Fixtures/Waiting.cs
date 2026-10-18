namespace Api.EndToEndTests.Fixtures;

// Waits for a state the test can see, such as an email that went out: the tenant creation runs in the worker, after the request.
internal static class Waiting
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    public static async Task UntilAsync(Func<Task<bool>> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(Patience);
        while (!await condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
        }
    }

    public static Task UntilAsync(Func<bool> condition) => UntilAsync(() => Task.FromResult(condition()));
}
