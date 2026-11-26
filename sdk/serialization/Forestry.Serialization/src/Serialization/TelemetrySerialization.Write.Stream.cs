using Forestry.Serialization.Definitions;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization
{
    public static partial class TelemetrySerialization
    {
        /// <summary>
        /// Write one telemetry to <paramref name="utf8"/> through the format's writer (spec 004 US2). The
        /// stream stays the caller's: it is written and flushed, never closed or disposed
        /// </summary>
        /// <param name="telemetry">A telemetry of a type added to <paramref name="options"/></param>
        /// <param name="utf8">Where the UTF-8 bytes go; left open</param>
        /// <param name="options">A format's serialize options; locked on first use</param>
        /// <exception cref="ArgumentException">The stream cannot be written to</exception>
        /// <exception cref="InvalidOperationException">The telemetry's type was not added, or a member cannot
        /// be written; nothing is written</exception>
        public static void Serialize(Telemetry telemetry, Stream utf8, SerializeOptions options)
        {
            ArgumentNullException.ThrowIfNull(telemetry);
            ArgumentNullException.ThrowIfNull(utf8);
            ArgumentNullException.ThrowIfNull(options);
            ThrowWhenCannotWrite(utf8);

            options.MakeReadOnly();
            TypeDefinition definition = options.ThrowingGetTypeDefinition(telemetry.GetType());

            using PooledBufferWriter buffer = new();
            TelemetryWriter writer = WriterFor(options, buffer);

            WriteTelemetry(telemetry, definition, writer);
            utf8.Write(buffer.WrittenSpan);
            utf8.Flush();
        }

        /// <summary>
        /// Write telemetries to <paramref name="utf8"/>, taking them one at a time, so that serializing pairs
        /// with <c>Deserialize</c> without gathering telemetries first (FR-011). The stream stays the caller's
        /// </summary>
        /// <param name="telemetries">Telemetries of types added to <paramref name="options"/></param>
        /// <param name="utf8">Where the UTF-8 bytes go; left open</param>
        /// <param name="options">A format's serialize options; locked on first use</param>
        /// <exception cref="ArgumentException">The stream cannot be written to</exception>
        /// <exception cref="InvalidOperationException">A telemetry's type was not added, or a member cannot be
        /// written</exception>
        public static void Serialize(IEnumerable<Telemetry> telemetries, Stream utf8, SerializeOptions options)
        {
            ArgumentNullException.ThrowIfNull(telemetries);
            ArgumentNullException.ThrowIfNull(utf8);
            ArgumentNullException.ThrowIfNull(options);
            ThrowWhenCannotWrite(utf8);

            options.MakeReadOnly();

            using PooledBufferWriter buffer = new();
            TelemetryWriter writer = WriterFor(options, buffer);

            foreach (Telemetry telemetry in telemetries)
            {
                ArgumentNullException.ThrowIfNull(telemetry, nameof(telemetries));
                WriteTelemetry(telemetry, options.ThrowingGetTypeDefinition(telemetry.GetType()), writer);

                if (buffer.WrittenCount >= FlushThreshold)
                {
                    utf8.Write(buffer.WrittenSpan);
                    buffer.Clear();
                }
            }

            if (buffer.WrittenCount > 0)
            {
                utf8.Write(buffer.WrittenSpan);
            }

            utf8.Flush();
        }

        /// <summary>
        /// Write one telemetry to <paramref name="utf8"/> asynchronously, through the stream's
        /// <c>WriteAsync</c> and <c>FlushAsync</c> only (FR-013). The stream stays the caller's
        /// </summary>
        /// <remarks>
        /// A <see cref="ValueTask"/>, since this is called once per telemetry and most writes complete at once
        /// (research R47): await it once, never concurrently, and never read its result before it completes.
        /// </remarks>
        /// <param name="telemetry">A telemetry of a type added to <paramref name="options"/></param>
        /// <param name="utf8">Where the UTF-8 bytes go; left open</param>
        /// <param name="options">A format's serialize options; locked on first use</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async ValueTask SerializeAsync(Telemetry telemetry, Stream utf8, SerializeOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(telemetry);
            ArgumentNullException.ThrowIfNull(utf8);
            ArgumentNullException.ThrowIfNull(options);
            ThrowWhenCannotWrite(utf8);
            cancellationToken.ThrowIfCancellationRequested();

            options.MakeReadOnly();
            TypeDefinition definition = options.ThrowingGetTypeDefinition(telemetry.GetType());

            using PooledBufferWriter buffer = new();
            TelemetryWriter writer = WriterFor(options, buffer);

            WriteTelemetry(telemetry, definition, writer);
            await utf8.WriteAsync(buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
            await utf8.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Write telemetries to <paramref name="utf8"/> asynchronously, taking them one at a time, so that
        /// serializing pairs with <c>DeserializeAsync</c> (FR-011, FR-013). Cancellation is checked before each
        /// telemetry. The stream stays the caller's
        /// </summary>
        /// <param name="telemetries">Telemetries of types added to <paramref name="options"/></param>
        /// <param name="utf8">Where the UTF-8 bytes go; left open</param>
        /// <param name="options">A format's serialize options; locked on first use</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task SerializeAsync(IAsyncEnumerable<Telemetry> telemetries, Stream utf8, SerializeOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(telemetries);
            ArgumentNullException.ThrowIfNull(utf8);
            ArgumentNullException.ThrowIfNull(options);
            ThrowWhenCannotWrite(utf8);

            options.MakeReadOnly();

            using PooledBufferWriter buffer = new();
            TelemetryWriter writer = WriterFor(options, buffer);

            await foreach (Telemetry telemetry in telemetries.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentNullException.ThrowIfNull(telemetry, nameof(telemetries));
                WriteTelemetry(telemetry, options.ThrowingGetTypeDefinition(telemetry.GetType()), writer);

                if (buffer.WrittenCount >= FlushThreshold)
                {
                    await utf8.WriteAsync(buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
                    buffer.Clear();
                }
            }

            if (buffer.WrittenCount > 0)
            {
                await utf8.WriteAsync(buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
            }

            await utf8.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
