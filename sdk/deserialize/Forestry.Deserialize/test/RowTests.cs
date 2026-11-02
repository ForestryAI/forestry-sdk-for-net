using Forestry.Deserialize.Tests.Fixtures;
using Xunit;

namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// Spec 001, User Story 2: work with rows without knowing their class (tasks.md T044, quickstart 4, FR-006)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4), so they do not compile until T047 declares the surface they
    /// assume. For the review gate (T046), the assumed surface: <c>Row.Is&lt;T&gt;() where T : Row</c>, true as
    /// C#'s <c>is</c> (the class or one deriving from it), and <c>Row.AsType&lt;T&gt;() where T : Row</c>, a
    /// checked cast returning the same instance and throwing <see cref="InvalidCastException"/> naming both
    /// classes (research R4). Rows are built directly: what is tested is <c>Row</c>, not reading.
    /// </remarks>
    public sealed class RowTests
    {
        // T044 - US2-1: a row is its own class
        [Fact]
        public void Is_ForTheRowsOwnClass_ItShould_BeTrue()
        {
            // Arrange
            Row row = new Log { LogKey = 1, LogVolume = 0.59m };

            // Act
            bool isLog = row.Is<Log>();

            // Assert
            Assert.True(isLog);
        }

        // T044 - US2-1: a row is not a different row class
        [Fact]
        public void Is_ForADifferentClass_ItShould_BeFalse()
        {
            // Arrange
            Row row = new Log { LogKey = 1, LogVolume = 0.59m };

            // Act
            bool isStem = row.Is<Stem>();

            // Assert
            Assert.False(isStem);
        }

        // T044 - US2-2: AsType returns the derived instance itself, with its values
        [Fact]
        public void AsType_ForTheRowsOwnClass_ItShould_ReturnTheSameInstance()
        {
            // Arrange
            Row row = new Log { LogKey = 1, LogVolume = 0.59m };

            // Act
            Log log = row.AsType<Log>();

            // Assert
            Assert.Same(row, log);
            Assert.Equal(1, log.LogKey);
            Assert.Equal(0.59m, log.LogVolume);
        }

        // T044 - US2-3: AsType to a different class throws naming both classes
        [Fact]
        public void AsType_ForADifferentClass_ItShould_ThrowNamingBothClasses()
        {
            // Arrange
            Row row = new Log { LogKey = 1, LogVolume = 0.59m };

            // Act
            InvalidCastException exception = Assert.Throws<InvalidCastException>(() => row.AsType<Stem>());

            // Assert
            Assert.Contains(nameof(Log), exception.Message);
            Assert.Contains(nameof(Stem), exception.Message);
        }

        // T044 - FR-006: a derived row is also its base class, as C#'s is
        [Fact]
        public void IsAndAsType_ForABaseClass_ItShould_TreatTheDerivedRowAsItsBase()
        {
            // Arrange
            Row row = new DerivedLog { LogKey = 1, LogVolume = 0.59m };

            // Act
            bool isBase = row.Is<BaseLog>();
            BaseLog baseLog = row.AsType<BaseLog>();

            // Assert
            Assert.True(isBase);
            Assert.Same(row, baseLog);
            Assert.Equal(1, baseLog.LogKey);
        }
    }
}
