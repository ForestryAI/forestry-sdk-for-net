using System.Text;
using Forestry.Serialization.Converters;
using Forestry.Serialization.Definitions;
using Forestry.Serialization.Tests.Fixtures;
using Xunit;

namespace Forestry.Serialization.Tests
{
    /// <summary>
    /// Spec 004, US2 follow-on: a type definition of a type writes itself through its object converter
    /// (tasks.md T053-T055, research R49)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4); they compile once T057 declares the surface they assume.
    /// For the review gate (T056), the assumed surface:
    /// <list type="bullet">
    /// <item><c>TypeDefinition&lt;T&gt; : TypeDefinition where T : Telemetry</c> with a
    /// <c>Converter</c> of type <c>ObjectConverter&lt;T&gt;</c>, reached through the internal
    /// <c>TryGetTypeDefinition</c> once the options are locked.</item>
    /// <item><c>ObjectConverter&lt;T&gt; : Converter&lt;T&gt; where T : Telemetry</c>: <c>Write</c> abstract,
    /// <c>Read</c> sealed and throwing <see cref="NotSupportedException"/>; resolved like any converter, the
    /// developer's first, the default last.</item>
    /// </list>
    /// The default object converter's output is already covered by every serializing test (SerializeTests,
    /// CollectionWriterTests, SerializeMemoryTests), which must pass unchanged once the walk moves into it.
    /// </remarks>
    public sealed class ObjectConverterTests
    {
        private static Sample First => new() { Id = 1, Name = "Gran", Value = 31.4m, Key = Guid.Empty };

        private static string ReadBack(MemoryStream memory)
        {
            memory.Position = 0;
            using StreamReader reader = new(memory, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            return reader.ReadToEnd();
        }

        // T053 - R49: every telemetry type gets a typed definition with an object converter, the default when none is registered
        [Fact]
        public void TypeDefinition_ForAnAddedTelemetryType_ItShould_BeTypedWithAnObjectConverter()
        {
            // Arrange
            TestingSerializeOptions options = new();
            options.Telemetries.Add<Sample>().Add<OptionalSample>();

            // Act
            options.MakeReadOnly();

            // Assert
            Assert.True(options.TryGetTypeDefinition(typeof(Sample), out TypeDefinition? sample));
            TypeDefinition<Sample> typed = Assert.IsType<TypeDefinition<Sample>>(sample);
            Assert.IsAssignableFrom<ObjectConverter<Sample>>(typed.Converter);

            Assert.True(options.TryGetTypeDefinition(typeof(OptionalSample), out TypeDefinition? optional));
            Assert.IsType<TypeDefinition<OptionalSample>>(optional);
        }

        // T054 - R49 / FR-004: a developer's object converter replaces the walk for its type
        [Fact]
        public void Serialize_ForAnObjectConverterInTheOptions_ItShould_WriteThroughItInsteadOfTheDefaultWalk()
        {
            // Arrange
            TestingSerializeOptions options = new();
            options.Converters.Add(new MarkingSampleConverter());
            options.Telemetries.Add<Sample>();
            using MemoryStream memory = new();

            // Act
            TelemetrySerialization.Serialize(First, memory, options);

            // Assert
            Assert.True(options.TryGetTypeDefinition(typeof(Sample), out TypeDefinition? definition));
            Assert.IsType<MarkingSampleConverter>(Assert.IsType<TypeDefinition<Sample>>(definition).Converter);
            Assert.Equal("sample:1\n", ReadBack(memory));
        }

        // T055 - R49: the read side of an object converter is deferred
        [Fact]
        public void Read_ForAnObjectConverter_ItShould_ThrowNotSupported()
        {
            // Arrange
            ObjectConverter<Sample> converter = new MarkingSampleConverter();

            // Act + Assert
            Assert.Throws<NotSupportedException>(() => converter.Read("1"u8));
        }

        // T055 - R49: a converter for a telemetry type must be an object converter
        [Fact]
        public void MakeReadOnly_ForAConverterOfATelemetryTypeThatIsNotAnObjectConverter_ItShould_FailNamingTheType()
        {
            // Arrange
            TestingSerializeOptions options = new();
            options.Converters.Add(new PlainSampleConverter());
            options.Telemetries.Add<Sample>();

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.MakeReadOnly);

            // Assert
            Assert.Contains(nameof(Sample), exception.Message);
            Assert.Contains(nameof(PlainSampleConverter), exception.Message);
        }
    }
}
