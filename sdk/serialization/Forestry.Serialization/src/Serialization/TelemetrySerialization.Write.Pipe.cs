using System.IO.Pipelines;
using Forestry.Serialization.Definitions;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization
{
    public static partial class TelemetrySerialization
    {
        /// <summary>
        /// Write one telemetry into <paramref name="utf8"/>'s own buffers (no copy) and flush it (spec 004
        /// research R48). The pipe stays the caller's: it is never completed
        /// </summary>
        /// <remarks>
        /// A <see cref="ValueTask"/>, as for a stream (research R47).
        /// </remarks>
        /// <param name="telemetry">A telemetry of a type added to <paramref name="options"/></param>
        /// <param name="utf8">Where the UTF-8 bytes go; never completed</param>
        /// <param name="options">A format's serialize options; locked on first use</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async ValueTask SerializeAsync(Telemetry telemetry, PipeWriter utf8, SerializeOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(telemetry);
            ArgumentNullException.ThrowIfNull(utf8);
            ArgumentNullException.ThrowIfNull(options);
            cancellationToken.ThrowIfCancellationRequested();

            options.MakeReadOnly();
            TypeDefinition definition = options.ThrowingGetTypeDefinition(telemetry.GetType());
            TelemetryWriter writer = WriterFor(options, utf8);

            WriteTelemetry(telemetry, definition, writer);
            TelemetryWritten(utf8);
            await FlushAsync(utf8, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Write telemetries into <paramref name="utf8"/>'s own buffers, taking them one at a time, flushing
        /// as unflushed bytes pass a threshold and at the end. The pipe stays the caller's
        /// </summary>
        /// <param name="telemetries">Telemetries of types added to <paramref name="options"/></param>
        /// <param name="utf8">Where the UTF-8 bytes go; never completed</param>
        /// <param name="options">A format's serialize options; locked on first use</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task SerializeAsync(IAsyncEnumerable<Telemetry> telemetries, PipeWriter utf8, SerializeOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(telemetries);
            ArgumentNullException.ThrowIfNull(utf8);
            ArgumentNullException.ThrowIfNull(options);

            options.MakeReadOnly();
            TelemetryWriter writer = WriterFor(options, utf8);

            await foreach (Telemetry telemetry in telemetries.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentNullException.ThrowIfNull(telemetry, nameof(telemetries));
                WriteTelemetry(telemetry, options.ThrowingGetTypeDefinition(telemetry.GetType()), writer);
                TelemetryWritten(utf8);

                if (!utf8.CanGetUnflushedBytes || utf8.UnflushedBytes >= FlushThreshold)
                {
                    await FlushAsync(utf8, cancellationToken).ConfigureAwait(false);
                }
            }

            await FlushAsync(utf8, cancellationToken).ConfigureAwait(false);
        }
    }
}
