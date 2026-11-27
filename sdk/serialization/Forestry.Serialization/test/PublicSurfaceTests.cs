using System.Reflection;
using Xunit;

namespace Forestry.Serialization.Tests
{
    /// <summary>
    /// Spec 003, US3 (#128): the public surface keeps none of the removed or replaced names (tasks.md
    /// T033, SC-002)
    /// </summary>
    /// <remarks>
    /// Test shell written before any code (C4). For the review gate (T034): after US2 it fails only for the
    /// names containing <c>Media</c> (<c>MediaTelemetryEnumerator</c>, <c>MediaTelemetryAsyncEnumerator</c>,
    /// <c>MediaConverters</c>), which US3 renames after "format" (T035, T036).
    /// </remarks>
    public sealed class PublicSurfaceTests
    {
        private static readonly string[] s_removedTypeNames = ["Field", "TelemetryAttribute", "FieldAttribute"];

        private const BindingFlags Visible = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        /// <summary>
        /// Types and members a developer or a format package author can see: exported types, and their public
        /// and protected (family or family-or-assembly) members
        /// </summary>
        private static IEnumerable<string> VisibleNames()
        {
            foreach (Type type in typeof(Telemetry).Assembly.GetExportedTypes())
            {
                yield return type.FullName!;

                foreach (MemberInfo member in type.GetMembers(Visible))
                {
                    if (IsVisible(member))
                    {
                        yield return $"{type.FullName}.{member.Name}";
                    }
                }
            }
        }

        private static bool IsVisible(MemberInfo member) => member switch
        {
            MethodBase method => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly,
            FieldInfo field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly,
            PropertyInfo property => (property.GetMethod ?? property.SetMethod) is { } accessor && IsVisible(accessor),
            EventInfo @event => @event.AddMethod is { } add && IsVisible(add),
            Type nested => nested.IsNestedPublic || nested.IsNestedFamily || nested.IsNestedFamORAssem,
            _ => false
        };

        // T033 - SC-002 (quickstart 10): no type named Field, TelemetryAttribute or FieldAttribute (removed in US2), and
        // no type or member whose name contains Media (renamed after "format" in US3)
        [Fact]
        public void PublicSurface_ForTheDeserializationPackage_ItShould_HaveNoRemovedOrMediaNames()
        {
            // Arrange
            List<string> names = [.. VisibleNames()];

            // Act
            List<string> removed = [.. typeof(Telemetry).Assembly.GetExportedTypes().Where(type => s_removedTypeNames.Contains(type.Name)).Select(type => type.FullName!)];
            List<string> media = [.. names.Where(name => name.Contains("Media", StringComparison.Ordinal))];

            // Assert
            Assert.Empty(removed);
            Assert.Empty(media);
        }

        // Spec 005 T023 (contracts/public-api.md): the public surface US1 adds
        [Theory]
        [InlineData("Forestry.Serialization.EnumMemberNameAttribute")]
        [InlineData("Forestry.Serialization.EnumMemberNameAttribute.Name")]
        [InlineData("Forestry.Serialization.Converters.EnumForm")]
        [InlineData("Forestry.Serialization.Converters.EnumMember`1")]
        [InlineData("Forestry.Serialization.Converters.EnumConverter`1")]
        [InlineData("Forestry.Serialization.Converters.EnumConverter`1.Form")]
        [InlineData("Forestry.Serialization.Converters.EnumConverter`1.Members")]
        [InlineData("Forestry.Serialization.Converters.EnumConverter`1.TryGetValue")]
        [InlineData("Forestry.Serialization.Converters.EnumConverter`1.TryGetUtf8Name")]
        [InlineData("Forestry.Serialization.Converters.ConverterFactory.UsesMember")]
        [InlineData("Forestry.Serialization.Writing.TelemetryWriter.WriteEnumName")]
        public void PublicSurface_ForSpec005US1_ItShould_IncludeTheContractsNames(string name)
        {
            // Arrange + Act
            List<string> names = [.. VisibleNames()];

            // Assert
            Assert.Contains(name, names);
        }
    }
}
