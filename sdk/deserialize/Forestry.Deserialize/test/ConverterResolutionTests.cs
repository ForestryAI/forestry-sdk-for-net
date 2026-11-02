using Forestry.Deserialize.Converters;
using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Tests.Fixtures;
using Xunit;

namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// The effective converter of a property definition (tasks.md T028, flow 3, R14)
    /// </summary>
    /// <remarks>
    /// Test shell written before any code (C4). For the review gate (T029), the assumed surface:
    /// <c>DeserializeOptions.Converters</c> to add a developer's converter, <c>MakeReadOnly()</c> and the
    /// internal <c>TryGetTypeDefinition</c> (as in <see cref="OptionsTests"/>), and on the type definition its
    /// <c>Properties</c>, each with <c>Name</c> and its effective <c>Converter</c>.
    /// </remarks>
    public sealed class ConverterResolutionTests
    {
        // T028 - a converter added to the options wins over the primitive (options, attributes, media defaults, primitives)
        [Fact]
        public void EffectiveConverter_ForAnInt32ConverterInTheOptions_ItShould_BeUsedInsteadOfThePrimitive()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Converters.Add(new MarkingInt32Converter());
            options.Rows.Add<Log>();
            options.MakeReadOnly();

            // Act
            Assert.True(options.TryGetTypeDefinition(typeof(Log), out TypeDefinition? definition));
            PropertyDefinition logKey = Assert.Single(definition!.Properties, property => property.Name == nameof(Log.LogKey));

            // Assert
            Assert.IsType<MarkingInt32Converter>(logKey.Converter);
        }

        // T081 - a converter added to the options is used inside Nullable<T>
        [Fact]
        public void EffectiveConverter_ForANullableInt32WithAnInt32ConverterInTheOptions_ItShould_WrapIt()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Converters.Add(new MarkingInt32Converter());
            options.Rows.Add<LogWithNullableKey>();
            options.MakeReadOnly();

            // Act
            Assert.True(options.TryGetTypeDefinition(typeof(LogWithNullableKey), out TypeDefinition? definition));
            PropertyDefinition logKey = Assert.Single(definition!.Properties, property => property.Name == nameof(LogWithNullableKey.LogKey));

            // Assert
            Converter<int?> converter = Assert.IsAssignableFrom<Converter<int?>>(logKey.Converter);
            Assert.Equal(-1, converter.Convert("5"u8));
        }
    }
}
