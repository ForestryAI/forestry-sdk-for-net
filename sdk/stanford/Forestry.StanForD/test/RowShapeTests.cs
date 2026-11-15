using System.Reflection;
using Xunit;

namespace Forestry.StanForD.Tests
{
    /// <summary>
    /// Spec 001, User Story 7: the shape of the rows that come out (tasks.md T058, T059; constitution C0, C3;
    /// spec FR-016, FR-017)
    /// </summary>
    public sealed class RowShapeTests
    {
        /// <summary>
        /// The rows that come out of a Harvesting document
        /// </summary>
        public static TheoryData<Type> Rows => new() { typeof(Stem), typeof(Log) };

        /// <summary>
        /// The public members a row type declares
        /// </summary>
        private static IEnumerable<PropertyInfo> Members(Type row) =>
            row.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        /// <summary>
        /// A single value (C0): a primitive, a single-value struct, an enum or text, or a nullable one of them
        /// </summary>
        private static bool IsSingleValue(Type type)
        {
            Type value = Nullable.GetUnderlyingType(type) ?? type;

            return value.IsPrimitive
                || value.IsEnum
                || value == typeof(string)
                || value == typeof(decimal)
                || value == typeof(Guid)
                || value == typeof(DateTime)
                || value == typeof(DateTimeOffset);
        }

        /// <summary>
        /// The spec's data model (<c>specs/001-harvesting-rows/data-model.md</c>), copied next to the tests by
        /// the test project, since the build output is outside the package folder
        /// </summary>
        private static string DataModel() =>
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data-model.md"));

        /// <summary>
        /// T058, quickstart 12, US7-5, C0, FR-016: every member of Stem and Log is a single value
        /// </summary>
        [Theory]
        [MemberData(nameof(Rows))]
        public void Row_ItShould_HaveOnlySingleValueMembers(Type row)
        {
            // Arrange, Act
            List<string> others = Members(row)
                .Where(member => !IsSingleValue(member.PropertyType))
                .Select(member => $"{member.Name} ({member.PropertyType.Name})")
                .ToList();

            // Assert
            Assert.Empty(others);
        }

        /// <summary>
        /// T059, C3, FR-017: every member of Stem and Log is named in the data model, which traces it to the
        /// query groups
        /// </summary>
        [Theory]
        [MemberData(nameof(Rows))]
        public void Row_ItShould_HaveEveryMemberInTheDataModel(Type row)
        {
            // Arrange
            string dataModel = DataModel();

            // Act
            List<string> missing = Members(row)
                .Select(member => member.Name)
                .Where(name => !dataModel.Contains($"`{name}`", StringComparison.Ordinal))
                .ToList();

            // Assert
            Assert.Empty(missing);
        }
    }
}
