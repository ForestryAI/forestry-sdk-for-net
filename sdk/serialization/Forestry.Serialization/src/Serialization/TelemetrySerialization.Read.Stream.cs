using System.IO.Pipelines;

namespace Forestry.Serialization
{
    public static partial class TelemetrySerialization
    {
        /// <summary>
        /// Telemetries from a document read from a stream, lazily and asynchronously, through a
        /// <see cref="PipeReader"/>. As <see cref="DeserializeAsync(PipeReader, DeserializeOptions, CancellationToken)"/>
        /// </summary>
        /// <remarks>
        /// The stream stays the caller's and is left open. The <see cref="PipeReader"/> over it is created
        /// here, never seen by the caller, so it is this deserialization's: disposing the enumerator
        /// completes it, which returns its pooled buffers (<c>leaveOpen</c> keeps that from disposing the
        /// stream).
        /// </remarks>
        /// <param name="utf8">UTF-8 bytes of the document; left open</param>
        /// <param name="options">A format's options with the telemetry types added</param>
        /// <param name="cancellationToken">Cancels a move waiting for bytes</param>
        /// <returns>The telemetries in document order, enumerable once</returns>
        public static IAsyncEnumerable<Telemetry> DeserializeAsync(Stream utf8, DeserializeOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8);
            ArgumentNullException.ThrowIfNull(options);

            options.MakeReadOnly();
            PipeReader pipe = PipeReader.Create(utf8, new StreamPipeReaderOptions(leaveOpen: true));

            return new TelemetryAsyncEnumerable(pipe, options, cancellationToken, isPipeOwned: true);
        }
    }
}
