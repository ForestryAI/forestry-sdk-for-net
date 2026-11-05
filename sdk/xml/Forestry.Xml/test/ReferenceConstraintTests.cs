using System.Text;
using Xunit;
using Forestry.Xml.Reading;
using static Forestry.Xml.Reading.Utf8XmlReader;

namespace Forestry.Xml.Tests
{
    /// <summary>
    /// <see cref="Utf8XmlReader.ReferenceConstraint"/> as a unit (tasks.md T011, #65): one
    /// character or entity reference evaluated from its <c>&amp;</c>, like
    /// <see cref="ElementStack"/> in <c>ElementNameStackTests</c>. Reader-level shells
    /// (<c>Utf8XmlReaderTests.ReadReferences.cs</c>) keep the line and position, the halts and
    /// the token.
    /// </summary>
    /// <remarks>
    /// Test shells written before the code (C4), reviewed at T017; they don't compile until T020.
    /// The shells assume the shape tasks.md T020 gives: <c>Evaluate(characters, out length, out
    /// reason)</c>, chunk by chunk with the state in the struct. Per chunk, <c>length</c> is the
    /// chunk's whole length when Unknown, the bytes through <c>;</c> when WellFormed, and the
    /// offending byte's index when Malformed, so the lengths summed over the chunks are the
    /// offset from the <c>&amp;</c>. Every row is evaluated whole and as two chunks split at
    /// every byte.
    /// </remarks>
    public class ReferenceConstraintTests
    {
        #region helpers
        /// <summary>
        /// The result of evaluating a reference: its markup state, the offset from the
        /// <c>&amp;</c>, the reason and the constraint afterwards
        /// </summary>
        private readonly record struct Evaluation(MarkupState State, int Offset, MarkupReason Reason, bool HasReferences, int ReferenceStart);

        private static Evaluation EvaluateChunks(byte[] bytes, int split, bool isDocumentType)
        {
            ReferenceConstraint constraint = new() { IsDocumentType = isDocumentType, MaxNameLength = 128 };
            int offset = 0;
            MarkupState state = MarkupState.Unknown;
            MarkupReason reason = MarkupReason.None;

            foreach (byte[] chunk in split < 0 ? new[] { bytes } : new[] { bytes[..split], bytes[split..] })
            {
                state = constraint.Evaluate(chunk, out int length, out reason);
                offset += length;

                if (state != MarkupState.Unknown)
                {
                    break;
                }
            }

            return new Evaluation(state, offset, reason, constraint.HasReferences, constraint.ReferenceStart);
        }

        /// <summary>
        /// Evaluate whole and split at every byte, asserting every way gives the same result, and
        /// return it
        /// </summary>
        private static Evaluation EvaluateEveryWay(string reference, bool isDocumentType = false)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(reference);
            Evaluation whole = EvaluateChunks(bytes, split: -1, isDocumentType);

            for (int split = 0; split <= bytes.Length; split++)
            {
                Assert.Equal(whole, EvaluateChunks(bytes, split, isDocumentType));
            }

            return whole;
        }
        #endregion

        #region well-formed
        /// <summary>
        /// The five predefined entity names, and character references, are well-formed through
        /// their <c>;</c>, with <c>HasReferences</c> set; <c>_entity</c> is chosen on the first
        /// byte (<c>l</c>, <c>g</c>, <c>q</c>) or on the second after <c>a</c> (<c>m</c>,
        /// <c>p</c>)
        /// </summary>
        [Theory]
        [InlineData("&amp;")]
        [InlineData("&lt;")]
        [InlineData("&gt;")]
        [InlineData("&quot;")]
        [InlineData("&apos;")]
        [InlineData("&#65;")]
        [InlineData("&#x41;")]
        [InlineData("&#xa;")]
        [InlineData("&#x1F332;")]
        [InlineData("&#0065;")]            // leading zeros are insignificant
        [InlineData("&#x0041;")]
        [InlineData("&#x0000000041;")]
        [InlineData("&#x10FFFF;")]         // the highest legal values
        [InlineData("&#1114111;")]
        [InlineData("&#x9;")]              // the Char boundaries
        [InlineData("&#xA;")]
        [InlineData("&#xD;")]
        [InlineData("&#x20;")]
        [InlineData("&#xD7FF;")]
        [InlineData("&#xE000;")]
        [InlineData("&#xFFFD;")]
        [InlineData("&#x10000;")]
        public void Evaluate_ForAWellFormedReference_ItShould_BeWellFormedThroughItsSemicolon(string reference)
        {
            // Act
            Evaluation evaluation = EvaluateEveryWay(reference);

            // Assert
            Assert.Equal(MarkupState.WellFormed, evaluation.State);
            Assert.Equal(reference.Length, evaluation.Offset);
            Assert.Equal(MarkupReason.None, evaluation.Reason);
            Assert.True(evaluation.HasReferences);
        }

        /// <summary>
        /// A predefined entity stays well-formed after a document type
        /// </summary>
        [Fact]
        public void Evaluate_ForAPredefinedEntityAfterADocumentType_ItShould_BeWellFormed()
        {
            // Act
            Evaluation evaluation = EvaluateEveryWay("&amp;", isDocumentType: true);

            // Assert
            Assert.Equal(MarkupState.WellFormed, evaluation.State);
            Assert.True(evaluation.HasReferences);
        }
        #endregion

