using System.Text;
using Xunit;
using DeclarationEncodingConstraint = Forestry.Xml.Reading.Utf8XmlReader.DeclarationEncodingConstraint;
using MarkupState = Forestry.Xml.Reading.Utf8XmlReader.MarkupState;

namespace Forestry.Xml.Tests
{
    /// <summary>
    /// <see cref="Forestry.Xml.Reading.Utf8XmlReader.DeclarationEncodingConstraint"/> as a unit
    /// (tasks.md T056, #67): the declaration's encoding name must be <c>UTF-8</c>, ignoring letter
    /// case (FR-014, research.md R8)
    /// </summary>
    /// <remarks>
    /// Test shells written before the code (C4), reviewed at T059; they don't compile until T064.
    /// The shells assume the shape tasks.md T063 gives <c>IConstraint</c>: <c>Evaluate</c> fed the
    /// delimited declaration after its starting terminal <c>&lt;?xml</c>, chunk by chunk with the
    /// state in the struct, then <c>OffendingIndex</c> (from the first byte fed), <c>Category</c>
    /// and <c>Markup</c>. Every row is fed whole and as two chunks split at every byte.
    /// </remarks>
    public class DeclarationEncodingConstraintTests
    {
        #region helpers
        private readonly record struct Evaluation(MarkupState State, int OffendingIndex, string Category, string Markup);

        private static Evaluation EvaluateChunks(byte[] bytes, int split, bool hasUtf8ByteOrderMark)
        {
            DeclarationEncodingConstraint constraint = new() { HasUtf8ByteOrderMark = hasUtf8ByteOrderMark };
            MarkupState state = MarkupState.Unknown;

            foreach (byte[] chunk in split < 0 ? new[] { bytes } : new[] { bytes[..split], bytes[split..] })
            {
                state = constraint.Evaluate(chunk);
                if (state != MarkupState.Unknown)
                {
                    break;
                }
            }

            return state == MarkupState.Malformed
                ? new Evaluation(state, constraint.OffendingIndex, constraint.Category.ToString(), constraint.Markup.ToString())
                : new Evaluation(state, 0, "", "");
        }

        /// <summary>
        /// Evaluate whole and split at every byte, asserting every way gives the same result, and
        /// return it
        /// </summary>
        private static Evaluation EvaluateEveryWay(string afterStartingTerminal, bool hasUtf8ByteOrderMark = false)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(afterStartingTerminal);
            Evaluation whole = EvaluateChunks(bytes, split: -1, hasUtf8ByteOrderMark);

            for (int split = 0; split <= bytes.Length; split++)
            {
                Assert.Equal(whole, EvaluateChunks(bytes, split, hasUtf8ByteOrderMark));
            }

            return whole;
        }

        /// <summary>
        /// Index of the encoding name's first byte: after the quote following <c>encoding</c>
        /// </summary>
        private static int NameStart(string afterStartingTerminal)
        {
            int encoding = afterStartingTerminal.IndexOf("encoding", StringComparison.Ordinal);
            return afterStartingTerminal.IndexOfAny(['"', '\''], encoding) + 1;
        }
        #endregion

        /// <summary>
        /// <c>UTF-8</c> in any letter case, either quote, spacing around <c>=</c>, and no encoding
        /// are well-formed
        /// </summary>
        [Theory]
        [InlineData(" version=\"1.0\" encoding=\"UTF-8\"?>")]
        [InlineData(" version=\"1.0\" encoding='utf-8'?>")]
        [InlineData(" version=\"1.0\" encoding=\"utf-8\"?>")]   // StanForD's
        [InlineData(" version=\"1.0\" encoding = \"Utf-8\"?>")]
        [InlineData(" version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>")]
        [InlineData(" version=\"1.0\"?>")]
        public void Evaluate_ForUtf8OrNoEncoding_ItShould_BeWellFormed(string afterStartingTerminal)
        {
            // Act
            Evaluation evaluation = EvaluateEveryWay(afterStartingTerminal);

            // Assert
            Assert.Equal(MarkupState.WellFormed, evaluation.State);
        }

