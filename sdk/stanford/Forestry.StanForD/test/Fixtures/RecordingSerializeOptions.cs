using System.Reflection;
using System.Text;
using Forestry.Serialization;
using Forestry.Serialization.Converters;
using Forestry.Serialization.Definitions;
using Forestry.Serialization.Writing;

namespace Forestry.StanForD.Tests.Fixtures
{
    /// <summary>
    /// A minimal format for StanForD's writing tests (spec 005 tasks.md T026): a telemetry is its members'
    /// values joined by <c>;</c>, one per line, with the writer's plain defaults, so an enum is written as the
    /// text its converter gives
    /// </summary>
    public sealed class RecordingSerializeOptions : SerializeOptions
    {
        /// <summary>
        /// Converters standing in for a format package's defaults (T028: a factory forcing number form)
        /// </summary>
        public IReadOnlyList<Converter> Format { get; init; } = [];

        protected override IEnumerable<Converter> FormatConverters => Format;

        protected override TelemetryWriter CreateWriter() => new LineWriter();

        /// <summary>
        /// Serialize <paramref name="telemetry"/> and return its line, without the new line
        /// </summary>
        public string Line(Telemetry telemetry)
        {
            using MemoryStream memory = new();
            TelemetrySerialization.Serialize(telemetry, memory, this);
            return Encoding.UTF8.GetString(memory.ToArray()).TrimEnd('\n');
        }

        private sealed class LineWriter : TelemetryWriter
        {
            private bool _wroteMember;

            protected override void OnReset() => _wroteMember = false;

            public override void WriteStartTelemetry(TypeDefinition definition) => _wroteMember = false;

            public override void WriteEndTelemetry() => WriteRaw("\n"u8);

            public override void WriteName(PropertyDefinition property)
            {
                if (_wroteMember)
                {
                    WriteRaw(";"u8);
                }

                _wroteMember = true;
            }
        }
    }

    /// <summary>
    /// The StanForD enums as members of one test telemetry
    /// </summary>
    public sealed class StanForDEnums : Telemetry
    {
        public CuttingReason CuttingReason { get; set; }

        public ProcessingCategory ProcessingCategory { get; set; }

        public CoordinateReferenceSystem CoordinateReferenceSystem { get; set; }
    }

    /// <summary>
    /// An enum converter in number form, as a format's would be for a member marked for numbers
    /// </summary>
    public sealed class NumberEnumConverter<TEnum>() : EnumConverter<TEnum>(EnumForm.Number) where TEnum : struct, Enum
    {
    }

    /// <summary>
    /// A format factory asking for number form on every enum member (spec 005 T028): StanForD's registered
    /// converter must still win (FR-002e)
    /// </summary>
    public sealed class NumberEnumFactory : ConverterFactory
    {
        public override bool UsesMember => true;

        public override bool CanConvert(Type type) => type.IsEnum;

        public override Converter CreateConverter(Type type, TelemetrySerializationOptions options) =>
            (Converter)Activator.CreateInstance(typeof(NumberEnumConverter<>).MakeGenericType(type))!;

        public override Converter CreateConverter(Type type, MemberInfo member, TelemetrySerializationOptions options) => CreateConverter(type, options);
    }
}
