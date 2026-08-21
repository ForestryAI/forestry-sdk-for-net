namespace Forestry.Deserialize.Xml {
    
    /// <summary>
    /// XML tokens are non-terminals and terminals 
    /// in the 3 document non-terminals i.e. prolog, markup, miscellaneous
    /// </summary>
    public enum TokenType: byte
    {
        None = (byte)0,

        #region prolog
        Declaration=(byte)1,
        DocumentType=(byte)2,
        #endregion

        #region markup
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