using Xunit;

namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// Spec 003, US2 (#127): where a value is read in the document, generic and in the top namespace
    /// (tasks.md T023, FR-009, research R36)
    /// </summary>
    /// <remarks>
    /// Test shell written before any code (C4). For the review gate (T024), the assumed surface:
    /// <c>Forestry.Deserialize.ReadingInformation</c> (spec 002 <c>Forestry.Deserialize.Reading.FieldContext</c>),
    /// a <c>readonly ref struct</c> constructed from a depth, a name and a path, with <c>Depth</c>, <c>Name</c>
    /// and <c>Path</c>; and <c>Forestry.Deserialize.Utf8Extensions.ToUtf8String</c> (spec 002
    /// <c>FieldContextExtensions</c>) over a UTF-8 span.
    /// </remarks>
    public sealed class ReadingInformationTests
    {
        // T023 - US2-6 / FR-009: depth, name and path as given, the name and path readable as strings
        [Fact]
        public void ReadingInformation_ForADepthNameAndPath_ItShould_ReportThem()
        {
            // Arrange
            ReadingInformation information = new(2, "Log"u8, "/Root/Log"u8);

            // Act
            string name = information.Name.ToUtf8String();
            string path = information.Path.ToUtf8String();

            // Assert
            Assert.Equal(2, information.Depth);
            Assert.Equal("Log", name);
            Assert.Equal("/Root/Log", path);
        }
    }
}
