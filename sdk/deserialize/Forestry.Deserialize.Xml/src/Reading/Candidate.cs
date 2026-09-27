using static Forestry.Deserialize.Xml.EBNF;

namespace Forestry.Deserialize.Xml.Reading
{
    /// <summary>
    /// Candidate non-terminal that a read value delegate can operate on (#27)
    /// </summary>
    internal enum CandidateNonTerminal : byte
    {
        /// <summary>
        /// No candidate in the context, delegated to the default delegate (S9)
        /// </summary>
        None = (byte)0,

        /// <summary>
        /// <c>XMLDecl</c>
        /// </summary>
        Declaration = (byte)1,

        /// <summary>
        /// <c>doctypedecl</c>
        /// </summary>
        DocumentType = (byte)2,

        /// <summary>
        /// <c>Comment</c>
        /// </summary>
        Comment = (byte)3,

        /// <summary>
        /// <c>PI</c>
        /// </summary>
        ProcessingInstruction = (byte)4,

        /// <summary>
        /// <c>element</c>, whether <c>STag content ETag</c> or <c>EmptyElemTag</c> is the
        /// delegate's concern
        /// </summary>
        Element = (byte)5,

        /// <summary>
        /// <c>ETag</c>
        /// </summary>
        EndTag = (byte)6,

        /// <summary>
        /// <c>Attribute</c>
        /// </summary>
        Attribute = (byte)7,

        /// <summary>
        /// <c>AttValue</c>
        /// </summary>
        AttributeValue = (byte)8,

        /// <summary>
        /// <c>CharData</c>
        /// </summary>
        CharacterData = (byte)9,
    }

    /// <summary>
    /// Deconstruction of the characters peeked into the scratch pad, together with the reader
    /// state, into a candidate non-terminal (#27's delegation table S0-S9)
    /// </summary>
    /// <remarks>
    /// The scratch pad is only read by the constructor, never stored, so the candidate is a
    /// plain struct without side effects on the reader.  The starting terminal is
    /// <c>scratchPad[..StartingTerminalLength]</c> and the extra is the rest.  The extra is
    /// checked against the grammar only to choose between candidates, never to validate the
    /// non-terminals after the starting terminal.  The candidate is the outermost non-terminal
    /// that starts at the scratch pad, e.g. <c>element</c> rather than <c>STag</c>.
    /// <para>
    /// The scratch pad either begins with the first character of an allowed starting terminal
    /// or is empty (#25).  An empty scratch pad is deconstructed by the context alone: an
    /// attribute in a start tag, character data in content and no candidate otherwise.  In a
    /// start tag the steps before delegation consume every other well-formed character
    /// (spacing, <c>&gt;</c>, <c>Eq</c> and <c>/&gt;</c>), so an attribute is the only
    /// well-formed possibility; anything else is malformed and left to the delegate.
    /// </para>
    /// </remarks>
    internal readonly struct Candidate
    {
        /// <summary>
        /// Candidate non-terminal
        /// </summary>
        public CandidateNonTerminal NonTerminal { get; }

        /// <summary>
        /// Characters in the scratch pad belonging to the candidate's starting terminal
        /// </summary>
        public int StartingTerminalLength { get; }

        /// <summary>
        /// Deconstruct the scratch pad and the reader state into a candidate
        /// </summary>
        /// <param name="scratchPad">Characters peeked into the scratch pad</param>
        /// <param name="readerState">Reader state the context is derived from</param>
        public Candidate(ReadOnlySpan<byte> scratchPad, ReaderState readerState)
        {
            // Context derived from the reader state (#27's context table)
            int depth = readerState._elementStack.Depth;
            bool rootElement = readerState._elementStack.RootElement;
            bool contentReady = readerState._elementStack.ContentReady;

            bool prolog = depth == 0 && !rootElement;
            bool startTag = depth != 0 && !contentReady;
            bool content = depth != 0 && contentReady;
            bool miscellaneous = depth == 0 && rootElement;

            // Ordered assertions, the first match wins
            (NonTerminal, StartingTerminalLength) = scratchPad switch
            {
                // S0
                [LessThan, QuestionMark, (byte)'x', (byte)'m', (byte)'l', Space or Tab or CarriageReturn or LineFeed, ..]
                    when prolog && readerState._currentTokenType == TokenType.None
                    => (CandidateNonTerminal.Declaration, 5),

                // S1
                [LessThan, ExclamationMark, (byte)'D', (byte)'O', (byte)'C', (byte)'T', (byte)'Y', (byte)'P', (byte)'E', ..]
                    when prolog
                    => (CandidateNonTerminal.DocumentType, 9),

                // S2
                [LessThan, ExclamationMark, Hyphen, Hyphen, ..]
                    when prolog || content || miscellaneous
                    => (CandidateNonTerminal.Comment, 4),

                // S3
                [LessThan, QuestionMark, ..]
                    when prolog || content || miscellaneous
                    => (CandidateNonTerminal.ProcessingInstruction, 2),

                // S4
                [LessThan, var character, ..]
                    when (prolog || content) && IsNameStartingCharacter(character)
                    => (CandidateNonTerminal.Element, 1),

                // S5
                [LessThan, Slash, ..]
                    when content
                    => (CandidateNonTerminal.EndTag, 2),

                // S6
                []
                    when startTag
                    => (CandidateNonTerminal.Attribute, 0),

                // S7
                [DoubleQuote or SingleQuote, ..]
                    when startTag
                    => (CandidateNonTerminal.AttributeValue, 1),

                // S8
                [] or [DoubleQuote or SingleQuote, ..]
                    when content
                    => (CandidateNonTerminal.CharacterData, 0),

                // S9
                _ => (CandidateNonTerminal.None, 0),
            };
        }

        /// <summary>
        /// Deconstruct into the candidate non-terminal and its starting terminal length
        /// </summary>
        /// <param name="nonTerminal"></param>
        /// <param name="startingTerminalLength"></param>
        public void Deconstruct(out CandidateNonTerminal nonTerminal, out int startingTerminalLength)
        {
            nonTerminal = NonTerminal;
            startingTerminalLength = StartingTerminalLength;
        }
    }
}
