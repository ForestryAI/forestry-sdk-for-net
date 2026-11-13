using Forestry.Deserialize.Converters;
using Forestry.Deserialize.Tests.Fixtures;
using Xunit;

namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// Packaging gate (eng spec 001, constitution C2, tasks.md T024, T027): the cells of
    /// <c>eng/specs/001-ci-cd/contracts/packaging-gate.md</c> no existing test covers, for
    /// <see cref="DeserializeOptions.Rows"/>, <see cref="DeserializeOptions.Converters"/> and read-only
    /// </summary>
    public sealed class DeserializeOptionsTests
    {
        /// <summary>
        /// A row type that cannot be instantiated
        /// </summary>
        public abstract class AbstractRow : Row
        {
        }

        /// <summary>
        /// A converter for the member <see cref="LogWithNestedProperty"/> has no default for
        /// </summary>
        private sealed class MeasurementConverter : Converter<Measurement>
        {
            public override Measurement Convert(ReadOnlySpan<byte> utf8) => new();
        }

        #region Rows
        // Default: new options have no row types
        [Fact]
        public void Rows_Default_ItShould_BeEmpty()
        {
            // Arrange + Act
            TestingDeserializeOptions options = new();

            // Assert
            Assert.Empty(options.Rows);
        }

        // Override: Add chains, keeps the order added, and a repeated type is kept once
        [Fact]
        public void Rows_Override_ForSeveralTypesAndARepeat_ItShould_KeepTheOrderAddedOnce()
        {
            // Arrange
            TestingDeserializeOptions options = new();

            // Act
            options.Rows.Add<Stem>().Add(typeof(Log), typeof(Stem));

            // Assert
            Assert.Equal([typeof(Stem), typeof(Log)], options.Rows);
        }

        // Rejected: ArgumentException naming the type, like RowsAdd_ForATypeNotExtendingRow
        [Fact]
        public void Rows_Rejects_AnAbstractRowType()
        {
            // Arrange
            TestingDeserializeOptions options = new();

            // Act
            ArgumentException exception = Assert.Throws<ArgumentException>(() => options.Rows.Add(typeof(AbstractRow)));

            // Assert
            Assert.Contains(nameof(AbstractRow), exception.Message);
        }

        // Rejected: ArgumentNullException
        [Fact]
        public void Rows_Rejects_ANullArrayOfTypes()
        {
            // Arrange
            TestingDeserializeOptions options = new();

            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => options.Rows.Add(null!));
        }

        // Rejected: ArgumentNullException for a null among valid types
        [Fact]
        public void Rows_Rejects_ANullTypeInTheArray()
        {
            // Arrange
            TestingDeserializeOptions options = new();

            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => options.Rows.Add(typeof(Log), null!));
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
            options.Rows.Add<Log>();
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
            options.Rows.Add<Log>();

            // Act
            options.MakeReadOnly();
            options.MakeReadOnly();

            // Assert
            Assert.True(options.IsReadOnly);
        }

        // The options stay unlocked when a definition cannot be built, so the developer can add what
        // was missing: the converter resolved as "none" during the failed build must not be remembered
        // (developer's review at T026, 2026-11-10)
        [Fact]
        public void MakeReadOnly_AfterAFailedBuildAndAddingTheMissingConverter_ItShould_Lock()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Rows.Add<LogWithNestedProperty>();
            Assert.Throws<InvalidOperationException>(options.MakeReadOnly);
            Assert.False(options.IsReadOnly);

            // Act
            options.Converters.Add(new MeasurementConverter());
            options.MakeReadOnly();

            // Assert
            Assert.True(options.IsReadOnly);
        }
        #endregion

        #region MaxContextRows (spec 002)
        // T016 - FR-011a / research R28: kept rows allowed in the deserialization context, 100 by default
        [Fact]
        public void MaxContextRows_Default_ItShould_Be100()
        {
            // Arrange + Act
            TestingDeserializeOptions options = new();

            // Assert
            Assert.Equal(100, options.MaxContextRows);
        }

        // T016 - FR-011a: the limit is locked with the options (001 FR-013)
        [Fact]
        public void MaxContextRows_Rejects_ChangeAfterTheOptionsAreLocked()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Rows.Add<Log>();
            options.MakeReadOnly();

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => options.MaxContextRows = 10);
        }
        #endregion
    }
}
