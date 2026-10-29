using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Notifications.Api.Authentication;
using Notifications.Application.Notifications;
using Notifications.Application.Notifications.GetNotificationReaderAccess;
using Notifications.Application.Notifications.GetNotificationReader;
using SharedKernel;
using Wolverine;

namespace Notifications.Api.Me.Notifications;

/// <summary>The signed-in user's own notifications: the live stream of their signals (U6).</summary>
public static class MyNotificationEndpoints
{
    public static void Map(RouteGroupBuilder me) =>
        me.MapGet("/notifications/stream", StreamNotifications)
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    // U6: server-sent events of the user's live signals, one event "notifications" each, whose data names only the bell that changed. A
    // comment line goes out every KeepAliveInterval, and the stream closes after its Lifetime, so the client opens a new one with a fresh
    // token. Before each send it asks again whether the user may read, and closes for a deleted or suspended user. The stream is written
    // here, not with TypedResults.ServerSentEvents, because .NET's SseItem writes no comment line.
    private static async Task StreamNotifications(
        HttpContext http,
        IMessageBus bus,
        ILiveChannel live,
        IOptions<LiveStreamOptions> options,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var actorId = http.User.Id();
        var reader = await bus.InvokeAsync<NotificationReader>(new GetNotificationReaderQuery(actorId), cancellationToken);

        // The subscription starts before the answer does, so a signal that comes once the client has the answer's headers reaches it.
        using var ends = new CancellationTokenSource(options.Value.Lifetime, time);
        using var open = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ends.Token);
        var signals = reader.UserId is { } userId ? live.SubscribeAsync(userId, open.Token) : NoSignals(open.Token);
        await using var next = signals.GetAsyncEnumerator(open.Token);
        var signal = next.MoveNextAsync().AsTask();

        http.Response.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        await http.Response.Body.FlushAsync(cancellationToken);
        try
        {
            while (!open.IsCancellationRequested)
            {
                var keepAlive = Task.Delay(options.Value.KeepAliveInterval, time, open.Token);
                var first = await Task.WhenAny(signal, keepAlive);
                if (open.IsCancellationRequested
                    || !(await bus.InvokeAsync<Result>(new GetNotificationReaderAccessQuery(actorId), open.Token)).IsSuccess
                    || (first == signal && !await signal))
                {
                    break;
                }

                var text = first == signal ? $"event: {next.Current.Name}\ndata: {next.Current.Data}\n\n" : ": keep-alive\n\n";
                await http.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(text), open.Token);
                await http.Response.Body.FlushAsync(open.Token);
                signal = first == signal ? next.MoveNextAsync().AsTask() : signal;
            }
        }
        catch (OperationCanceledException) when (open.IsCancellationRequested)
        {
            // The lifetime ended, or the client left: the stream ends without an error.
        }
        finally
        {
            // A subscription is disposed only once its pending wait has ended.
            await open.CancelAsync();
            await signal.ContinueWith(_ => { }, TaskScheduler.Default);
        }
    }

    // A person who is not a user yet has no notification, and gets the comment lines only.
    private static async IAsyncEnumerable<LiveEvent> NoSignals([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith(_ => { }, TaskScheduler.Default);
        yield break;
    }
}
