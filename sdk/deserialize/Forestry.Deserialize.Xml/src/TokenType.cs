namespace Forestry.Deserialize.Xml {
    public enum TokenType: byte
    {
        StartingTag=(byte)0,
        EndingTag=(byte)1,
        EmptyTag=(byte)2,
        AttributeName=(byte)3,
        AttributeValue=(byte)4,
        Declaration=(byte)5,
        Comment=(byte)6,
        CharacterData=(byte)7,
        Null = (byte)8
    }
}