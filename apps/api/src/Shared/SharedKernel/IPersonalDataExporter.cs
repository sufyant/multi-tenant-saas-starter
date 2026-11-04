namespace SharedKernel;

/// <summary>
/// A module's part of a person's data export (GDPR, Articles 15 and 20): the data the module keeps about the person, and no data of an
/// organisation, which holds other people's personal data. Each module that keeps such data registers one in its Infrastructure entry
/// point, and the export collects every one, so a new module joins the export with no change elsewhere
/// (<c>docs/flows/personal-data-export.md</c>). An exporter reads in a transaction of its own module.
/// </summary>
public interface IPersonalDataExporter
{
    /// <summary>The module's parts of the person's export, each a file of JSON in the export.</summary>
    Task<IReadOnlyList<PersonalDataPart>> ExportAsync(Guid userId, CancellationToken cancellationToken);
}