        #region malformed
        /// <summary>
        /// contracts/exceptions.md "Story 1": each malformed reference at the first byte where it
        /// is known, as an offset from the <c>&amp;</c>
        /// </summary>
        [Theory]
        [InlineData("& ", 1)]                          // bare '&': the byte after it
        [InlineData("&amp<", 4)]                       // missing ';' before '<'
        [InlineData("&#65\"", 4)]                      // missing ';' before a quote
        [InlineData("&amp ", 4)]                       // missing ';' before spacing
        [InlineData("&;", 1)]                          // empty name
        [InlineData("&#;", 2)]                         // empty number
        [InlineData("&#x;", 3)]
        [InlineData("&#6A;", 3)]                       // a non-digit
        [InlineData("&#xG;", 3)]
        [InlineData("&#X41;", 2)]                      // an uppercase 'X'
        [InlineData("&#x110000;", 8)]                  // above U+10FFFF: the digit that takes it there
        [InlineData("&#99999999999999999999;", 8)]     // the 7th '9', never wrapping
        [InlineData("&#0;", 3)]                        // not a legal Char: at ';'
        [InlineData("&#x8;", 4)]
        [InlineData("&#x1F;", 5)]
        [InlineData("&#xD800;", 7)]
        [InlineData("&#xDFFF;", 7)]
        [InlineData("&#xFFFE;", 7)]
        [InlineData("&#xFFFF;", 7)]
        public void Evaluate_ForAMalformedReference_ItShould_BeMalformedAtTheFirstKnownByte(string reference, int offset)
        {
            // Act
            Evaluation evaluation = EvaluateEveryWay(reference);

            // Assert
            Assert.Equal(MarkupState.Malformed, evaluation.State);
            Assert.Equal(offset, evaluation.Offset);
            Assert.Equal(MarkupReason.MalformedReference, evaluation.Reason);
            Assert.False(evaluation.HasReferences);
        }

        /// <summary>
        /// Without a document type, an entity other than the five is undeclared at the first byte
        /// no predefined name accepts, <c>;</c> included
        /// </summary>
        [Theory]
        [InlineData("&nbsp;", 1)]
        [InlineData("&ampx;", 4)]
        [InlineData("&am;", 3)]
        [InlineData("&a;", 2)]
        [InlineData("&lx;", 2)]
        [InlineData("&gx;", 2)]
        [InlineData("&qx;", 2)]
        public void Evaluate_ForAnUndeclaredEntity_ItShould_BeMalformedAtTheFirstByteNoEntityAccepts(string reference, int offset)
        {
            // Act
            Evaluation evaluation = EvaluateEveryWay(reference);

            // Assert
            Assert.Equal(MarkupState.Malformed, evaluation.State);
            Assert.Equal(offset, evaluation.Offset);
            Assert.Equal(MarkupReason.UndeclaredEntity, evaluation.Reason);
        }

        /// <summary>
        /// After a document type, an entity name not fully matching a predefined one may be
        /// declared there: not supported, reported at the reference's <c>&amp;</c>
        /// </summary>
        [Theory]
        [InlineData("&e;")]
        [InlineData("&ampx;")]
        [InlineData("&am;")]
        public void Evaluate_ForAnOtherEntityAfterADocumentType_ItShould_BeNotSupportedAtTheAmpersand(string reference)
        {
            // Act
            Evaluation evaluation = EvaluateEveryWay(reference, isDocumentType: true);

            // Assert
            Assert.Equal(MarkupState.Malformed, evaluation.State);
            Assert.Equal(MarkupReason.NotSupportedEntity, evaluation.Reason);
            Assert.Equal(0, evaluation.ReferenceStart);
        }

        /// <summary>
        /// After a document type a missing <c>;</c> is still malformed
        /// </summary>
        [Fact]
        public void Evaluate_ForAnEntityMissingItsSemicolonAfterADocumentType_ItShould_BeMalformed()
        {
            // Act
            Evaluation evaluation = EvaluateEveryWay("&e<", isDocumentType: true);

            // Assert
            Assert.Equal(MarkupState.Malformed, evaluation.State);
            Assert.Equal(2, evaluation.Offset);
            Assert.Equal(MarkupReason.MalformedReference, evaluation.Reason);
        }
        #endregion

        #region unknown
        /// <summary>
        /// A chunk ending inside a reference is Unknown, its whole length consumed
        /// </summary>
        [Theory]
        [InlineData("&")]
        [InlineData("&am")]
        [InlineData("&#")]
        [InlineData("&#x1F3")]
        public void Evaluate_ForAChunkEndingInsideAReference_ItShould_BeUnknown(string reference)
        {
            // Arrange
            ReferenceConstraint constraint = new() { IsDocumentType = false, MaxNameLength = 128 };

            // Act
            MarkupState state = constraint.Evaluate(Encoding.UTF8.GetBytes(reference), out int length, out MarkupReason reason);

            // Assert
            Assert.Equal(MarkupState.Unknown, state);
            Assert.Equal(reference.Length, length);
            Assert.Equal(MarkupReason.None, reason);
            Assert.False(constraint.HasReferences);
        }
        #endregion
    }
}
