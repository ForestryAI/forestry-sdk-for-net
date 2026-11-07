using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Tests.Fixtures;
using Xunit;

namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// Which policies apply, resolved once when the options lock (tasks.md T053, T089; quickstart 10;
    /// research R7, R19)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4), so they do not compile until T056-T058 declare the
    /// surface they assume (research R19). For the review gate (T055), the assumed surface, beyond
    /// <c>MakeReadOnly()</c> and the internal <c>TryGetTypeDefinition</c> (as in <see cref="OptionsTests"/>):
    /// <list type="bullet">
    /// <item><c>[Policy&lt;TPolicy&gt;]</c> on a class or member; through the options, instances:
    /// <c>options.Policies.Add(policy)</c> (options-wide), <c>Add&lt;TRow&gt;(policy)</c> (type) and
    /// <c>Add&lt;TRow&gt;(memberName, policy)</c> (member).</item>
    /// <item><c>options.Policies.Applying(TypeDefinition)</c> and <c>Applying(PropertyDefinition)</c>: the
    /// enumeration policies acting on that definition, in chain order, empty when only the built-in steps
    /// apply. The definitions themselves hold no enumeration policies.</item>
    /// <item><c>DefinitionPolicy.IncludeMember</c> decides the members when a definition is built.</item>
    /// </list>
    /// Precedence, most specific first: option for the member, option for the type, attribute on the
    /// member, attribute on the type, options-wide, built-in (R7). The core reads no document (C3), so
    /// these tests read the resolution; acting in a deserialization is the XML package's tests (T051).
    /// </remarks>
    public sealed class PolicyPrecedenceTests
    {
        private static TypeDefinition Lock<TRow>(TestingDeserializeOptions options) where TRow : Row
        {
            options.Rows.Add<TRow>();
            options.MakeReadOnly();
            Assert.True(options.TryGetTypeDefinition(typeof(TRow), out TypeDefinition? definition));
            return definition!;
        }

        private static PropertyDefinition Member(TypeDefinition definition, string name) =>
            Assert.Single(definition.Properties, property => property.Name == name);

        private static IEnumerable<Type> Applying(TestingDeserializeOptions options, TypeDefinition definition) =>
            options.Policies.Applying(definition).Select(policy => policy.GetType());

        private static IEnumerable<Type> Applying(TestingDeserializeOptions options, PropertyDefinition definition) =>
            options.Policies.Applying(definition).Select(policy => policy.GetType());

        // T053 - built-in: with no policy, only the built-in steps apply
        [Fact]
        public void Applying_WithoutPolicies_ItShould_BeEmpty()
        {
            // Arrange
            TestingDeserializeOptions options = new();

            // Act
            TypeDefinition definition = Lock<Log>(options);

            // Assert
            Assert.Empty(Applying(options, definition));
            Assert.Empty(Applying(options, Member(definition, nameof(Log.LogKey))));
        }

        // T053 - attribute on the type: applies when nothing more specific is attached
        [Fact]
        public void Applying_ForAnAttributeOnTheType_ItShould_BeThatPolicy()
        {
            // Arrange
            TestingDeserializeOptions options = new();

            // Act
            TypeDefinition definition = Lock<LogWithConstructMarker>(options);

            // Assert
            Assert.Equal([typeof(ConstructMarkerPolicy)], Applying(options, definition));
        }

        // T053 - quickstart 10 / US4-3 / FR-012: the same method by attribute and by options: only the options' applies
        [Fact]
        public void Applying_ForTheSameMethodByAttributeAndByOptions_ItShould_BeOnlyTheOptions()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Policies.Add<LogWithConstructMarker>(new OtherConstructMarkerPolicy());

            // Act
            TypeDefinition definition = Lock<LogWithConstructMarker>(options);

            // Assert
            Assert.Equal([typeof(OtherConstructMarkerPolicy)], Applying(options, definition));
        }

        // T053 - options-wide is less specific than an attribute on the type
        [Fact]
        public void Applying_ForAnOptionsWidePolicyAndAnAttributeOnTheType_ItShould_BeTheAttribute()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Policies.Add(new OtherConstructMarkerPolicy());

            // Act
            TypeDefinition definition = Lock<LogWithConstructMarker>(options);

            // Assert
            Assert.Equal([typeof(ConstructMarkerPolicy)], Applying(options, definition));
        }

        // T053 - an option for the member is more specific than an option for the type
        [Fact]
        public void Applying_ForAnOptionForTheMemberAndOneForTheType_ItShould_BeTheMembersForThatMemberOnly()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Policies.Add<Log>(new SetMarkerPolicy());
            options.Policies.Add<Log>(nameof(Log.LogKey), new OtherSetMarkerPolicy());

            // Act
            TypeDefinition definition = Lock<Log>(options);

            // Assert
            Assert.Equal([typeof(OtherSetMarkerPolicy)], Applying(options, Member(definition, nameof(Log.LogKey))));
            Assert.Equal([typeof(SetMarkerPolicy)], Applying(options, Member(definition, nameof(Log.LogVolume))));
        }

        // T053 - an option for the type is more specific than an attribute on the member (options win)
        [Fact]
        public void Applying_ForAnOptionForTheTypeAndAnAttributeOnTheMember_ItShould_BeTheOption()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Policies.Add<LogWithSetMarkerOnKey>(new OtherSetMarkerPolicy());

            // Act
            TypeDefinition definition = Lock<LogWithSetMarkerOnKey>(options);

            // Assert
            Assert.Equal([typeof(OtherSetMarkerPolicy)], Applying(options, Member(definition, nameof(LogWithSetMarkerOnKey.LogKey))));
        }

        // T053 - R19: a member-level policy may only override Set
        [Fact]
        public void MakeReadOnly_ForAMemberPolicyOverridingConstruct_ItShould_ThrowNamingTheMemberTheClassAndThePolicy()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Rows.Add<Log>();
            options.Policies.Add<Log>(nameof(Log.LogKey), new ConstructMarkerPolicy());

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.MakeReadOnly);

            // Assert
            Assert.Contains(nameof(Log.LogKey), exception.Message);
            Assert.Contains(nameof(Log), exception.Message);
            Assert.Contains(nameof(ConstructMarkerPolicy), exception.Message);
        }

        // T089 - R19 (member inclusion): by default a public property and a public field are both members
        [Fact]
        public void IncludeMember_WithoutAPolicy_ItShould_IncludePropertiesAndFieldsAlike()
        {
            // Arrange
            TestingDeserializeOptions options = new();

            // Act
            TypeDefinition definition = Lock<LogWithExcludedMembers>(options);

            // Assert
            Assert.Equal(
                [nameof(LogWithExcludedMembers.ExcludedField), nameof(LogWithExcludedMembers.ExcludedProperty), nameof(LogWithExcludedMembers.LogKey)],
                definition.Properties.Select(property => property.Name).Order());
        }

        // T089 - R19 (member inclusion): a definition policy for the type leaves out a property and a field alike
        [Fact]
        public void IncludeMember_ForAnExcludingPolicy_ItShould_LeaveOutThePropertyAndTheField()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Policies.Add<LogWithExcludedMembers>(new ExcludingPolicy());

            // Act
            TypeDefinition definition = Lock<LogWithExcludedMembers>(options);

            // Assert
            Assert.Equal([nameof(LogWithExcludedMembers.LogKey)], definition.Properties.Select(property => property.Name));
        }
    }
}
