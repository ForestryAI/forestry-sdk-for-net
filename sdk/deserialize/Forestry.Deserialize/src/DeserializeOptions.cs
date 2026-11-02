using Forestry.Deserialize.Converters;
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
        /// Converters added by the developer; they win over the media's defaults and the built-ins
        /// (research R14)
        /// </summary>
        public IList<Converter> Converters => _converters ??= new OptionsObservableList<Converter>(this);

        private OptionsObservableList<Converter>? _converters;

        /// <summary>
        /// The media's default converters, e.g. XML Schema booleans; after the developer's converters,
        /// before the built-ins
        /// </summary>
        protected virtual IEnumerable<Converter> MediaConverters => [];
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

                _typeDefinitions = BuildTypeDefinitions();
                _isReadOnly = true;
            }
        }

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
        protected internal abstract RowEnumerator CreateRowEnumerator(ReadOnlyMemory<byte> utf8);
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
