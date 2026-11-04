using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace ControlPlane.Application.DataExports;

/// <summary>
/// The file of a person's data export: a ZIP with one JSON file for each topic, in the order of their names, and a plain README.txt that
/// says what each file holds and where the identity provider keeps the rest (GDPR, Articles 15 and 20: a structured, commonly used and
/// machine-readable format).
/// </summary>
internal static class DataExportArchive
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static MemoryStream Write(IReadOnlyList<SharedKernel.PersonalDataPart> parts, DateTimeOffset now)
    {
        var content = new MemoryStream();
        using (var zip = new ZipArchive(content, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "README.txt", Readme(parts, now));
            foreach (var part in parts.OrderBy(part => part.Name, StringComparer.Ordinal))
            {
                WriteEntry(zip, $"{part.Name}.json", JsonSerializer.Serialize(part.Content, part.Content.GetType(), Json));
            }
        }

        content.Position = 0;
        return content;
    }

    private static void WriteEntry(ZipArchive zip, string name, string text)
    {
        using var entry = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
        entry.Write(Encoding.UTF8.GetBytes(text));
    }

    private static string Readme(IReadOnlyList<SharedKernel.PersonalDataPart> parts, DateTimeOffset now) =>
        string.Join(
            "\n",
            [
                "Your data",
                string.Empty,
                $"This file holds the data we keep about you, as it was on {now.UtcDateTime:yyyy-MM-dd HH:mm} UTC.",
                "Each JSON file is one topic:",
                .. parts.OrderBy(part => part.Name, StringComparer.Ordinal).Select(part => $"- {part.Name}.json"),
                string.Empty,
                "It holds data about you only. The data of your organisations holds other people's data too, so it is not here.",
                string.Empty,
                "Your sign-in methods, your sessions and your linked Google or Microsoft accounts are kept by our identity provider, Clerk.",
                "You see them in Account and security.",
                string.Empty,
            ]);
}
