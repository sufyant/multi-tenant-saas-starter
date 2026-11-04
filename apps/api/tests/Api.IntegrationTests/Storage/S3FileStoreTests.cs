using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Tenancy;

namespace Api.IntegrationTests.Storage;

// Section 7, "File storage": the host's file store on the S3 API, as the host registers it, against a real S3 server (ObjectStorage). A
// module names a file by its key only; the store knows the bucket.
public sealed class S3FileStoreTests(Database database) : IAsyncLifetime
{
    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private IFileStore Files => _api.Services.GetRequiredService<IFileStore>();

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task SaveFile_ThenReadIt_GivesTheContentBack()
    {
        var key = FileKeys.UserExport(Guid.CreateVersion7(), Guid.CreateVersion7());

        await Files.SaveAsync(key, new MemoryStream(Encoding.UTF8.GetBytes("hello")), "application/zip", Cancellation);

        using var read = new StreamReader(await Files.ReadAsync(key, Cancellation));
        (await read.ReadToEndAsync(Cancellation)).ShouldBe("hello");
    }

    // A deletion of a user or a tenant deletes its whole prefix, and nothing of another user's or tenant's (GDPR, Article 17).
    [Fact]
    public async Task DeletePrefix_FilesOfTwoUsers_DeletesOnlyTheFilesUnderThePrefix()
    {
        var (user, other) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        await ObjectStorage.PutAsync(FileKeys.UserExport(user, Guid.CreateVersion7()), "a");
        await ObjectStorage.PutAsync(FileKeys.UserExport(user, Guid.CreateVersion7()), "b");
        await ObjectStorage.PutAsync(FileKeys.UserExport(other, Guid.CreateVersion7()), "c");

        await Files.DeletePrefixAsync(FileKeys.User(user), Cancellation);

        (await ObjectStorage.KeysAsync(FileKeys.User(user))).ShouldBeEmpty();
        (await ObjectStorage.KeysAsync(FileKeys.User(other))).Count.ShouldBe(1);
    }

    // A retried step finds nothing to delete, and is done.
    [Fact]
    public async Task DeleteFile_AKeyWithoutAFile_IsDone()
    {
        var delete = () => Files.DeleteAsync(FileKeys.UserExport(Guid.CreateVersion7(), Guid.CreateVersion7()), Cancellation);
        var deletePrefix = () => Files.DeletePrefixAsync(FileKeys.Tenant(Guid.CreateVersion7()), Cancellation);

        await delete.ShouldNotThrowAsync();
        await deletePrefix.ShouldNotThrowAsync();
    }

    // The link needs no sign-in of its own: the API checked the person before it made it. It downloads the file as an attachment with the
    // name the API gave it.
    [Fact]
    public async Task CreateDownloadLink_AFile_DownloadsItAsAnAttachment()
    {
        var key = FileKeys.UserExport(Guid.CreateVersion7(), Guid.CreateVersion7());
        await Files.SaveAsync(key, new MemoryStream(Encoding.UTF8.GetBytes("zip")), "application/zip", Cancellation);

        var link = await Files.CreateDownloadLinkAsync(key, "data-export-2026-11-04.zip", Cancellation);
        using var http = new HttpClient();
        using var download = await http.GetAsync(link, Cancellation);

        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await download.Content.ReadAsStringAsync(Cancellation)).ShouldBe("zip");
        download.Content.Headers.ContentDisposition!.ToString().ShouldBe("attachment; filename=\"data-export-2026-11-04.zip\"");
    }
}
