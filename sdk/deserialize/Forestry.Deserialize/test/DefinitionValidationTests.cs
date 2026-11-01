using Forestry.Deserialize.Tests.Fixtures;
using Xunit;

namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// Spec 001, FR-008: rejections before any byte is read (tasks.md T021-T026)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4); they compile once US1's implementation (T030-T039)
    /// declares the surface they assume. For the review gate (T029), the assumed surface:
    /// <list type="bullet">
    /// <item><c>Deserialization.Deserialize(string, DeserializeOptions)</c> validates the added row types and
    /// throws <see cref="InvalidOperationException"/>, naming the class, before it returns, so before any
    /// byte is read: <see cref="TestingDeserializeOptions"/> cannot read a document, so reaching its
    /// enumerator would throw <see cref="NotSupportedException"/> instead.</item>
    /// <item><c>Rows.Add(params Type[])</c> throws <see cref="ArgumentException"/>, naming the type, for a type
    /// not extending <c>Row</c> (R12).</item>
    /// </list>
    /// </remarks>
    public sealed class DefinitionValidationTests
    {
        private static TestingDeserializeOptions OptionsWith<TRow>() where TRow : Row
        {
            TestingDeserializeOptions options = new();
            options.Rows.Add<TRow>();
            return options;
        }

        private static void AssertRejectedAtTheCall(DeserializeOptions options, string expectedName)
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Deserialization.Deserialize("<Root />", options));

            Assert.Contains(expectedName, exception.Message);
        }

        // T021 - quickstart 13 / FR-008: no row type added
        [Fact]
        public void Deserialize_ForOptionsWithoutRowTypes_ItShould_ThrowBeforeAnyByteIsRead()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Deserialization.Deserialize("<Root />", new TestingDeserializeOptions()));

            Assert.Contains("row type", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        // T022 - FR-008: a property whose type no converter handles (no converter for Measurement)
        [Fact]
        public void Deserialize_ForAPropertyWithoutAConverter_ItShould_ThrowBeforeAnyByteIsRead()
        {
            AssertRejectedAtTheCall(OptionsWith<LogWithNestedProperty>(), nameof(LogWithNestedProperty));
        }

        // T023 - FR-008 (this slice): the chosen constructor has parameters.
        //
        // In spec 001 any constructor parameter is rejected, whatever its type. From spec 002/003 a
        // constructor may take parameters, but each must be a type derived from Row: an ancestor
        // taken from the open-row stack (research R5), whose type is added automatically (R12).
        // The fixture's parameter is a row (Stem), so this test is about "has parameters" only, and
        // it fails as soon as row-typed parameters are allowed: then turn it into an "accepted"
        // test, and add one rejecting a parameter that is not a row (e.g. int).
        [Fact]
        public void Deserialize_ForAConstructorWithParameters_ItShould_ThrowBeforeAnyByteIsRead()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Deserialization.Deserialize("<Root />", OptionsWith<LogWithParameters>()));

            Assert.Contains(nameof(LogWithParameters), exception.Message);
            Assert.Contains("constructor", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("parameter", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        // T024 - FR-008: several public constructors and none marked
        [Fact]
        public void Deserialize_ForTwoUnmarkedPublicConstructors_ItShould_ThrowBeforeAnyByteIsRead()
        {
            AssertRejectedAtTheCall(OptionsWith<LogWithTwoPublicConstructors>(), nameof(LogWithTwoPublicConstructors));
        }

        // T025 - FR-008: more than one constructor marked.
        //
        // A row type uses exactly one constructor, so at most one may carry [RowConstructor]. The
        // fixture marks two (the second needs a parameter only because C# allows one parameterless
        // constructor), so the message must give "more than one marked" as the reason, not the
        // parameter (that is T023's reason).
        [Fact]
        public void Deserialize_ForTwoMarkedConstructors_ItShould_ThrowBeforeAnyByteIsRead()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Deserialization.Deserialize("<Root />", OptionsWith<LogWithTwoMarkedConstructors>()));

            Assert.Contains(nameof(LogWithTwoMarkedConstructors), exception.Message);
            Assert.Contains(nameof(RowConstructorAttribute).Replace("Attribute", string.Empty), exception.Message);
            Assert.Contains("more than one", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        // T026 - FR-008 / R12: a type not extending Row, added by Type
        [Fact]
        public void RowsAdd_ForATypeNotExtendingRow_ItShould_ThrowWhenAdded()
        {
            TestingDeserializeOptions options = new();

            ArgumentException exception = Assert.Throws<ArgumentException>(() => options.Rows.Add(typeof(NotARow)));

            Assert.Contains(nameof(NotARow), exception.Message);
        }
    }
}
