using System.IO.Pipelines;
using Forestry.Deserialize.Converters;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Xml
{
    /// <summary>
    /// Options of the XML media: the row types to shred, converters and settings. Associates elements
    /// with row types and members by name, or by <see cref="RowAttribute"/> and
    /// <see cref="FieldAttribute"/> names (research R6, R8)
    /// </summary>
    public sealed class DeserializeXmlOptions : DeserializeOptions
    {
        private static readonly Converter[] s_mediaConverters = [new XmlBooleanConverter()];

        private XmlAssociation? _association;

        private Dictionary<Type, string>? _textValues;

        /// <summary>
        /// Set <typeparamref name="TRow"/>'s member <paramref name="memberName"/> from the text directly
        /// inside the row's element, as <see cref="XmlTextValueAttribute"/> does; it wins over the
        /// attribute (spec 002 US3, research R30). A type not added or a member it does not have fails
        /// when the options lock
        /// </summary>
        /// <typeparam name="TRow">An added row type</typeparam>
        /// <param name="memberName">The member's C# name, e.g. <c>nameof(Price.Amount)</c></param>
        /// <returns>These options, to chain</returns>
        /// <exception cref="InvalidOperationException">The options are read-only</exception>
        public DeserializeXmlOptions TextValue<TRow>(string memberName) where TRow : Row
        {
            ArgumentException.ThrowIfNullOrEmpty(memberName);
            ThrowingAssertNotReadOnly();

            (_textValues ??= [])[typeof(TRow)] = memberName;
            return this;
        }

        /// <summary>
        /// The member names set through <see cref="TextValue{TRow}(string)"/>, by row type
        /// </summary>
        internal IReadOnlyDictionary<Type, string> TextValues => _textValues ?? [];

        /// <summary>
        /// XML defaults, e.g. XML Schema booleans
        /// </summary>
        protected override IEnumerable<Converter> MediaConverters => s_mediaConverters;

        /// <summary>
        /// Association of elements with row types and members, built once when the options are locked
        /// </summary>
        internal XmlAssociation Association
        {
            get
            {
                MakeReadOnly();
                return _association!;
            }
        }

        /// <summary>
        /// Associate the definitions, throwing for two row types or two members read from the same
        /// element (FR-008, R8)
        /// </summary>
        protected override void OnMakingReadOnly() => _association = XmlAssociation.Create(this);

        /// <summary>
        /// The XML synchronous row enumerator
        /// </summary>
        /// <param name="utf8"></param>
        /// <returns></returns>
        protected internal override MediaRowEnumerator CreateRowEnumerator(ReadOnlyMemory<byte> utf8) => new XmlRowEnumerator(utf8, this);

        /// <summary>
        /// False: <c>Utf8XmlReader</c> reads a byte order mark itself and checks it against the declared
        /// encoding (research R18)
        /// </summary>
        protected internal override bool SkipsByteOrderMark => false;

        /// <summary>
        /// The XML asynchronous row enumerator
        /// </summary>
        /// <param name="utf8"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected internal override MediaRowAsyncEnumerator CreateRowAsyncEnumerator(PipeReader utf8, CancellationToken cancellationToken) =>
            new XmlRowAsyncEnumerator(utf8, this, cancellationToken);
    }
}
