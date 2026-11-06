using System.IO.Pipelines;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize
{
    public static partial class Deserialization
    {
        /// <summary>
        /// Rows from a document read from a pipe, lazily and asynchronously. Each row is handed out as soon
        /// as its end is read, without waiting for more input; a UTF-8 byte order mark is skipped unless the
        /// media's reader reads it (research R18). The
        /// options are locked and their definitions built before this returns, so a row type that cannot
        /// be defined throws here (FR-008)
        /// </summary>
        /// <param name="utf8">UTF-8 bytes of the document; the caller completes it</param>
        /// <param name="options">A media's options with the row types added</param>
        /// <param name="cancellationToken">Cancels a move waiting for bytes</param>
        /// <returns>The rows in document order, enumerable once</returns>
        public static IAsyncEnumerable<Row> DeserializeAsync(PipeReader utf8, DeserializeOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8);
            ArgumentNullException.ThrowIfNull(options);

            options.MakeReadOnly();

            return new RowAsyncEnumerable(utf8, options, cancellationToken, isPipeOwned: false);
        }

        /// <summary>
        /// Rows from a document read from a stream, lazily and asynchronously, through a
        /// <see cref="PipeReader"/>. As <see cref="DeserializeAsync(PipeReader, DeserializeOptions, CancellationToken)"/>
        /// </summary>
        /// <param name="utf8">UTF-8 bytes of the document; left open</param>
        /// <param name="options">A media's options with the row types added</param>
        /// <param name="cancellationToken">Cancels a move waiting for bytes</param>
        /// <returns>The rows in document order, enumerable once</returns>
        public static IAsyncEnumerable<Row> DeserializeAsync(Stream utf8, DeserializeOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8);
            ArgumentNullException.ThrowIfNull(options);

            options.MakeReadOnly();
            PipeReader pipe = PipeReader.Create(utf8, new StreamPipeReaderOptions(leaveOpen: true));

            return new RowAsyncEnumerable(pipe, options, cancellationToken, isPipeOwned: true);
        }

        /// <summary>
        /// Rows from a pipe, enumerated once: the media's row enumerator is created when enumeration
        /// starts, a second enumeration throws (research R2)
        /// </summary>
        private sealed class RowAsyncEnumerable(
            PipeReader pipe,
            DeserializeOptions options,
            CancellationToken cancellationToken,
            bool isPipeOwned) : IAsyncEnumerable<Row>
        {
            private int _isEnumerated;

            public IAsyncEnumerator<Row> GetAsyncEnumerator(CancellationToken enumerationToken = default)
            {
                if (Interlocked.Exchange(ref _isEnumerated, 1) == 1)
                {
                    Throwing.WhenEnumeratedTwice();
                }

                CancellationTokenSource? linked = cancellationToken.CanBeCanceled && enumerationToken.CanBeCanceled
                    ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, enumerationToken)
                    : null;
                CancellationToken token = linked?.Token ?? (enumerationToken.CanBeCanceled ? enumerationToken : cancellationToken);
                RowAsyncEnumerator rows = options.CreateRowAsyncEnumerator(pipe, token);

                return linked is null && !isPipeOwned ? rows : new OwningRowAsyncEnumerator(rows, isPipeOwned ? pipe : null, linked);
            }
        }

        /// <summary>
        /// A row enumerator that also completes the pipe the entry point created, and disposes the linked
        /// cancellation
        /// </summary>
        private sealed class OwningRowAsyncEnumerator(
            RowAsyncEnumerator rows,
            PipeReader? pipe,
            CancellationTokenSource? linked) : IAsyncEnumerator<Row>
        {
            public Row Current => rows.Current;

            public ValueTask<bool> MoveNextAsync() => rows.MoveNextAsync();

            public async ValueTask DisposeAsync()
            {
                await rows.DisposeAsync().ConfigureAwait(false);

                if (pipe is not null)
                {
                    await pipe.CompleteAsync().ConfigureAwait(false);
                }

                linked?.Dispose();
            }
        }
    }
}
