using Forestry.Serialization.Tests.Fixtures;
using Xunit;

namespace Forestry.Serialization.Tests
{
    /// <summary>
    /// Spec 001, FR-008: rejections before any byte is read (tasks.md T021-T026)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4); they compile once US1's implementation (T030-T039)
    /// declares the surface they assume. For the review gate (T029), the assumed surface:
    /// <list type="bullet">
    /// <item><c>TelemetrySerializer.Deserialize(string, DeserializeOptions)</c> validates the added telemetry types and
    /// throws <see cref="InvalidOperationException"/>, naming the class, before it returns, so before any
    /// byte is read: <see cref="TestingDeserializeOptions"/> cannot read a document, so reaching its
    /// enumerator would throw <see cref="NotSupportedException"/> instead.</item>
    /// <item><c>Telemetries.Add(params Type[])</c> throws <see cref="ArgumentException"/>, naming the type, for a type
    /// not extending <c>Telemetry</c> (R12).</item>
    /// </list>
    /// </remarks>
    public sealed class DefinitionValidationTests
    {
        /// <summary>
        /// Words <c>MemberTypeIsNestedStructure</c>'s message contains (spec 003 research R33)
        /// </summary>
        private const string NestedStructure = "nested structure";

        /// <summary>
        /// The flat-telemetry rule as <c>MemberTypeIsNestedStructure</c>'s message states it (constitution C7)
        /// </summary>
        private const string SingleValue = "single value";

        /// <summary>
        /// The second fix <c>MemberTypeIsNestedStructure</c>'s message gives (research R33)
        /// </summary>
        private const string OwnTelemetryType = "own telemetry type";

        private static TestingDeserializeOptions OptionsWith<TTelemetry>() where TTelemetry : Telemetry
        {
            TestingDeserializeOptions options = new();
            options.Telemetries.Add<TTelemetry>();
            return options;
        }

        // Spec 003, US1 (#126): telemetries are flat (tasks.md T002-T008, research R32, R33).
        //
        // Test shells written before any code (C4). For the review gate (T010), the assumed surface:
        // - MakeReadOnly() builds the type definitions and throws one InvalidOperationException per
        //   telemetry type, naming the type and each failing member with its type and reason (R33).
        // - MemberTypeIsNestedStructure's message contains "nested structure", the rule ("single
        //   value") and the fix "own telemetry type"; MemberTypeNotUtf8Formattable's names
        //   IUtf8SpanFormattable; MemberWithoutConverter's (spec 001) says "converter".
        // - PropertyDefinition.ResolvedConverter replaces PropertyDefinition.Converter (R33).

        // T002 - US1-1 / quickstart 1: every single-value kind with a converter locks, and each member
        // gets a resolved converter for its type
        [Fact]
        public void MakeReadOnly_ForATelemetryTypeOfSingleValues_ItShould_ResolveAConverterForEachMember()
        {
            // Arrange
            TestingDeserializeOptions options = OptionsWith<FlatTelemetry>();
            options.Converters.Add(new MeasureConverter());

            // Act
            options.MakeReadOnly();

            // Assert
            Assert.True(options.TryGetTypeDefinition(typeof(FlatTelemetry), out Definitions.TypeDefinition? definition));
            Assert.Equal(12, definition!.Properties.Count);
            Assert.All(definition.Properties, property => Assert.True(property.ResolvedConverter.CanConvert(property.MemberType), property.Name));
        }

        // T002 (R32, R33) - single values without a built-in converter (enum, DateOnly, TimeSpan) pass
        // the flat check and fail only for lack of a converter, all three in one message
        [Fact]
        public void MakeReadOnly_ForSingleValuesWithoutConverters_ItShould_ThrowForTheConverterOnly()
        {
            // Arrange
            TestingDeserializeOptions options = OptionsWith<SingleValuesWithoutConverters>();

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.MakeReadOnly);

