using System.Globalization;
using Forestry.Serialization.Definitions;

namespace Forestry.Serialization.JsonLines
{
    /// <summary>
    /// The exceptions JSON Lines throws, and their messages
    /// </summary>
    internal static class Throwing
    {
        /// <summary>
        /// A converter wrote through the writer's buffer members (spec 005 FR-011b, research R66)
        /// </summary>
        public static NotSupportedException DirectBufferUse() => new(
            "JSON Lines does not support writing bytes directly through the telemetry writer's GetSpan, GetMemory " +
            "and Advance: its JSON writer buffers, so the bytes would land out of order. Write values through " +
            "WriteValue, or a complete JSON value through WriteRaw.");

        /// <summary>
        /// A NaN or infinite double, which JSON has no number for (FR-011a)
        /// </summary>
        public static NotSupportedException NonFiniteDouble(PropertyDefinition? member, double value) => new(string.Format(
            CultureInfo.InvariantCulture,
            "JSON Lines cannot write {0} for member '{1}': JSON numbers are finite.",
            value.ToString(CultureInfo.InvariantCulture),
            member?.Name ?? "?"));
    }
}
