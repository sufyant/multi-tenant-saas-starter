namespace SharedKernel;

/// <summary>
/// One topic of a person's data export: its name, in lower case and kebab case, which names its file (<c>{name}.json</c>), and its content,
/// which the export writes as JSON.
/// </summary>
public sealed record PersonalDataPart(string Name, object Content);
