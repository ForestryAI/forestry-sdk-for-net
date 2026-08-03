namespace Forestry.Deserialize.Xml.Reading
{
    internal ref partial struct Utf8XmlReader
    {
        public readonly string? GetString()
        {
            if (_tokenType == TokenType.Null)
            {
                return null;
            }

            return string.Empty;
        }
    }
}