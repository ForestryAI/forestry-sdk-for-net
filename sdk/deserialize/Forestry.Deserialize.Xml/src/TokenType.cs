namespace Forestry.Deserialize.Xml {
    public enum TokenType: byte
    {
        None = (byte)0,
        Null = (byte)11  // TODO: How to handle nulls e.g. xs:int
    }
}