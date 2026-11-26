using System.Collections;

namespace Forestry.Serialization
{
    /// <summary>
    /// Telemetry types added explicitly to an options instance; locked with the options (research R12).
    /// Only these types' telemetries are handed out; there is no assembly scan
    /// </summary>
    public sealed class TelemetryTypes : IReadOnlyCollection<Type>
    {
        internal TelemetryTypes(TelemetrySerializerOptions options)
        {
            _options = options;
        }

        private readonly TelemetrySerializerOptions _options;

        private readonly List<Type> _types = [];

        /// <summary>
        /// Add a telemetry type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>This collection, for chaining</returns>
        public TelemetryTypes Add<T>() where T : Telemetry => Add(typeof(T));

        /// <summary>
        /// Add telemetry types; a type not extending <see cref="Telemetry"/> throws when added
        /// </summary>
        /// <param name="types"></param>
        /// <returns>This collection, for chaining</returns>
        public TelemetryTypes Add(params Type[] types)
        {
            ArgumentNullException.ThrowIfNull(types);
            _options.ThrowingAssertNotReadOnly();

            foreach (Type type in types)
            {
                ArgumentNullException.ThrowIfNull(type, nameof(types));

                if (!type.IsSubclassOf(typeof(Telemetry)) || type.IsAbstract)
                {
                    Throwing.WhenNotATelemetryType(type);
                }

                if (!_types.Contains(type))
                {
                    _types.Add(type);
                }
            }

            return this;
        }

        /// <summary>
        /// Number of telemetry types added
        /// </summary>
        public int Count => _types.Count;

        /// <summary>
        /// Telemetry types in the order added
        /// </summary>
        /// <returns></returns>
        public IEnumerator<Type> GetEnumerator() => _types.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
