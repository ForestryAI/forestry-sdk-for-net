using Forestry.Serialization.Converters;
using Forestry.Serialization.Definitions;
using Forestry.Serialization.Tests.Fixtures;
using Xunit;

namespace Forestry.Serialization.Tests
{
    /// <summary>
    /// The resolved converter of a property definition (spec 001 tasks.md T028, flow 3, R14; named
    /// "resolved" in spec 003, research R33)
    /// </summary>
    /// <remarks>
    /// Test shell written before any code (C4). For the review gate (T029), the assumed surface:
    /// <c>DeserializeOptions.Converters</c> to add a developer's converter, <c>MakeReadOnly()</c> and the
    /// internal <c>TryGetTypeDefinition</c> (as in <see cref="OptionsTests"/>), and on the type definition its
    /// <c>Properties</c>, each with <c>Name</c> and its <c>ResolvedConverter</c> (spec 003 T009).
    /// </remarks>
    public sealed class ConverterResolutionTests
    {
        // T028 - a converter added to the options wins over the primitive (options, attributes, format defaults, primitives)
        [Fact]
        public void EffectiveConverter_ForAnInt32ConverterInTheOptions_ItShould_BeUsedInsteadOfThePrimitive()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Converters.Add(new MarkingInt32Converter());
            options.Telemetries.Add<Log>();
            options.MakeReadOnly();

            // Act
            Assert.True(options.TryGetTypeDefinition(typeof(Log), out TypeDefinition? definition));
            PropertyDefinition logKey = Assert.Single(definition!.Properties, property => property.Name == nameof(Log.LogKey));

            // Assert
            Assert.IsType<MarkingInt32Converter>(logKey.ResolvedConverter);
        }

        // T081 - a converter added to the options is used inside Nullable<T>
        [Fact]
        public void EffectiveConverter_ForANullableInt32WithAnInt32ConverterInTheOptions_ItShould_WrapIt()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Converters.Add(new MarkingInt32Converter());
            options.Telemetries.Add<LogWithNullableKey>();
            options.MakeReadOnly();

            // Act
            Assert.True(options.TryGetTypeDefinition(typeof(LogWithNullableKey), out TypeDefinition? definition));
            PropertyDefinition logKey = Assert.Single(definition!.Properties, property => property.Name == nameof(LogWithNullableKey.LogKey));

            // Assert
            Converter<int?> converter = Assert.IsAssignableFrom<Converter<int?>>(logKey.ResolvedConverter);
            Assert.Equal(-1, converter.Convert("5"u8));
        }
    }
}
