namespace Forestry.Serialization
{
    /// <summary>
    /// Abstract base of the developer's telemetry classes. A telemetry is an instance of a class extending
    /// <see cref="Telemetry"/> whose typed properties and fields hold the values flattened for one
    /// occurrence in the document (research R4); a telemetry is the unit deserialization enumerates
    /// </summary>
    /// <remarks>
    /// <para>
    /// A telemetry is flat: every member is a single value, a primitive, a <see cref="string"/>, an enum, or a
    /// struct that formats to UTF-8 and parses from text (e.g. <see cref="Guid"/>,
    /// <see cref="DateTimeOffset"/>), or a nullable of one of these. Never a nested object, a collection
    /// or a composite struct; a telemetry type with such a member fails when the options lock (constitution
    /// C7; spec 003, research R32, R33). So each member can become one column in any big data format.
    /// "Telemetry" names the unit enumerated and has nothing to do with row-based vs columnar formats.
    /// </para>
    /// <para>
    /// A telemetry's public surface is only <see cref="Is{T}"/> and <see cref="AsType{T}"/>; both are
    /// non-virtual and need nothing stored on the telemetry, so a telemetry costs only its typed members (C1)
    /// </para>
    /// </remarks>
    public abstract class Telemetry
    {
        /// <summary>
        /// Telemetry base for a derived class
        /// </summary>
        protected Telemetry()
        {
        }

        /// <summary>
        /// Whether this telemetry is a <typeparamref name="T"/>, as C#'s <c>is</c>: true for
        /// <typeparamref name="T"/> and for classes deriving from it
        /// </summary>
        /// <typeparam name="T">A telemetry class</typeparam>
        /// <returns></returns>
        public bool Is<T>() where T : Telemetry => this is T;

        /// <summary>
        /// A copy of this telemetry for the consumer when the context keeps the original (spec 002 isolation,
        /// research R25); shallow is enough since members are single values
        /// </summary>
        /// <returns></returns>
        internal Telemetry ShallowCopy() => (Telemetry)MemberwiseClone();

        /// <summary>
        /// This telemetry as a <typeparamref name="T"/>: the same instance, with its values already typed
        /// </summary>
        /// <typeparam name="T">A telemetry class</typeparam>
        /// <returns></returns>
        /// <exception cref="InvalidCastException">The telemetry is not a <typeparamref name="T"/>; the message
        /// names both classes</exception>
        public T AsType<T>() where T : Telemetry
        {
            T? telemetry = this as T;

            if (telemetry is null)
            {
                Throwing.WhenTelemetryNotOfType(GetType(), typeof(T));
            }

            return telemetry;
        }
    }
}
