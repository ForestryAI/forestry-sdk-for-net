using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Forestry.Serialization;
using Forestry.Serialization.JsonLines;
using Forestry.Serialization.Xml;

namespace Forestry.StanForD.Tests.Fixtures
{
    /// <summary>
    /// The round trip of serialization spec 005 US5 (tasks.md T052, C6): a Harvesting document read into
    /// <see cref="Stem"/> and <see cref="Log"/> telemetries, each type written as JSON Lines to its own output,
    /// each line parsed back and compared with its telemetry, member by member
    /// </summary>
    /// <remarks>
    /// The members compared are the ones a type definition maps: public instance properties with a public
    /// getter and setter and no index parameters (the core's type definitions are internal to it). Strings,
    /// booleans and numbers compare by value (<see cref="decimal"/> with every digit); dates through their
    /// JSON strings; enums by their declared text (<see cref="EnumMemberNameAttribute"/>) or C# name.
    /// </remarks>
    public static class RoundTrip
    {
        /// <summary>
        /// One output per telemetry type, as JSON Lines
        /// </summary>
        public sealed record Result(IReadOnlyList<Stem> Stems, string[] StemLines, IReadOnlyList<Log> Logs, string[] LogLines)
        {
            /// <summary>
            /// Every difference between a telemetry and its line, e.g. <c>Log 3: LogVolume 0.214 != 0.21</c>
            /// </summary>
            public IReadOnlyList<string> Differences()
            {
                List<string> differences = [];

                if (Stems.Count != StemLines.Length)
                {
                    differences.Add($"{Stems.Count} stems but {StemLines.Length} Stem lines");
                }

                if (Logs.Count != LogLines.Length)
                {
                    differences.Add($"{Logs.Count} logs but {LogLines.Length} Log lines");
                }

                for (int index = 0; index < Math.Min(Stems.Count, StemLines.Length); index++)
                {
                    Compare($"Stem {index}", Stems[index], StemLines[index], differences);
                }

                for (int index = 0; index < Math.Min(Logs.Count, LogLines.Length); index++)
                {
                    Compare($"Log {index}", Logs[index], LogLines[index], differences);
                }

                return differences;
            }
        }

        /// <summary>
        /// Read <paramref name="xml"/>, write its stems and logs as JSON Lines, one output each
        /// </summary>
        public static Result Of(string xml)
        {
            List<Telemetry> telemetries = TelemetrySerialization.Deserialize(xml, new DeserializeXmlOptions().AddHarvesting()).ToList();
            List<Stem> stems = telemetries.OfType<Stem>().ToList();
            List<Log> logs = telemetries.OfType<Log>().ToList();

            SerializeJsonLinesOptions options = new SerializeJsonLinesOptions().AddHarvesting();
            return new Result(stems, Write(stems, options), logs, Write(logs, options));
        }

        private static string[] Write<TTelemetry>(IReadOnlyList<TTelemetry> telemetries, SerializeJsonLinesOptions options) where TTelemetry : Telemetry
        {
            using MemoryStream memory = new();

            foreach (TTelemetry telemetry in telemetries)
            {
                TelemetrySerialization.Serialize(telemetry, memory, options);
            }

            string text = Encoding.UTF8.GetString(memory.ToArray());
            return text.Length == 0 ? [] : text[..^1].Split('\n');
        }

        /// <summary>
        /// The members a type definition maps
        /// </summary>
        private static IEnumerable<PropertyInfo> Members(Type type) =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetMethod is { IsPublic: true } && property.SetMethod is { IsPublic: true } && property.GetIndexParameters().Length == 0);

        private static void Compare(string which, Telemetry telemetry, string line, List<string> differences)
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            HashSet<string> written = [.. root.EnumerateObject().Select(property => property.Name)];

            foreach (PropertyInfo member in Members(telemetry.GetType()))
            {
                if (!written.Remove(member.Name))
                {
                    differences.Add($"{which}: {member.Name} missing");
                    continue;
                }

                if (Difference(member.GetValue(telemetry), member.PropertyType, root.GetProperty(member.Name)) is { } difference)
                {
                    differences.Add($"{which}: {member.Name} {difference}");
                }
            }

            differences.AddRange(written.Select(name => $"{which}: {name} written but not a member"));
        }

        /// <summary>
        /// How <paramref name="json"/> differs from <paramref name="value"/>, or null when it does not
        /// </summary>
        private static string? Difference(object? value, Type memberType, JsonElement json)
        {
            if (value is null)
            {
                return json.ValueKind == JsonValueKind.Null ? null : $"null != {json.GetRawText()}";
            }

            Type type = Nullable.GetUnderlyingType(memberType) ?? memberType;
            object? read;

            try
            {
                read = type switch
                {
                    _ when type == typeof(string) => json.GetString(),
                    _ when type == typeof(bool) => json.GetBoolean(),
                    _ when type == typeof(int) => json.GetInt32(),
                    _ when type == typeof(long) => json.GetInt64(),
                    _ when type == typeof(double) => json.GetDouble(),
                    _ when type == typeof(decimal) => json.GetDecimal(),
                    _ when type == typeof(Guid) => json.GetGuid(),
                    _ when type == typeof(DateTime) => json.GetDateTime(),
                    _ when type == typeof(DateTimeOffset) => json.GetDateTimeOffset(),
                    _ when type == typeof(DateOnly) => DateOnly.Parse(json.GetString()!, CultureInfo.InvariantCulture),
                    _ when type == typeof(TimeOnly) => TimeOnly.Parse(json.GetString()!, CultureInfo.InvariantCulture),
                    _ when type == typeof(TimeSpan) => TimeSpan.Parse(json.GetString()!, CultureInfo.InvariantCulture),
                    _ when type.IsEnum => json.GetString(),
                    _ => json.GetRawText()
                };
            }
            catch (Exception exception) when (exception is InvalidOperationException or FormatException)
            {
                return $"{value} != {json.GetRawText()} ({exception.GetType().Name})";
            }

            object expected = type.IsEnum ? EnumText(type, value) : value;
            bool equal = expected is DateTimeOffset offset && read is DateTimeOffset readOffset
                ? offset.EqualsExact(readOffset)
                : Equals(expected, read);

            return equal ? null : $"{expected} != {json.GetRawText()}";
        }

        /// <summary>
        /// An enum value's declared StanForD text, or its C# name
        /// </summary>
        private static string EnumText(Type type, object value)
        {
            string name = Enum.GetName(type, value) ?? value.ToString()!;
            return type.GetField(name)?.GetCustomAttribute<EnumMemberNameAttribute>()?.Name ?? name;
        }
    }
}
