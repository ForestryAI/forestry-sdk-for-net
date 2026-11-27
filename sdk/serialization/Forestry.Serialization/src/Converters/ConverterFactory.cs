using System.Reflection;

namespace Forestry.Serialization.Converters
{
    /// <summary>
    /// Creates converters for a family of types (e.g. <see cref="Nullable{T}"/>), once per type when
    /// definitions are built (research R14)
    /// </summary>
    public abstract class ConverterFactory : Converter
    {
        /// <summary>
        /// Converter factory
        /// </summary>
        protected ConverterFactory()
        {
        }

        /// <summary>
        /// Create the converter for <paramref name="type"/>, a <see cref="Converter{T}"/> with T == type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="options">Options, to resolve the converters a created converter depends on</param>
        /// <returns></returns>
        public abstract Converter CreateConverter(Type type, TelemetrySerializationOptions options);

        /// <summary>
        /// Whether the converter depends on the member it is for (e.g. a format attribute on the telemetry
        /// member): then <see cref="CreateConverter(Type, MemberInfo, TelemetrySerializationOptions)"/> is
        /// called per member and its converter is not shared by type (spec 005 research R58)
        /// </summary>
        public virtual bool UsesMember => false;

        /// <summary>
        /// Create the converter for <paramref name="type"/> on <paramref name="member"/>, a
        /// <see cref="Converter{T}"/> with T == type. Called instead of
        /// <see cref="CreateConverter(Type, TelemetrySerializationOptions)"/> when <see cref="UsesMember"/>;
        /// the default ignores the member (research R58)
        /// </summary>
        /// <param name="type"></param>
        /// <param name="member">The telemetry member (property or field) the converter is for</param>
        /// <param name="options">Options, to resolve the converters a created converter depends on</param>
        /// <returns></returns>
        public virtual Converter CreateConverter(Type type, MemberInfo member, TelemetrySerializationOptions options) => CreateConverter(type, options);
    }
}
