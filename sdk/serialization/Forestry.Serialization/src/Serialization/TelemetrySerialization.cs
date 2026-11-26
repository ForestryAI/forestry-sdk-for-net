namespace Forestry.Serialization
{
    /// <summary>
    /// Entry points in both directions (research R3; spec 004 R44): deserialize a document into telemetries
    /// of the telemetry types added to a format's <see cref="DeserializeOptions"/> (reading:
    /// <c>TelemetrySerialization.Read.*.cs</c>, by source: memory, string, stream, pipe), and serialize
    /// telemetries through a format's <see cref="SerializeOptions"/> (writing:
    /// <c>TelemetrySerialization.Write.*.cs</c>, by target: stream, pipe). The format is chosen by the
    /// options' type
    /// </summary>
    public static partial class TelemetrySerialization
    {
    }
}
