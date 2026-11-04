using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradeFolio.Contracts;

/// <summary>
/// Writes every timestamp as UTC with a trailing Z (2026-10-16T18:30:00Z), as in the spec, whatever offset
/// the value had. The fraction is only written when it is not zero. Reading accepts any ISO 8601 offset.
/// </summary>
public sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String ||
            !DateTimeOffset.TryParse(
                reader.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var value))
        {
            throw new JsonException("Expected an ISO 8601 date-time string.");
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.UtcDateTime.ToString(Format, CultureInfo.InvariantCulture));
}