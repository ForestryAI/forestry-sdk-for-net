using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using Forestry.Serialization.Definitions;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization
{
    /// <summary>
    /// Writing: the walk and helpers every <c>Serialize</c> and <c>SerializeAsync</c> entry point shares
    /// (spec 004 research R48, R49)
    /// </summary>
    public static partial class TelemetrySerialization
    {
        /// <summary>
        /// Bytes gathered before they are handed to the target when many telemetries are serialized in one
        /// call (spec 004 research R48)
        /// </summary>
        private const int FlushThreshold = 16 * 1024;

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
