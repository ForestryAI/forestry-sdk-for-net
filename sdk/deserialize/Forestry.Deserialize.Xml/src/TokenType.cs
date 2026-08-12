namespace Forestry.Deserialize.Xml {
    public enum TokenType: byte
    {
        None = (byte)0,
        StartingTag=(byte)1,
        EndingTag=(byte)2,
        EmptyTag=(byte)3,
        ElementName=(byte)4,
        ElementValue=(byte)5,
        AttributeName=(byte)6,
        AttributeValue=(byte)7,
        Declaration=(byte)8,
        Comment=(byte)9,
        CharacterData=(byte)10,
        Null = (byte)11
    }
}