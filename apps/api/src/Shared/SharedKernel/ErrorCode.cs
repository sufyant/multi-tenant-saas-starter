using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharedKernel;

/// <summary>
/// The code of an error answer, <c>area.reason</c> in lower snake case (K5). A screen writes its own text for each code. Each owner
/// declares its codes in one static class whose name ends with <c>ErrorCodes</c>: <see cref="PagingErrorCodes"/> here, the host's
/// <c>HostErrorCodes</c>, and each module's in its Domain project, such as <c>ControlPlaneErrorCodes</c>. The OpenAPI document joins
/// them into one enum. Only such a class calls <see cref="Declare"/> (ErrorCodeTests). It is a class with no public constructor, so no
/// code exists that nobody declared, not even a default one.
/// </summary>
[JsonConverter(typeof(ErrorCodeJsonConverter))]
public sealed record ErrorCode
{
    private ErrorCode(string value) => Value = value;

    public string Value { get; }

    /// <summary>Declares a code, as a static readonly field of an <c>...ErrorCodes</c> class.</summary>
    public static ErrorCode Declare(string value) => new(value);

    public override string ToString() => Value;

    // A saved idempotent result keeps the code as its text, so it replays to the same answer (I5).
    private sealed class ErrorCodeJsonConverter : JsonConverter<ErrorCode>
    {
        public override ErrorCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new(reader.GetString()!);

        public override void Write(Utf8JsonWriter writer, ErrorCode value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
    }
}
