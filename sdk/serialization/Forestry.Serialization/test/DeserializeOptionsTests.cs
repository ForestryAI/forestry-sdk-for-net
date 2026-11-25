using Forestry.Serialization.Tests.Fixtures;
using Xunit;

namespace Forestry.Serialization.Tests
{
    /// <summary>
    /// Packaging gate (eng spec 001, constitution C2, tasks.md T024, T027): the cells of
    /// <c>eng/specs/001-ci-cd/contracts/packaging-gate.md</c> no existing test covers, for
    /// <see cref="DeserializeOptions.Telemetries"/>, <see cref="DeserializeOptions.Converters"/> and read-only
    /// </summary>
    public sealed class DeserializeOptionsTests
    {
        /// <summary>
        /// A telemetry type that cannot be instantiated
        /// </summary>
        public abstract class AbstractTelemetry : Telemetry
        {
        }

        #region Telemetries
        // Default: new options have no telemetry types
        [Fact]
        public void Telemetries_Default_ItShould_BeEmpty()
        {
            // Arrange + Act
            TestingDeserializeOptions options = new();

            // Assert
            Assert.Empty(options.Telemetries);
        }

        // Override: Add chains, keeps the order added, and a repeated type is kept once
        [Fact]
        public void Telemetries_Override_ForSeveralTypesAndARepeat_ItShould_KeepTheOrderAddedOnce()
        {
            // Arrange
            TestingDeserializeOptions options = new();

            // Act
            options.Telemetries.Add<Stem>().Add(typeof(Log), typeof(Stem));

            // Assert
            Assert.Equal([typeof(Stem), typeof(Log)], options.Telemetries);
        }

        // Rejected: ArgumentException naming the type, like TelemetriesAdd_ForATypeNotExtendingTelemetry
        [Fact]
        public void Telemetries_Rejects_AnAbstractTelemetryType()
        {
            // Arrange
            TestingDeserializeOptions options = new();

            // Act
            ArgumentException exception = Assert.Throws<ArgumentException>(() => options.Telemetries.Add(typeof(AbstractTelemetry)));

            // Assert
            Assert.Contains(nameof(AbstractTelemetry), exception.Message);
        }

        // Rejected: ArgumentNullException
        [Fact]
        public void Telemetries_Rejects_ANullArrayOfTypes()
        {
            // Arrange
            TestingDeserializeOptions options = new();

            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => options.Telemetries.Add(null!));
        }

        // Rejected: ArgumentNullException for a null among valid types
        [Fact]
        public void Telemetries_Rejects_ANullTypeInTheArray()
        {
            // Arrange
            TestingDeserializeOptions options = new();

            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => options.Telemetries.Add(typeof(Log), null!));
        }
        #endregion

        #region Converters
        // Default: new options have no converters of the developer's
        [Fact]
        public void Converters_Default_ItShould_BeEmpty()
        {
            // Arrange + Act
            TestingDeserializeOptions options = new();

            // Assert
            Assert.Empty(options.Converters);
        }

        // Rejected: ArgumentNullException
        [Fact]
        public void Converters_Rejects_ANull()
        {
            // Arrange
            TestingDeserializeOptions options = new();

            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => options.Converters.Add(null!));
        }

        // Rejected: InvalidOperationException; Add after locking is covered by OptionsTests
        [Fact]
        public void Converters_Rejects_ClearAfterTheOptionsAreLocked()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Telemetries.Add<Log>();
            options.MakeReadOnly();

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => options.Converters.Clear());
        }
        #endregion

        #region Read-only
        // Default: new options are not read-only
        [Fact]
        public void IsReadOnly_Default_ItShould_BeFalse()
        {
            // Arrange + Act
            TestingDeserializeOptions options = new();

            // Assert
            Assert.False(options.IsReadOnly);
        }

        // Override: MakeReadOnly locks the options, and a second call does nothing
        [Fact]
        public void MakeReadOnly_Override_ItShould_LockAndBeIdempotent()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Telemetries.Add<Log>();

            // Act
            options.MakeReadOnly();
            options.MakeReadOnly();

            // Assert
            Assert.True(options.IsReadOnly);
        }

        // The options stay unlocked when a definition cannot be built, so the developer can add what
        // was missing: the converter resolved as "none" during the failed build must not be remembered
        // (developer's review at T026, 2026-11-10). Spec 003: the missing converter is for a single value
        // (FlatTelemetry's Measure); a converter for a nested class no longer helps (FR-003, research R33)
        [Fact]
        public void MakeReadOnly_AfterAFailedBuildAndAddingTheMissingConverter_ItShould_Lock()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Telemetries.Add<FlatTelemetry>();
            Assert.Throws<InvalidOperationException>(options.MakeReadOnly);
            Assert.False(options.IsReadOnly);

            // Act
            options.Converters.Add(new MeasureConverter());
            options.MakeReadOnly();

            // Assert
            Assert.True(options.IsReadOnly);
        }
        #endregion

        #region MaxContextTelemetries (spec 002)
        // T016 - FR-011a / research R28: kept telemetries allowed in the deserialization context, 100 by default
        [Fact]
        public void MaxContextTelemetries_Default_ItShould_Be100()
        {
            // Arrange + Act
            TestingDeserializeOptions options = new();

            // Assert
            Assert.Equal(100, options.MaxContextTelemetries);
        }

        // T016 - FR-011a: the limit is locked with the options (001 FR-013)
        [Fact]
        public void MaxContextTelemetries_Rejects_ChangeAfterTheOptionsAreLocked()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Telemetries.Add<Log>();
            options.MakeReadOnly();

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => options.MaxContextTelemetries = 10);
        }
        #endregion
    }
}
