namespace Forestry.Serialization
{
    public static partial class TelemetrySerialization
    {
        /// <summary>
        /// Telemetries from a document as UTF-8 bytes in memory, lazily and synchronously. A UTF-8 byte order
        /// mark is skipped unless the format's reader reads it (research R18); the options are locked and their definitions built before this returns, so
        /// a telemetry type that cannot be defined throws here (FR-008)
        /// </summary>
        /// <param name="utf8"></param>
        /// <param name="options">A format's options with the telemetry types added</param>
        /// <returns>The telemetries in document order, enumerable once</returns>
        public static IEnumerable<Telemetry> Deserialize(ReadOnlyMemory<byte> utf8, DeserializeOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            options.MakeReadOnly();

            if (options.SkipsByteOrderMark && utf8.Span.StartsWith(Constants.Utf8Bom))
            {
                utf8 = utf8[Constants.Utf8Bom.Length..];
            }

            return new TelemetryEnumerable(utf8, options);
        }
    }
}