            // Assert
            Assert.Contains(nameof(SingleValuesWithoutConverters), exception.Message);
            Assert.Contains(nameof(SingleValuesWithoutConverters.Grade), exception.Message);
            Assert.Contains(nameof(SingleValuesWithoutConverters.Felled), exception.Message);
            Assert.Contains(nameof(SingleValuesWithoutConverters.Duration), exception.Message);
            Assert.Contains("converter", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(NestedStructure, exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        // T003 - US1-2 / US1-3 / US1-4 / SC-001 (quickstart 2, 3): each nested-structure kind fails,
        // naming the telemetry type, the member, its type and the rule
        [Theory]
        [InlineData(typeof(LogWithNestedProperty), nameof(LogWithNestedProperty.Measurement), nameof(Measurement))]
        [InlineData(typeof(LogWithTelemetryMember), nameof(LogWithTelemetryMember.Stem), nameof(Stem))]
        [InlineData(typeof(LogWithArray), nameof(LogWithArray.Diameters), "Int32[]")]
        [InlineData(typeof(LogWithList), nameof(LogWithList.Diameters), "List")]
        [InlineData(typeof(LogWithDictionary), nameof(LogWithDictionary.Diameters), "Dictionary")]
        [InlineData(typeof(LogWithCompositeStruct), nameof(LogWithCompositeStruct.Position), nameof(Coordinate))]
        public void MakeReadOnly_ForANestedStructureMember_ItShould_ThrowNamingTheMemberAndTheRule(Type telemetryType, string member, string memberType)
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Telemetries.Add(telemetryType);

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.MakeReadOnly);

            // Assert
            Assert.Contains(telemetryType.Name, exception.Message);
            Assert.Contains(member, exception.Message);
            Assert.Contains(memberType, exception.Message);
            Assert.Contains(NestedStructure, exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(SingleValue, exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(OwnTelemetryType, exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        // T004 - US1-5 / FR-003 / SC-001 (quickstart 4): a converter does not make a composite member flat
        [Fact]
        public void MakeReadOnly_ForACompositeMemberWithAConverter_ItShould_StillThrowForTheNestedStructure()
        {
            // Arrange
            TestingDeserializeOptions options = OptionsWith<LogWithConvertedComposite>();
            options.Converters.Add(new MeasurementConverter());

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.MakeReadOnly);

            // Assert
            Assert.Contains(nameof(LogWithConvertedComposite), exception.Message);
            Assert.Contains(nameof(LogWithConvertedComposite.Measurement), exception.Message);
            Assert.Contains(NestedStructure, exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        // T005 - R32 / R33: a struct that formats only to chars fails with MemberTypeNotUtf8Formattable's
        // message, not as a nested structure
        [Fact]
        public void MakeReadOnly_ForAStructFormattingOnlyToChars_ItShould_ThrowNamingIUtf8SpanFormattable()
        {
            // Arrange
            TestingDeserializeOptions options = OptionsWith<LogWithCharsOnlyStruct>();

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.MakeReadOnly);

            // Assert
            Assert.Contains(nameof(LogWithCharsOnlyStruct), exception.Message);
            Assert.Contains(nameof(LogWithCharsOnlyStruct.Diameter), exception.Message);
            Assert.Contains(nameof(IUtf8SpanFormattable), exception.Message);
            Assert.DoesNotContain(NestedStructure, exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        // T006 - R33, one message per type: a nested structure and a single value without a converter
        // in one telemetry type fail together, in one exception
        [Fact]
        public void MakeReadOnly_ForTwoFailingMembers_ItShould_NameBothInOneException()
        {
            // Arrange
            TestingDeserializeOptions options = OptionsWith<LogWithTwoFailingMembers>();

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.MakeReadOnly);

            // Assert
            Assert.Contains(nameof(LogWithTwoFailingMembers), exception.Message);
            Assert.Contains(nameof(LogWithTwoFailingMembers.Diameters), exception.Message);
            Assert.Contains(nameof(LogWithTwoFailingMembers.Length), exception.Message);
            Assert.Contains(NestedStructure, exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("converter", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        // T007 - R33, ignored members (quickstart 5): a member a definition policy leaves out is never
        // checked
        [Fact]
        public void MakeReadOnly_ForANestedStructureLeftOutByAPolicy_ItShould_Lock()
        {
            // Arrange
            TestingDeserializeOptions options = OptionsWith<LogWithExcludedList>();
            options.Policies.Add<LogWithExcludedList>(new ExcludingPolicy());

            // Act
            options.MakeReadOnly();

            // Assert
            Assert.True(options.TryGetTypeDefinition(typeof(LogWithExcludedList), out Definitions.TypeDefinition? definition));
            Assert.DoesNotContain(definition!.Properties, property => property.Name == nameof(LogWithExcludedList.ExcludedDiameters));
            Assert.Contains(definition.Properties, property => property.Name == nameof(LogWithExcludedList.LogKey));
        }

        // T021 - quickstart 13 / FR-008: no telemetry type added
        [Fact]
        public void Deserialize_ForOptionsWithoutTelemetryTypes_ItShould_ThrowBeforeAnyByteIsRead()
        {
            // Arrange + Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => TelemetrySerializer.Deserialize("<Root />", new TestingDeserializeOptions()));

            // Assert
            Assert.Contains("telemetry type", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        // T022 - FR-008: a nested class member, rejected before any byte is read.
        //
        // Spec 003 T008: spec 001 rejected it for lack of a converter; the flat check now runs first
        // (research R33), so it fails as a nested structure. The message must name the member's type
        // and the reason, not only the class, so a rejection for another reason (e.g. the
        // constructor) does not pass this test.
        [Fact]
        public void Deserialize_ForANestedClassMember_ItShould_ThrowBeforeAnyByteIsRead()
        {
            // Arrange + Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => TelemetrySerializer.Deserialize("<Root />", OptionsWith<LogWithNestedProperty>()));

            // Assert
            Assert.Contains(nameof(LogWithNestedProperty), exception.Message);
            Assert.Contains(nameof(Measurement), exception.Message);
            Assert.Contains(NestedStructure, exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        // T023 - FR-008 (this slice): the chosen constructor has parameters.
        //
        // In spec 001 any constructor parameter is rejected, whatever its type. From spec 002/003 a
        // constructor may take parameters, but each must be a type derived from Telemetry: an ancestor
        // taken from the open-telemetry stack (research R5), whose type is added automatically (R12).
        // The fixture's parameter is a telemetry (Stem), so this test is about "has parameters" only, and
        // it fails as soon as telemetry-typed parameters are allowed: then turn it into an "accepted"
        // test, and add one rejecting a parameter that is not a telemetry (e.g. int).
        [Fact]
        public void Deserialize_ForAConstructorWithParameters_ItShould_ThrowBeforeAnyByteIsRead()
        {
            // Arrange + Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => TelemetrySerializer.Deserialize("<Root />", OptionsWith<LogWithParameters>()));

            // Assert
            Assert.Contains(nameof(LogWithParameters), exception.Message);
            Assert.Contains("constructor", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("parameter", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        // T024 - FR-008: several public constructors and none marked
        [Fact]
        public void Deserialize_ForTwoUnmarkedPublicConstructors_ItShould_ThrowBeforeAnyByteIsRead()
        {
            // Arrange + Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => TelemetrySerializer.Deserialize("<Root />", OptionsWith<LogWithTwoPublicConstructors>()));

            // Assert
            Assert.Contains(nameof(LogWithTwoPublicConstructors), exception.Message);
        }

        // T079 - FR-008 / R5: no public constructor and none marked
        [Fact]
        public void Deserialize_ForNoPublicConstructorAndNoneMarked_ItShould_ThrowBeforeAnyByteIsRead()
        {
            // Arrange + Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => TelemetrySerializer.Deserialize("<Root />", OptionsWith<LogWithPrivateConstructor>()));

            // Assert
            Assert.Contains(nameof(LogWithPrivateConstructor), exception.Message);
            Assert.DoesNotContain("parameter", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        // T025 - FR-008: more than one constructor marked.
        //
        // A telemetry type uses exactly one constructor, so at most one may carry [TelemetryConstructor]. The
        // fixture marks two (the second needs a parameter only because C# allows one parameterless
        // constructor), so the message must give "more than one marked" as the reason, not the
        // parameter (that is T023's reason).
        [Fact]
        public void Deserialize_ForTwoMarkedConstructors_ItShould_ThrowBeforeAnyByteIsRead()
        {
            // Arrange + Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => TelemetrySerializer.Deserialize("<Root />", OptionsWith<LogWithTwoMarkedConstructors>()));

            // Assert
            Assert.Contains(nameof(LogWithTwoMarkedConstructors), exception.Message);
            Assert.Contains(nameof(TelemetryConstructorAttribute).Replace("Attribute", string.Empty), exception.Message);
            Assert.Contains("more than one", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        // T026 - FR-008 / R12: a type not extending Telemetry, added by Type
        [Fact]
        public void TelemetriesAdd_ForATypeNotExtendingTelemetry_ItShould_ThrowWhenAdded()
        {
            // Arrange
            TestingDeserializeOptions options = new();

            // Act
            ArgumentException exception = Assert.Throws<ArgumentException>(() => options.Telemetries.Add(typeof(NotATelemetry)));

            // Assert
            Assert.Contains(nameof(NotATelemetry), exception.Message);
        }

        // T016 - FR-007 / research R22: a type definition finds a member's index by its C# name, the bit
        // its set-member bitmap uses; an unknown name is not found
        [Fact]
        public void TypeDefinition_ForMemberNames_ItShould_FindTheIndexOfEachMemberOnly()
        {
            // Arrange
            TestingDeserializeOptions options = OptionsWith<Log>();
            options.MakeReadOnly();
            Definitions.TypeDefinition definition = options.ThrowingGetTypeDefinition(typeof(Log));
            int logVolumeIndex = definition.Properties.Select(property => property.Name).ToList().IndexOf(nameof(Log.LogVolume));

            // Act
            bool foundLogVolume = definition.TryGetPropertyIndex(nameof(Log.LogVolume), out int index);
            bool foundUnknown = definition.TryGetPropertyIndex("NoSuchMember", out _);

            // Assert
            Assert.True(foundLogVolume);
            Assert.Equal(logVolumeIndex, index);
            Assert.False(foundUnknown);
        }
    }
}
