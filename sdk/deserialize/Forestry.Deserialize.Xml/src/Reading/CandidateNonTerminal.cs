using static Forestry.Deserialize.Xml.EBNF;

namespace Forestry.Deserialize.Xml.Reading
{
    /// <summary>
    /// CandidateNonTerminal non-terminal that a read value delegate can operate on (#27)
    /// </summary>
    internal enum NonTerminal : byte
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
        /// <c>Start Tag</c> breaks from the EBNF XML grammar because the element and empty 
        /// element non-terminals both begin with the same sequence of terminals and 
        /// non-terminals equal to the start tag non-terminal
        /// </summary>
        StartTag = (byte)5,

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
    internal readonly struct CandidateNonTerminal
    {
        /// <summary>
        /// CandidateNonTerminal non-terminal
        /// </summary>
        public NonTerminal NonTerminal { get; }

        /// <summary>
        /// Deconstruct the scratch pad and the reader state into a candidate
        /// </summary>
        /// <param name="scratchPad">Characters peeked into the scratch pad</param>
        /// <param name="elementStack">Element stack the context is derived from</param>
        /// <param name="currentTokenType">Current token, separating a declaration from a processing instruction</param>
        /// <remarks>
        /// Only the reader state the deconstruction needs is passed, never the whole
        /// <see cref="ReaderState"/>: the reader's <c>ReaderState</c> property builds a new
        /// reader state on every call, copying the element stack's inline names (about 2 KB),
        /// which read delegation would pay on every read.  The element stack is passed by
        /// <c>in</c>, i.e. a read-only reference to the reader's own field, and the members read
        /// from it (<see cref="ElementStack.Depth"/>, <see cref="ElementStack.RootElement"/> and
        /// <see cref="ElementStack.ContentReady"/>) are <c>readonly</c>, so the compiler makes no
        /// defensive copies of it either.  Keep any member added to that list <c>readonly</c>.
        /// </remarks>
        public CandidateNonTerminal(ReadOnlySpan<byte> scratchPad, in ElementStack elementStack, TokenType currentTokenType)
        {
            // Context derived from the reader state (#27's context table)
            int depth = elementStack.Depth;
            bool rootElement = elementStack.RootElement;
            bool contentReady = elementStack.ContentReady;

            bool prolog = depth == 0 && !rootElement;
            bool startTag = depth != 0 && !contentReady;
            bool content = depth != 0 && contentReady;
            bool miscellaneous = depth == 0 && rootElement;

            // Ordered assertions, the first match wins
            NonTerminal = scratchPad switch
            {
                // S0
                [LessThan, QuestionMark, (byte)'x', (byte)'m', (byte)'l', Space or Tab or CarriageReturn or LineFeed, ..]
                    when prolog && currentTokenType == TokenType.None
                    => NonTerminal.Declaration,

                // S1
                [LessThan, ExclamationMark, (byte)'D', (byte)'O', (byte)'C', (byte)'T', (byte)'Y', (byte)'P', (byte)'E', ..]
                    when prolog
                    => NonTerminal.DocumentType,

                // S2
                [LessThan, ExclamationMark, Hyphen, Hyphen, ..]
                    when prolog || content || miscellaneous
                    => NonTerminal.Comment,

                // S3
                [LessThan, QuestionMark, ..]
                    when prolog || content || miscellaneous
                    => NonTerminal.ProcessingInstruction,

                // S4
                [LessThan, var character, ..]
                    when (prolog || content) && IsNameStartingCharacter(character)
                    => NonTerminal.StartTag,

                // S5
                [LessThan, Slash, ..]
                    when content
                    => NonTerminal.EndTag,

                // S6
                []
                    when startTag
                    => NonTerminal.Attribute,

                // S7
                [DoubleQuote or SingleQuote, ..]
                    when startTag
                    => NonTerminal.AttributeValue,

                // S8
                [] or [DoubleQuote or SingleQuote, ..]
                    when content
                    => NonTerminal.CharacterData,

                // S9
                _ => NonTerminal.None,
            };
        }

        /// <summary>
        /// Deconstruct into the candidate non-terminal
        /// </summary>
        /// <param name="nonTerminal"></param>
        /// <param name="startingTerminalLength"></param>
        public void Deconstruct(out NonTerminal nonTerminal)
        {
            nonTerminal = NonTerminal;
        }
    }
}
