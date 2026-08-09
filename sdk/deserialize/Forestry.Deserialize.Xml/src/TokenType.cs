namespace Forestry.Deserialize.Xml {
    public enum TokenType: byte
    {
        StartingTag=(byte)0,
        EndingTag=(byte)1,
        EmptyTag=(byte)2,
        ElementName=(byte)3,
        ElementValue=(byte)4,
        AttributeName=(byte)5,
        AttributeValue=(byte)6,
        Declaration=(byte)7,
        Comment=(byte)8,
        CharacterData=(byte)9,        
        Null = (byte)10
    }
}