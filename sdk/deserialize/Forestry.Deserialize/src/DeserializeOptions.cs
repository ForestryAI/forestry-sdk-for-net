using System.IO.Pipelines;
using Forestry.Deserialize.Converters;
using Forestry.Deserialize.Policies;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize
{
    /// <summary>
    /// Options of one media (e.g. <c>DeserializeXmlOptions</c>): the row types, converters and settings
    /// of a deserialization. The options own and build all definitions; once first used they are
    /// read-only (research R6)
    /// </summary>
    public abstract partial class DeserializeOptions
    {
        /// <summary>
        /// Options of a media
        /// </summary>
        protected DeserializeOptions()
        {
            Rows = new RowTypes(this);
        }

        #region Row types
        /// <summary>
        /// Row types added explicitly: only their rows are handed out (research R12)
        /// </summary>
        public RowTypes Rows { get; }
        #endregion

        #region Converters
        /// <summary>
        /// Converters added by the developer; they win over the media's defaults and the primitives
        /// (research R14)
        /// </summary>
        public IList<Converter> Converters => _converters ??= new OptionsObservableList<Converter>(this);

        private OptionsObservableList<Converter>? _converters;

        /// <summary>
        /// The media's default converters, e.g. XML Schema booleans; after the developer's converters,
        /// before the primitives
        /// </summary>
        protected virtual IEnumerable<Converter> MediaConverters => [];
        #endregion

        #region Policies
        /// <summary>
        /// Policies added by the developer, as instances living as long as the options; they win over
        /// policy attributes for the same method and target (research R7, R19)
        /// </summary>
        public PolicyCollection Policies => _policies ??= new PolicyCollection(this);

        private PolicyCollection? _policies;
        #endregion

        #region Read-only
        /// <summary>
        /// Locked for changes: the definitions are built
        /// </summary>
        public bool IsReadOnly => _isReadOnly;

        private volatile bool _isReadOnly;

        private readonly object _gate = new();

        /// <summary>
        /// Lock the options and build their definitions, as the first deserialization does; throws when
        /// an added row type cannot be defined. Idempotent
        /// </summary>
        public void MakeReadOnly()
        {
            if (_isReadOnly)
            {
                return;
            }

            lock (_gate)
            {
                if (_isReadOnly)
                {
                    return;
                }

                PolicyResolver resolver = new(_policies);
                _typeDefinitions = BuildTypeDefinitions(resolver);
                ResolvePolicies(resolver, _typeDefinitions);
                OnMakingReadOnly();
                _isReadOnly = true;
            }
        }

        /// <summary>
        /// The media builds what it derives from the definitions, once, before the options are locked;
        /// it throws to reject them, at the call like any definition error (FR-008)
        /// </summary>
        protected virtual void OnMakingReadOnly() { }

        /// <summary>
        /// Throws when read-only
        /// </summary>
        internal void ThrowingAssertNotReadOnly()
        {
            if (_isReadOnly)
            {
                Throwing.WhenDeserializeOptionsIsReadOnly();
            }
        }
        #endregion

        #region Media
        /// <summary>
        /// The media's synchronous row enumerator over UTF-8 bytes (research R2)
        /// </summary>
        /// <param name="utf8"></param>
        /// <returns></returns>
        protected internal abstract MediaRowEnumerator CreateRowEnumerator(ReadOnlyMemory<byte> utf8);

        /// <summary>
        /// The media's asynchronous row enumerator over the UTF-8 bytes of a pipe (research R2)
        /// </summary>
        /// <param name="utf8"></param>
        /// <param name="cancellationToken">Cancels a move waiting for bytes</param>
        /// <returns></returns>
        protected internal abstract MediaRowAsyncEnumerator CreateRowAsyncEnumerator(PipeReader utf8, CancellationToken cancellationToken);

        /// <summary>
        /// Whether the core skips a leading UTF-8 byte order mark before the media's reader sees the bytes
        /// (research R18). True by default, for a reader that does not handle one (e.g. JSON's); a media
        /// whose reader checks the mark itself (XML's, against the declared encoding) returns false
        /// </summary>
        protected internal virtual bool SkipsByteOrderMark => true;
        #endregion

        /// <summary>
        /// List locked with the options
        /// </summary>
        private sealed class OptionsObservableList<T>(DeserializeOptions options) : ObservableList<T>
        {
            public override bool IsReadOnly => options.IsReadOnly;

            protected override void Before()
            {
                options.ThrowingAssertNotReadOnly();
            }
        }
    }
}
