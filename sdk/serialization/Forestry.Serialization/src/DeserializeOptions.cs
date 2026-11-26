using System.IO.Pipelines;
using Forestry.Serialization.Converters;
using Forestry.Serialization.Policies;
using Forestry.Serialization.Reading;

namespace Forestry.Serialization
{
    /// <summary>
    /// Options of one format for reading (e.g. <c>DeserializeXmlOptions</c>): deserializing calls each
    /// converter's <see cref="Converter{T}.Read"/>. Adds to what reading and writing share
    /// (<see cref="TelemetrySerializationOptions"/>: telemetry types, converters, definitions) the policies and
    /// settings of a deserialization. Once first used they are read-only (research R6; spec 004 R44)
    /// </summary>
    public abstract partial class DeserializeOptions : TelemetrySerializationOptions
    {
        /// <summary>
        /// Options of a format, for reading
        /// </summary>
        protected DeserializeOptions()
        {
        }

        #region Policies
        /// <summary>
        /// Policies added by the developer, as instances living as long as the options; they win over
        /// policy attributes for the same method and target (research R7, R19)
        /// </summary>
        public PolicyCollection Policies => _policies ??= new PolicyCollection(this);

        private PolicyCollection? _policies;
        #endregion

        #region Deserialization context
        /// <summary>
        /// Telemetries policies may keep in the deserialization context after their completion (spec 002 FR-011a,
        /// S1); open telemetries do not count. Going past it throws a
        /// <see cref="DeserializeContextLimitException"/>. Default 100; locked with the options
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Less than 0</exception>
        /// <exception cref="InvalidOperationException">The options are read-only</exception>
        public int MaxContextTelemetries
        {
            get => _maxContextTelemetries;
            set
            {
                ThrowingAssertNotReadOnly();
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                _maxContextTelemetries = value;
            }
        }

        private int _maxContextTelemetries = DefaultMaxContextTelemetries;

        /// <summary>
        /// Default of <see cref="MaxContextTelemetries"/>: StanForD hpr documents keep about 16 telemetries (research R28)
        /// </summary>
        internal const int DefaultMaxContextTelemetries = 100;
        #endregion

        #region Format
        /// <summary>
        /// The format's synchronous telemetry enumerator over UTF-8 bytes (research R2)
        /// </summary>
        /// <param name="utf8"></param>
        /// <returns></returns>
        protected internal abstract FormatTelemetryEnumerator CreateTelemetryEnumerator(ReadOnlyMemory<byte> utf8);

        /// <summary>
        /// The format's asynchronous telemetry enumerator over the UTF-8 bytes of a pipe (research R2)
        /// </summary>
        /// <param name="utf8"></param>
        /// <param name="cancellationToken">Cancels a move waiting for bytes</param>
        /// <returns></returns>
        protected internal abstract FormatTelemetryAsyncEnumerator CreateTelemetryAsyncEnumerator(PipeReader utf8, CancellationToken cancellationToken);

        /// <summary>
        /// Whether the core skips a leading UTF-8 byte order mark before the format's reader sees the bytes
        /// (research R18). True by default, for a reader that does not handle one (e.g. JSON's); a format
        /// whose reader checks the mark itself (XML's, against the declared encoding) returns false
        /// </summary>
        protected internal virtual bool SkipsByteOrderMark => true;
        #endregion
    }
}
