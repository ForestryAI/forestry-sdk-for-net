using System.IO.Pipelines;

namespace Forestry.Serialization
{
    public static partial class TelemetrySerialization
    {
        /// <summary>
        /// Telemetries from a document read from a pipe, lazily and asynchronously. Each telemetry is handed out as soon
        /// as its end is read, without waiting for more input; a UTF-8 byte order mark is skipped unless the
        /// format's reader reads it (research R18). The
        /// options are locked and their definitions built before this returns, so a telemetry type that cannot
        /// be defined throws here (FR-008)
        /// </summary>
        /// <remarks>
        /// The pipe stays the caller's: disposing the enumerator gives back the buffer it holds but never
        /// completes the pipe, so the caller can read on after the document or complete it themselves.
        /// </remarks>
        /// <param name="utf8">UTF-8 bytes of the document; the caller completes it</param>
        /// <param name="options">A format's options with the telemetry types added</param>
        /// <param name="cancellationToken">Cancels a move waiting for bytes</param>
        /// <returns>The telemetries in document order, enumerable once</returns>
        public static IAsyncEnumerable<Telemetry> DeserializeAsync(PipeReader utf8, DeserializeOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8);
            ArgumentNullException.ThrowIfNull(options);

            options.MakeReadOnly();

            return new TelemetryAsyncEnumerable(utf8, options, cancellationToken, isPipeOwned: false);
        }
    }
}
