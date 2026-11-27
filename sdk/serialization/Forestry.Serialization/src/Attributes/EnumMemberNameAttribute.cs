namespace Forestry.Serialization
{
    /// <summary>
    /// The name an enum member is read from and written as, in every format and both directions, in place of
    /// its C# name, e.g. a StanForD text with spaces: <c>[EnumMemberName("Other manual")] OtherManual</c>
    /// (spec 005 research R62)
    /// </summary>
    /// <remarks>
    /// Names are matched ignoring case when read, so they must be unique ignoring case within the enum;
    /// otherwise the options fail when their definitions are built. The attribute names a value, not a part
    /// of a document or an output (constitution A5).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class EnumMemberNameAttribute : Attribute
    {
        /// <summary>
        /// Name the member <paramref name="name"/>
        /// </summary>
        /// <param name="name">The member's name; not null or empty</param>
        public EnumMemberNameAttribute(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            Name = name;
        }

        /// <summary>
        /// The member's name
        /// </summary>
        public string Name { get; }
    }
}
