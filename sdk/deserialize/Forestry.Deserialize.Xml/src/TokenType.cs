namespace Forestry.Deserialize.Xml {
    public enum TokenType: byte
    {
        None = (byte)0,
        Element=(byte)1,
        ElementEnd=(byte)2,
        Attribute=(byte)3,
        Value=(byte)4,
        Declaration=(byte)5,
        ProcessInstruction=(byte)6,
        Comment=(byte)7,
        DocumentType=(byte)8,
        Null = (byte)9
    }
}