        /// <summary>
        /// Another name throws "not supported" at its first byte, including names that differ
        /// from <c>UTF-8</c> only by a byte that <c>| 0x20</c> would fold into <c>-</c> or
        /// <c>8</c> (<c>\r</c>, <c>\x18</c>), a longer or a shorter name, and an empty one
        /// </summary>
        [Theory]
        [InlineData(" version=\"1.0\" encoding=\"ISO-8859-1\"?>")]
        [InlineData(" version=\"1.0\" encoding=\"UTF-16\"?>")]
        [InlineData(" version=\"1.0\" encoding=\"UTF8\"?>")]
        [InlineData(" version=\"1.0\" encoding=\"\"?>")]
        [InlineData(" version=\"1.0\" encoding=\"UTF\r8\"?>")]
        [InlineData(" version=\"1.0\" encoding=\"UTF-\u00188\"?>")]
        [InlineData(" version=\"1.0\" encoding=\"UTF-8X\"?>")]
        [InlineData(" version=\"1.0\" encoding=\"UTF-\"?>")]
        public void Evaluate_ForAnotherEncoding_ItShould_BeNotSupportedAtTheName(string afterStartingTerminal)
        {
            // Act
            Evaluation evaluation = EvaluateEveryWay(afterStartingTerminal);

            // Assert
            Assert.Equal(MarkupState.Malformed, evaluation.State);
            Assert.Equal(NameStart(afterStartingTerminal), evaluation.OffendingIndex);
            Assert.Equal("NotSupported", evaluation.Category);
            Assert.Equal("Encoding", evaluation.Markup);
        }

        /// <summary>
        /// After a skipped UTF-8 BOM another name contradicts it (XML 1.0 §4.3.3): Expected +
        /// Encoding at the name, not "not supported" (FR-014, developer 2026-11-06)
        /// </summary>
        [Theory]
        [InlineData(" version=\"1.0\" encoding=\"ISO-8859-1\"?>")]
        [InlineData(" version=\"1.0\" encoding=\"UTF-16\"?>")]
        public void Evaluate_ForAnotherEncodingAfterAUtf8ByteOrderMark_ItShould_BeExpectedAtTheName(string afterStartingTerminal)
        {
            // Act
            Evaluation evaluation = EvaluateEveryWay(afterStartingTerminal, hasUtf8ByteOrderMark: true);

            // Assert
            Assert.Equal(MarkupState.Malformed, evaluation.State);
            Assert.Equal(NameStart(afterStartingTerminal), evaluation.OffendingIndex);
            Assert.Equal("Expected", evaluation.Category);
            Assert.Equal("Encoding", evaluation.Markup);
        }

        /// <summary>
        /// After a skipped UTF-8 BOM, <c>UTF-8</c> in any case, or no encoding, is well-formed
        /// </summary>
        [Theory]
        [InlineData(" version=\"1.0\" encoding=\"utf-8\"?>")]
        [InlineData(" version=\"1.0\"?>")]
        public void Evaluate_ForUtf8AfterAUtf8ByteOrderMark_ItShould_BeWellFormed(string afterStartingTerminal)
        {
            // Act
            Evaluation evaluation = EvaluateEveryWay(afterStartingTerminal, hasUtf8ByteOrderMark: true);

            // Assert
            Assert.Equal(MarkupState.WellFormed, evaluation.State);
        }

        /// <summary>
        /// Text after <c>encoding</c> without the <c>S? '=' S?</c> quote shape, or
        /// <c>encoding</c> not preceded by spacing, throws nothing: the rest of the declaration
        /// stays unchecked
        /// </summary>
        [Theory]
        [InlineData(" version=\"1.0\" encoding UTF-16?>")]
        [InlineData(" version=\"1.0\" xencoding=\"x\"?>")]
        public void Evaluate_ForAnotherShape_ItShould_BeWellFormed(string afterStartingTerminal)
        {
            // Act
            Evaluation evaluation = EvaluateEveryWay(afterStartingTerminal);

            // Assert
            Assert.Equal(MarkupState.WellFormed, evaluation.State);
        }
    }
}
