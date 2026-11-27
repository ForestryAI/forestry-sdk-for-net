using System.Buffers;
using System.Globalization;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// A writer that overrides every typed <c>WriteValue</c> to write its type's name and the value, so a test
    /// sees which overload a converter called (spec 004 research R49, option A)
    /// </summary>
    internal sealed class TypedTelemetryWriter : TelemetryWriter
    {
        /// <summary>
        /// A writer writing to <paramref name="output"/>, as the serializer sets one up for a call
        /// </summary>
        /// <param name="output"></param>
        /// <returns></returns>
        public static TypedTelemetryWriter Over(IBufferWriter<byte> output)
        {
            TypedTelemetryWriter writer = new();
            writer.Reset(output);
            return writer;
        }

        public override void WriteValue(ReadOnlySpan<char> value) => Mark("string", value.ToString());

        public override void WriteValue(bool value) => Mark("bool", value.ToString(CultureInfo.InvariantCulture));

        public override void WriteValue(int value) => Mark("int", value.ToString(CultureInfo.InvariantCulture));

        public override void WriteValue(long value) => Mark("long", value.ToString(CultureInfo.InvariantCulture));

        public override void WriteValue(double value) => Mark("double", value.ToString(CultureInfo.InvariantCulture));

        public override void WriteValue(decimal value) => Mark("decimal", value.ToString(CultureInfo.InvariantCulture));

        public override void WriteValue(Guid value) => Mark("Guid", value.ToString());

        public override void WriteValue(DateTime value) => Mark("DateTime", value.Year.ToString(CultureInfo.InvariantCulture));

        public override void WriteValue(DateTimeOffset value) => Mark("DateTimeOffset", value.Year.ToString(CultureInfo.InvariantCulture));

        public override void WriteValue(DateOnly value) => Mark("DateOnly", value.Year.ToString(CultureInfo.InvariantCulture));

        public override void WriteValue(TimeOnly value) => Mark("TimeOnly", value.Hour.ToString(CultureInfo.InvariantCulture));

        public override void WriteValue(TimeSpan value) => Mark("TimeSpan", value.Ticks.ToString(CultureInfo.InvariantCulture));

        public override void WriteEnumName(ReadOnlySpan<byte> utf8Name) => Mark("enum", System.Text.Encoding.UTF8.GetString(utf8Name));

        public override void WriteValue<T>(T value) => Mark("generic", typeof(T).Name + ":" + value);

        private void Mark(string overload, string value) => WriteRaw(System.Text.Encoding.UTF8.GetBytes(overload + ":" + value));
    }
}
