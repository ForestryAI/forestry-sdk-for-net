namespace Forestry.Deserialize.Xml {
    
    /// <summary>
    /// XML tokens are EBNF non-terminals inside a document ::= prolog element miscellaneous
    /// </summary>
    public enum TokenType: byte
    {
        None = (byte)0,

        #region prolog
        Declaration=(byte)1,
        DocumentType=(byte)2,
        #endregion

        #region element
        Element=(byte)3,
        ElementEnd=(byte)4,
        Attribute=(byte)5,
        Value=(byte)6,
        #endregion

        #region miscellaneous
        ProcessInstruction=(byte)7,
        Comment=(byte)8
        #endregion
    }
}