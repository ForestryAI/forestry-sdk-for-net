namespace Forestry.Serialization.Writing
{
    /// <summary>
    /// A writer that knows about one collection (spec 004 research R51): what
    /// <see cref="MonitoringCollectionStream"/> and <see cref="MonitoringCollectionPipeWriter"/> share, so whoever
    /// collects can read either the same way
    /// </summary>
    public interface ICollectionWriter
    {
        /// <summary>
        /// The collection's settings
        /// </summary>
        MonitoringCollectionOptions Options { get; }

        /// <summary>
        /// Telemetries written so far, raised by the serializer after each whole telemetry
        /// </summary>
        long Sequence { get; }

        /// <summary>
        /// Bytes that reached the collection writer
        /// </summary>
        long BytesWritten { get; }

        /// <summary>
        /// A limit in <see cref="Options"/> is reached; checked by whoever collects between telemetries
        /// </summary>
        bool IsFull { get; }
    }

    /// <summary>
    /// How the serializer raises <see cref="ICollectionWriter.Sequence"/> at each telemetry boundary; internal,
    /// so nothing else can (research R52)
    /// </summary>
    internal interface ICollectionWriterSequence
    {
        /// <summary>
        /// One whole telemetry reached the collection writer
        /// </summary>
        void OnTelemetryWritten();
    }
}
