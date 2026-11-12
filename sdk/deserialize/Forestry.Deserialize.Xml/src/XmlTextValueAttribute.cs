namespace Forestry.Deserialize.Xml
{
    /// <summary>
    /// Marks the property or field of a row set only from the text directly inside the row's own element
    /// (spec 002 US3, research R30): <c>[XmlTextValue]</c> on <c>Price.Amount</c> reads <c>0.59</c> from
    /// <c>&lt;Price currency="EUR"&gt;0.59&lt;/Price&gt;</c>
    /// </summary>
    /// <remarks>
    /// At most one per row type; more fail when the options lock.
    /// <see cref="DeserializeXmlOptions.TextValue{TRow}(string)"/> wins over it. With text around child
    /// elements the last text wins; an element with no text sets the member empty (null where it allows
    /// it, else its default), which counts as set
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class XmlTextValueAttribute : Attribute
    {
    }
}
