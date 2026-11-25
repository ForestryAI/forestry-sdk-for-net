using System.Text;

namespace Forestry.Serialization
{
    public static partial class TelemetrySerializer
    {
        /// <summary>
        /// Telemetries from a document as text, lazily and synchronously. The text is encoded to UTF-8; the
        /// options are locked and their definitions built before this returns, so a telemetry type that
        /// cannot be defined throws here (FR-008)
        /// </summary>
        /// <param name="document"></param>
        /// <param name="options">A format's options with the telemetry types added</param>
        /// <returns>The telemetries in document order, enumerable once</returns>
        public static IEnumerable<Telemetry> Deserialize(string document, DeserializeOptions options)
        {
            ArgumentNullException.ThrowIfNull(document);

            return Deserialize(Encoding.UTF8.GetBytes(document), options);
        }
    }
}
