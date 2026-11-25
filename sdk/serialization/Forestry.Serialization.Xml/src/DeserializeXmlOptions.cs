using System.IO.Pipelines;
using Forestry.Serialization.Converters;
using Forestry.Serialization.Xml.Converters;
using Forestry.Serialization.Reading;

namespace Forestry.Serialization.Xml
{
    /// <summary>
    /// Options of the XML format: the telemetry types to flatten, converters and settings. Associates elements
    /// with telemetry types and members by name, or by <see cref="XmlElementNameAttribute"/> names (research R6,
    /// R8; spec 003 R37)
    /// </summary>
    public sealed class DeserializeXmlOptions : DeserializeOptions
    {
        private static readonly Converter[] s_formatConverters = [new XmlBooleanConverter()];

        private XmlAssociation? _association;

        private Dictionary<Type, string>? _elementTexts;

        /// <summary>
        /// Set <typeparamref name="TTelemetry"/>'s member <paramref name="memberName"/> from the text of the telemetry's
        /// own element, as <see cref="XmlElementTextAttribute"/> does; it wins over the attribute (spec 002
        /// US3, research R30; named in spec 003, R37, spec 002 <c>TextValue</c>). A type not added, a member it
        /// does not have, or a member with another source (<see cref="XmlElementNameAttribute"/>,
        /// <see cref="XmlAttributeNameAttribute"/>) fails when the options lock
        /// </summary>
        /// <typeparam name="TTelemetry">An added telemetry type</typeparam>
        /// <param name="memberName">The member's C# name, e.g. <c>nameof(Price.Amount)</c></param>
        /// <returns>These options, to chain</returns>
        /// <exception cref="InvalidOperationException">The options are read-only</exception>
        public DeserializeXmlOptions ElementText<TTelemetry>(string memberName) where TTelemetry : Telemetry
        {
            ArgumentException.ThrowIfNullOrEmpty(memberName);
            ThrowingAssertNotReadOnly();

            (_elementTexts ??= [])[typeof(TTelemetry)] = memberName;
            return this;
        }

        /// <summary>
        /// The member names set through <see cref="ElementText{TTelemetry}(string)"/>, by telemetry type
        /// </summary>
        internal IReadOnlyDictionary<Type, string> ElementTexts => _elementTexts ?? [];

        /// <summary>
        /// XML defaults, e.g. XML Schema booleans
        /// </summary>
        protected override IEnumerable<Converter> FormatConverters => s_formatConverters;

        /// <summary>
        /// Association of elements with telemetry types and members, built once when the options are locked
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
        /// Associate the definitions, throwing for two telemetry types or two members read from the same
        /// element (FR-008, R8)
        /// </summary>
        protected override void OnMakingReadOnly() => _association = XmlAssociation.Create(this);

        /// <summary>
        /// The XML synchronous telemetry enumerator
        /// </summary>
        /// <param name="utf8"></param>
        /// <returns></returns>
        protected internal override FormatTelemetryEnumerator CreateTelemetryEnumerator(ReadOnlyMemory<byte> utf8) => new XmlTelemetryEnumerator(utf8, this);

        /// <summary>
        /// False: <c>Utf8XmlReader</c> reads a byte order mark itself and checks it against the declared
        /// encoding (research R18)
        /// </summary>
        protected internal override bool SkipsByteOrderMark => false;

        /// <summary>
        /// The XML asynchronous telemetry enumerator
        /// </summary>
        /// <param name="utf8"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected internal override FormatTelemetryAsyncEnumerator CreateTelemetryAsyncEnumerator(PipeReader utf8, CancellationToken cancellationToken) =>
            new XmlTelemetryAsyncEnumerator(utf8, this, cancellationToken);
    }
}
