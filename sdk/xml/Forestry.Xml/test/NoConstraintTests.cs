using System.Text;
using Xunit;
using MarkupState = Forestry.Xml.Reading.Utf8XmlReader.MarkupState;
using NoConstraint = Forestry.Xml.Reading.Utf8XmlReader.NoConstraint;

namespace Forestry.Xml.Tests
{
    /// <summary>
    /// <see cref="Forestry.Xml.Reading.Utf8XmlReader.NoConstraint"/> as a unit (tasks.md T057,
    /// #67): the constraint of an opaque value with nothing to check (a comment; the declaration
    /// until T064), always well-formed
    /// </summary>
    /// <remarks>
    /// Test shell written before the code (C4), reviewed at T059; it doesn't compile until the
    /// T063 refactor. Fed whole and as two chunks split at every byte.
    /// </remarks>
    public class NoConstraintTests
    {
        /// <summary>
        /// Well-formed for any bytes, however they are split
        /// </summary>
        [Theory]
        [InlineData(" anything at all -->")]
        [InlineData("<>[]\"'?")]
        public void Evaluate_ForAnyBytes_ItShould_BeWellFormed(string afterStartingTerminal)
        {
            // Arrange
            byte[] bytes = Encoding.UTF8.GetBytes(afterStartingTerminal);

            for (int split = -1; split <= bytes.Length; split++)
            {
                NoConstraint constraint = new();
                MarkupState state = MarkupState.Unknown;

                // Act
                foreach (byte[] chunk in split < 0 ? new[] { bytes } : new[] { bytes[..split], bytes[split..] })
                {
                    state = constraint.Evaluate(chunk);
                    if (state != MarkupState.Unknown)
                    {
                        break;
                    }
                }

                // Assert
                Assert.Equal(MarkupState.WellFormed, state);
            }
        }
    }
}
