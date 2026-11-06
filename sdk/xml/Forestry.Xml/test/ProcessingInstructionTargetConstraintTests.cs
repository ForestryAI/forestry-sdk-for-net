using System.Text;
using Xunit;
using MarkupState = Forestry.Xml.Reading.Utf8XmlReader.MarkupState;
using ProcessingInstructionTargetConstraint = Forestry.Xml.Reading.Utf8XmlReader.ProcessingInstructionTargetConstraint;

namespace Forestry.Xml.Tests
{
    /// <summary>
    /// <see cref="Forestry.Xml.Reading.Utf8XmlReader.ProcessingInstructionTargetConstraint"/> as a
    /// unit (tasks.md T057, #67): <c>PITarget ::= Name - (('X' | 'x') ('M' | 'm') ('L' | 'l'))</c>,
    /// then spacing or <c>?&gt;</c>. The opaque read delimits the instruction at its first
    /// <c>?&gt;</c>; the constraint checks the target (research.md R8).
    /// </summary>
    /// <remarks>
    /// Test shells written before the code (C4), reviewed at T059; they don't compile until the
    /// T063 refactor. The constraint is fed the delimited value after <c>&lt;?</c>, whole and as two
    /// chunks split at every byte, and <c>OffendingIndex</c> counts from the first byte fed. The
    /// reader-level processing instruction tests in <c>Utf8XmlReaderTests.ReadOpaqueValue.cs</c>
    /// keep the line and position and nothing committed.
    /// </remarks>
    public class ProcessingInstructionTargetConstraintTests
    {
        #region helpers
        private readonly record struct Evaluation(MarkupState State, int OffendingIndex, string Category, string Markup);

        private static Evaluation EvaluateChunks(byte[] bytes, int split)
        {
            ProcessingInstructionTargetConstraint constraint = new() { MaxLength = 16 };
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
        /// A target followed by spacing or <c>?&gt;</c> is well-formed, also one that only starts
        /// like <c>xml</c>
        /// </summary>
        [Theory]
        [InlineData("pi?>")]
        [InlineData("pi data?>")]
        [InlineData("xmlx?>")]
        public void Evaluate_ForAWellFormedTarget_ItShould_BeWellFormed(string afterStartingTerminal)
        {
            // Act
            Evaluation evaluation = EvaluateEveryWay(afterStartingTerminal);

            // Assert
            Assert.Equal(MarkupState.WellFormed, evaluation.State);
        }

        /// <summary>
        /// An empty target, a first byte that cannot start a name, <c>?</c> not followed by
        /// <c>&gt;</c>, and <c>xml</c> in any letter case (at the target's start) break the naming
        /// rules
        /// </summary>
        [Theory]
        [InlineData("?>", 0)]          // empty target
        [InlineData(" pi?>", 0)]       // empty target before spacing
        [InlineData("1pi?>", 0)]       // a first byte that cannot start a name
        [InlineData("pi?x?>", 3)]      // '?' not followed by '>'
        [InlineData("xml ?>", 0)]      // xml, at the target's start
        [InlineData("XmL?>", 0)]
        public void Evaluate_ForAMalformedTarget_ItShould_BreakTheNamingRules(string afterStartingTerminal, int offendingIndex)
        {
            // Act
            Evaluation evaluation = EvaluateEveryWay(afterStartingTerminal);

            // Assert
            Assert.Equal(MarkupState.Malformed, evaluation.State);
            Assert.Equal(offendingIndex, evaluation.OffendingIndex);
            Assert.Equal("Name", evaluation.Category);
            Assert.Equal("ProcessingInstructionTarget", evaluation.Markup);
        }

        /// <summary>
        /// A target of <c>MaxLength + 1</c> bytes is a limit at index <c>MaxLength</c> (T045)
        /// </summary>
        [Fact]
        public void Evaluate_ForATargetBeyondItsMaxLength_ItShould_BeALimit()
        {
            // Act
            Evaluation evaluation = EvaluateEveryWay("N" + new string('a', 16) + "?>");

            // Assert
            Assert.Equal(MarkupState.Malformed, evaluation.State);
            Assert.Equal(16, evaluation.OffendingIndex);
            Assert.Equal("Limit", evaluation.Category);
            Assert.Equal("ProcessingInstructionTarget", evaluation.Markup);
        }
    }
}
