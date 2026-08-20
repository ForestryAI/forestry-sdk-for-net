namespace Forestry.Deserialize.Xml {
    public enum TokenType: byte
    {
        None = (byte)0,
        #region markup
        Element=(byte)1,
        ElementEnd=(byte)2,
        Attribute=(byte)3,
        Value=(byte)4,
        #endregion
        #region prolog
        Declaration=(byte)5,
        DocumentType=(byte)6,
        #endregion
        #region miscellaneous
        ProcessInstruction=(byte)7,
        Comment=(byte)8
        #endregion
    }
}