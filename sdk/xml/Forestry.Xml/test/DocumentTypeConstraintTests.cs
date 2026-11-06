using System.Text;
using Xunit;
using DocumentTypeConstraint = Forestry.Xml.Reading.Utf8XmlReader.DocumentTypeConstraint;
using MarkupState = Forestry.Xml.Reading.Utf8XmlReader.MarkupState;

namespace Forestry.Xml.Tests
{
    /// <summary>
    /// <see cref="Forestry.Xml.Reading.Utf8XmlReader.DocumentTypeConstraint"/> as a unit (tasks.md
    /// T057, #67): an internal subset (<c>'[' intSubset ']'</c>) is an opaque inside the opaque
    /// document type, not supported until delimiting it is built (FR-006, US1-8, option A)
    /// </summary>
    /// <remarks>
    /// Test shells written before the code (C4), reviewed at T059; they don't compile until the
    /// T063 refactor, which moves US1's internal-subset check into this constraint. It is fed the
    /// delimited value after <c>&lt;!DOCTYPE</c>, whole and as two chunks split at every byte, and
    /// <c>OffendingIndex</c> counts from the first byte fed. The reader-level shells are in
    /// <c>Utf8XmlReaderTests.ReadReferences.cs</c> (T012).
    /// </remarks>
    public class DocumentTypeConstraintTests
    {
        #region helpers
        private readonly record struct Evaluation(MarkupState State, int OffendingIndex, string Category, string Markup);

        private static Evaluation EvaluateChunks(byte[] bytes, int split)
        {
            DocumentTypeConstraint constraint = new();
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
        private static Evaluation EvaluateEveryWay(string afterStartingTerminal)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(afterStartingTerminal);
            Evaluation whole = EvaluateChunks(bytes, split: -1);

            for (int split = 0; split <= bytes.Length; split++)
            {
                Assert.Equal(whole, EvaluateChunks(bytes, split));
            }

            return whole;
        }
        #endregion

        /// <summary>
        /// A <c>[</c> outside quoted literals is an internal subset: not supported at the
        /// <c>[</c> (index 3 after <c>&lt;!DOCTYPE</c>, offset 12 from its <c>&lt;</c>)
        /// </summary>
        [Fact]
        public void Evaluate_ForAnInternalSubset_ItShould_BeNotSupported()
        {
            // Act
            Evaluation evaluation = EvaluateEveryWay(" a [<!ENTITY e \"x\">");

            // Assert
            Assert.Equal(MarkupState.Malformed, evaluation.State);
            Assert.Equal(3, evaluation.OffendingIndex);
            Assert.Equal("NotSupported", evaluation.Category);
            Assert.Equal("DocumentType", evaluation.Markup);
        }

        /// <summary>
        /// A document type without an internal subset is well-formed, a <c>[</c> inside a quoted
        /// literal included
        /// </summary>
        [Theory]
        [InlineData(" a>")]
        [InlineData(" a SYSTEM \"a[1].dtd\">")]
        [InlineData(" a PUBLIC '-//x[y]//EN' \"a.dtd\">")]
        public void Evaluate_ForNoInternalSubset_ItShould_BeWellFormed(string afterStartingTerminal)
        {
            // Act
            Evaluation evaluation = EvaluateEveryWay(afterStartingTerminal);

            // Assert
            Assert.Equal(MarkupState.WellFormed, evaluation.State);
        }
    }
}
