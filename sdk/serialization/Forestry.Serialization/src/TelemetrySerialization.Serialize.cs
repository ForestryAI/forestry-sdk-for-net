using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using Forestry.Serialization.Definitions;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization
{
    public static partial class TelemetrySerialization
    {
        /// <summary>
        /// Bytes gathered before they are handed to the target when many telemetries are serialized in one
        /// call (spec 004 research R48)
        /// </summary>
        private const int FlushThreshold = 16 * 1024;

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

                if (!utf8.CanGetUnflushedBytes || utf8.UnflushedBytes >= FlushThreshold)
                {
                    await FlushAsync(utf8, cancellationToken).ConfigureAwait(false);
                }
            }

            await FlushAsync(utf8, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// The serializer's walk (spec 004 research R49): the writer frames the telemetry, each member writes
        /// itself through its compiled getter and resolved converter
        /// </summary>
        /// <param name="telemetry"></param>
        /// <param name="definition"></param>
        /// <param name="writer"></param>
        internal static void WriteTelemetry(Telemetry telemetry, TypeDefinition definition, TelemetryWriter writer)
        {
            writer.WriteStartTelemetry(definition);

            foreach (PropertyDefinition property in definition.Properties)
            {
                property.WriteMember(telemetry, writer);
            }

            writer.WriteEndTelemetry();
        }

        /// <summary>
        /// The format's writer for one call, writing to <paramref name="output"/>
        /// </summary>
        /// <param name="options"></param>
        /// <param name="output"></param>
        /// <returns></returns>
        private static TelemetryWriter WriterFor(SerializeOptions options, System.Buffers.IBufferWriter<byte> output)
        {
            TelemetryWriter writer = options.CreateWriterForCall();
            writer.Reset(output);
            return writer;
        }

        /// <summary>
        /// Flush a pipe, honoring a cancelled flush
        /// </summary>
        /// <param name="utf8"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private static async ValueTask FlushAsync(PipeWriter utf8, CancellationToken cancellationToken)
        {
            FlushResult result = await utf8.FlushAsync(cancellationToken).ConfigureAwait(false);

            if (result.IsCanceled)
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }

        private static void ThrowWhenCannotWrite(Stream utf8, [CallerArgumentExpression(nameof(utf8))] string? name = null)
        {
            if (!utf8.CanWrite)
            {
                throw new ArgumentException(Formatting.StreamCannotWrite, name);
            }
        }
    }
}
