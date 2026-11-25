using Forestry.Serialization.Tests.Fixtures;
using Xunit;

namespace Forestry.Serialization.Tests
{
    /// <summary>
    /// Spec 001, User Story 2: work with telemetries without knowing their class (tasks.md T044, quickstart 4, FR-006)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4), so they do not compile until T047 declares the surface they
    /// assume. For the review gate (T046), the assumed surface: <c>Telemetry.Is&lt;T&gt;() where T : Telemetry</c>, true as
    /// C#'s <c>is</c> (the class or one deriving from it), and <c>Telemetry.AsType&lt;T&gt;() where T : Telemetry</c>, a
    /// checked cast returning the same instance and throwing <see cref="InvalidCastException"/> naming both
    /// classes (research R4). Telemetries are built directly: what is tested is <c>Telemetry</c>, not reading.
    /// </remarks>
    public sealed class TelemetryTests
    {
        // T044 - US2-1: a telemetry is its own class
        [Fact]
        public void Is_ForTheTelemetriesOwnClass_ItShould_BeTrue()
        {
            // Arrange
            Telemetry telemetry = new Log { LogKey = 1, LogVolume = 0.59m };

            // Act
            bool isLog = telemetry.Is<Log>();

            // Assert
            Assert.True(isLog);
        }

        // T044 - US2-1: a telemetry is not a different telemetry class
        [Fact]
        public void Is_ForADifferentClass_ItShould_BeFalse()
        {
            // Arrange
            Telemetry telemetry = new Log { LogKey = 1, LogVolume = 0.59m };

            // Act
            bool isStem = telemetry.Is<Stem>();

            // Assert
            Assert.False(isStem);
        }

        // T044 - US2-2: AsType returns the derived instance itself, with its values
        [Fact]
        public void AsType_ForTheTelemetriesOwnClass_ItShould_ReturnTheSameInstance()
        {
            // Arrange
            Telemetry telemetry = new Log { LogKey = 1, LogVolume = 0.59m };

            // Act
            Log log = telemetry.AsType<Log>();

            // Assert
            Assert.Same(telemetry, log);
            Assert.Equal(1, log.LogKey);
            Assert.Equal(0.59m, log.LogVolume);
        }

        // T044 - US2-3: AsType to a different class throws naming both classes
        [Fact]
        public void AsType_ForADifferentClass_ItShould_ThrowNamingBothClasses()
        {
            // Arrange
            Telemetry telemetry = new Log { LogKey = 1, LogVolume = 0.59m };

            // Act
            InvalidCastException exception = Assert.Throws<InvalidCastException>(() => telemetry.AsType<Stem>());

            // Assert
            Assert.Contains(nameof(Log), exception.Message);
            Assert.Contains(nameof(Stem), exception.Message);
        }

        // T044 - FR-006: a derived telemetry is also its base class, as C#'s is
        [Fact]
        public void IsAndAsType_ForABaseClass_ItShould_TreatTheDerivedTelemetryAsItsBase()
        {
            // Arrange
            Telemetry telemetry = new DerivedLog { LogKey = 1, LogVolume = 0.59m };

            // Act
            bool isBase = telemetry.Is<BaseLog>();
            BaseLog baseLog = telemetry.AsType<BaseLog>();

            // Assert
            Assert.True(isBase);
            Assert.Same(telemetry, baseLog);
            Assert.Equal(1, baseLog.LogKey);
        }
    }
}
