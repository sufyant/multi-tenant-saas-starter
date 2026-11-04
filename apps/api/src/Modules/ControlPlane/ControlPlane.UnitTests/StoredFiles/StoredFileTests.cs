using ControlPlane.Domain.StoredFiles;
using SharedKernel;

namespace ControlPlane.UnitTests.StoredFiles;

// Section 7, "File storage": an export is prepared, then ready for 7 days, then expired; one still in preparation at its timeout failed.
public sealed class StoredFileTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PrepareDataExport_ForAUser_KeepsOnlyTheKeyOfItsFile()
    {
        var (id, userId) = (Guid.CreateVersion7(), Guid.CreateVersion7());

        var export = StoredFile.PrepareDataExport(id, userId, Now);

        (export.Status, export.ObjectKey, export.FileName).ShouldBe((StoredFileStatus.Preparing, $"exports/users/{userId}/{id}.zip", "data-export-2026-11-04.zip"));
    }

    [Fact]
    public void ReadyDataExport_InPreparation_CanBeDownloadedForSevenDays()
    {
        var export = StoredFile.PrepareDataExport(Guid.CreateVersion7(), Guid.CreateVersion7(), Now);

        var ready = export.Ready(1024, Now.AddMinutes(1));

        (ready, export.Status, export.SizeBytes, export.ExpiresAt).ShouldBe((StateChange.Changed, StoredFileStatus.Ready, 1024L, Now.AddMinutes(1).AddDays(7)));
    }

    // A late file of an export that failed by its timeout changes nothing; nor does a second Ready.
    [Theory]
    [InlineData("Failed")]
    [InlineData("Ready")]
    public void ReadyDataExport_NotInPreparation_IsUnchanged(string status)
    {
        var export = InStatus(Enum.Parse<StoredFileStatus>(status));

        export.Ready(1024, Now).ShouldBe(StateChange.Unchanged);
        export.Status.ToString().ShouldBe(status);
    }

    [Fact]
    public void FailDataExport_Ready_IsUnchanged()
    {
        var export = InStatus(StoredFileStatus.Ready);

        export.Fail().ShouldBe(StateChange.Unchanged);
    }

    [Fact]
    public void ExpireDataExport_Ready_KeepsItsExpiry()
    {
        var export = InStatus(StoredFileStatus.Ready);
        var expiresAt = export.ExpiresAt;

        export.Expire().ShouldBe(StateChange.Changed);
        (export.Status, export.ExpiresAt).ShouldBe((StoredFileStatus.Expired, expiresAt));
    }

    [Fact]
    public void ExpireDataExport_InPreparation_IsUnchanged()
    {
        var export = InStatus(StoredFileStatus.Preparing);

        export.Expire().ShouldBe(StateChange.Unchanged);
    }

    private static StoredFile InStatus(StoredFileStatus status)
    {
        var export = StoredFile.PrepareDataExport(Guid.CreateVersion7(), Guid.CreateVersion7(), Now);
        _ = status switch
        {
            StoredFileStatus.Ready => export.Ready(1, Now),
            StoredFileStatus.Failed => export.Fail(),
            _ => StateChange.Unchanged,
        };
        return export;
    }
}
