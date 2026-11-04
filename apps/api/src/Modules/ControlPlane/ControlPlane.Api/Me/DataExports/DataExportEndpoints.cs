using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Statuses;
using ControlPlane.Application.DataExports;
using ControlPlane.Application.DataExports.DownloadDataExport;
using ControlPlane.Application.DataExports.GetDownloadLink;
using ControlPlane.Application.DataExports.GetMyDataExport;
using ControlPlane.Application.DataExports.RequestDataExport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Me.DataExports;

/// <summary>The signed-in user's exports of their own data (docs/flows/personal-data-export.md).</summary>
public static class DataExportEndpoints
{
    internal const string NoLocation =
        "Answers 201 with the export and without Location: the API reads the person's latest export, not one by its id, so the request's target URI identifies it (RFC 9110, section 15.3.2).";

    public static void Map(RouteGroupBuilder me)
    {
        me.MapPost("/data-exports", RequestDataExport)
            .WithMetadata(new IdempotencyKeyRequired())
            .WithDescription(NoLocation)
            .Produces<DataExportResponse>(StatusCodes.Status201Created);
        me.MapGet("/data-exports/latest", GetMyDataExport);
        me.MapPost("/data-exports/{exportId:guid}/download", DownloadDataExport);
    }

    // The request starts a process, so it takes an idempotency key (I1): a replay answers the same export, and a second request while one is
    // in preparation answers 409. The endpoint declares 201, so the host answers a new export with 201 (I5).
    private static async Task<Result<DataExportResponse>> RequestDataExport(
        ClaimsPrincipal user,
        IIdempotencyExecutor idempotency,
        CancellationToken cancellationToken) =>
        (await idempotency.ExecuteAsync<DataExportView>(new RequestDataExportCommand(user.Id()), null, cancellationToken)).Map(ToResponse);

    // Read without a tenant. A person who never asked for an export is not found: the screen offers "Export my data".
    private static async Task<Result<DataExportResponse>> GetMyDataExport(ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<DataExportView>>(new GetMyDataExportQuery(user.Id()), cancellationToken)).Map(ToResponse);

    // The API checks the signed-in person first, and records the download; then it makes the link, which works for five minutes and is
    // never stored. A download changes no state of the export and creates nothing, so it takes no idempotency key (I1).
    private static async Task<Result<DownloadLinkResponse>> DownloadDataExport(
        Guid exportId,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var download = await bus.InvokeAsync<Result<DataExportDownload>>(new DownloadDataExportCommand(user.Id(), exportId), cancellationToken);
        if (!download.IsSuccess)
        {
            return download.Error;
        }

        var link = await bus.InvokeAsync<DownloadLink>(new GetDownloadLinkQuery(download.Value.ObjectKey, download.Value.FileName), cancellationToken);
        return new DownloadLinkResponse(link.Url, link.ExpiresAt);
    }

    private static DataExportResponse ToResponse(DataExportView export) => new(
        export.Id,
        ResponseStatus.OfDataExport(export.Status),
        export.RequestedAt,
        export.ReadyAt,
        export.ExpiresAt,
        export.SizeBytes);
}
