namespace Forestry.Xml {

    /// <summary>
    /// XML tokens are EBNF non-terminals inside a document ::= prolog element miscellaneous
    /// </summary>
    public enum TokenType: byte
    {
        /// <summary>No token read yet</summary>
        None = (byte)0,

        #region prolog
        /// <summary>The declaration <c>&lt;?xml … ?&gt;</c>, an opaque value</summary>
        Declaration=(byte)1,
        /// <summary>The document type <c>&lt;!DOCTYPE … &gt;</c>, an opaque value</summary>
        DocumentType=(byte)2,
        #endregion

        #region element
        /// <summary>A start tag, its value the element name</summary>
        Element=(byte)3,
        /// <summary>An end tag or the end of an empty element tag</summary>
        ElementEnd=(byte)4,
        /// <summary>An attribute name</summary>
        Attribute=(byte)5,
        /// <summary>An attribute value or character data</summary>
        Value=(byte)6,
        #endregion

        #region miscellaneous
        /// <summary>A processing instruction <c>&lt;? … ?&gt;</c>, an opaque value</summary>
        ProcessInstruction=(byte)7,
        /// <summary>A comment <c>&lt;!-- … --&gt;</c>, an opaque value</summary>
        Comment=(byte)8
        #endregion
    }
}