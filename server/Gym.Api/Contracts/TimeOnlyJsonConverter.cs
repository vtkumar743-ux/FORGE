using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gym.Api.Contracts;

/// <summary>
/// Reads a <see cref="TimeOnly"/> from "HH:mm" as well as "HH:mm:ss".
///
/// The API's own responses render wall-clock times as "06:00" and an HTML
/// <c>&lt;input type="time"&gt;</c> produces the same, but the built-in converter only accepts
/// the seconds form — so without this the timetable builder cannot post back a slot it was
/// just shown. An API should always be able to read what it writes.
/// </summary>
public class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    private static readonly string[] Formats = { "HH:mm:ss.FFFFFFF", "HH:mm:ss", "HH:mm" };

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();
        if (string.IsNullOrWhiteSpace(raw))
            throw new JsonException("Expected a time of day such as \"06:30\".");

        if (TimeOnly.TryParseExact(raw, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
            return value;

        // Last resort so an ISO instant or a localised string still lands somewhere sensible.
        if (TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
            return value;

        throw new JsonException($"\"{raw}\" is not a time of day. Use \"HH:mm\" or \"HH:mm:ss\".");
    }

    /// <summary>Writes the seconds form, which every client parser accepts.</summary>
    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
}
