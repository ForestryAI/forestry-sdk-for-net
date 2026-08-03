public ref partial struct Utf8XmlReader
{
    /// <summary>
    /// Reads next element || attribute
    /// </summary>
    /// <returns></returns>
    public bool Read()
    {
        return false;
    }

     /// <summary>
     /// Skip current element || attribute
     /// </summary>
    public void Skip()
    {}

    #region Get
    public string? GetString()
    {
        // TODO: Assert type is string
        return null;
    }
    #endregion
}