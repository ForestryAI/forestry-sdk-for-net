using System.Collections;
using System.IO.Pipelines;
using Forestry.Serialization.Reading;

namespace Forestry.Serialization
{
    /// <summary>
    /// Reading: the enumerables every <c>Deserialize</c> and <c>DeserializeAsync</c> entry point hands out
    /// </summary>
    public static partial class TelemetrySerialization
    {
        /// <summary>
        /// Telemetries from a document in memory, enumerated once: the format's telemetry enumerator is created when
        /// enumeration starts, a second enumeration throws (research R2)
        /// </summary>
        private sealed class TelemetryEnumerable(ReadOnlyMemory<byte> utf8, DeserializeOptions options) : IEnumerable<Telemetry>
        {
            private int _isEnumerated;

            public IEnumerator<Telemetry> GetEnumerator()
            {
                if (Interlocked.Exchange(ref _isEnumerated, 1) == 1)
                {
                    Throwing.WhenEnumeratedTwice();
                }

                return options.CreateTelemetryEnumerator(utf8);
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <summary>
        /// Telemetries from a pipe, enumerated once: the format's telemetry enumerator is created when enumeration
        /// starts, a second enumeration throws (research R2)
        /// </summary>
        /// <remarks>
        /// <para>
        /// Whoever creates the <see cref="PipeReader"/> completes it. <paramref name="isPipeOwned"/> is true
        /// only when an entry point created it (the <see cref="Stream"/> overload); a caller's pipe is never
        /// completed here.
        /// </para>
        /// <para>
        /// The format creates the <see cref="FormatTelemetryAsyncEnumerator"/> and does not know who owns the pipe, so the
        /// extra clean-up lives in <see cref="OwningTelemetryAsyncEnumerator"/>: completing an owned pipe, and
        /// disposing the source that links the entry point's and the enumeration's cancellation tokens.
        /// With neither to clean up, the format's enumerator is returned as it is.
        /// </para>
        /// </remarks>
        /// <param name="pipe"></param>
        /// <param name="options"></param>
        /// <param name="cancellationToken">The entry point's token</param>
        /// <param name="isPipeOwned">The entry point created <paramref name="pipe"/> and completes it</param>
        private sealed class TelemetryAsyncEnumerable(
            PipeReader pipe,
            DeserializeOptions options,
            CancellationToken cancellationToken,
            bool isPipeOwned) : IAsyncEnumerable<Telemetry>
        {
            private int _isEnumerated;

            public IAsyncEnumerator<Telemetry> GetAsyncEnumerator(CancellationToken enumerationToken = default)
            {
                if (Interlocked.Exchange(ref _isEnumerated, 1) == 1)
                {
                    Throwing.WhenEnumeratedTwice();
                }

                CancellationTokenSource? linked = cancellationToken.CanBeCanceled && enumerationToken.CanBeCanceled
                    ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, enumerationToken)
                    : null;
                CancellationToken token = linked?.Token ?? (enumerationToken.CanBeCanceled ? enumerationToken : cancellationToken);
                FormatTelemetryAsyncEnumerator telemetries;

                try
                {
                    telemetries = options.CreateTelemetryAsyncEnumerator(pipe, token);
                }
                catch
                {
                    // e.g. another enumeration is running on options with enumeration policies (R19):
                    // nothing was read, so what this call created is released here
                    linked?.Dispose();

                    if (isPipeOwned)
                    {
                        pipe.Complete();
                    }

                    throw;
                }

                return linked is null && !isPipeOwned ? telemetries : new OwningTelemetryAsyncEnumerator(telemetries, isPipeOwned ? pipe : null, linked);
            }
        }

        /// <summary>
        /// A telemetry enumerator that also completes the pipe the entry point created, and disposes the linked
        /// cancellation, after the format's enumerator has given back its buffer
        /// </summary>
        /// <param name="telemetries">The format's enumerator</param>
        /// <param name="pipe">The pipe to complete, or null when the caller owns it</param>
        /// <param name="linked">The linked cancellation source to dispose, or null</param>
        private sealed class OwningTelemetryAsyncEnumerator(
            FormatTelemetryAsyncEnumerator telemetries,
            PipeReader? pipe,
            CancellationTokenSource? linked) : IAsyncEnumerator<Telemetry>
        {
            public Telemetry Current => telemetries.Current;

            public ValueTask<bool> MoveNextAsync() => telemetries.MoveNextAsync();

            public async ValueTask DisposeAsync()
            {
                await telemetries.DisposeAsync().ConfigureAwait(false);

                if (pipe is not null)
                {
                    await pipe.CompleteAsync().ConfigureAwait(false);
                }

                linked?.Dispose();
            }
        }
    }
}
