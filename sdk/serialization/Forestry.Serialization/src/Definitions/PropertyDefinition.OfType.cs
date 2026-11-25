using System.Linq.Expressions;
using System.Reflection;
using Forestry.Serialization.Converters;
using Forestry.Serialization.Reading;

namespace Forestry.Serialization.Definitions
{
    /// <summary>
    /// Definition of a member of type <typeparamref name="T"/>: its resolved <see cref="Converter{T}"/>
    /// and a setter built once (research R14; spec 003 R33)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class PropertyDefinition<T> : PropertyDefinition
    {
        internal PropertyDefinition(MemberInfo member) : base(member, typeof(T))
        {
            _setter = CreateSetter(member);
        }

        private Converter<T> _converter = null!;

        /// <summary>
        /// Resolve the typed <see cref="Converter{T}"/> for <typeparamref name="T"/> through the options
        /// (spec 003, research R33)
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        private protected override Converter? ResolveConverter(DeserializeOptions options)
        {
            Converter<T>? converter = options.ResolveConverter(typeof(T)) as Converter<T>;

            if (converter is not null)
            {
                _converter = converter;
            }

            return converter;
        }

        private readonly Action<Telemetry, T> _setter;

        /// <summary>
        /// Empty text sets null where the member allows it, else its type's default, without the
        /// converter; otherwise the converter's value. A failed conversion throws naming the member,
        /// its place and the class (FR-007)
        /// </summary>
        /// <param name="telemetry"></param>
        /// <param name="utf8"></param>
        /// <param name="information"></param>
        /// <param name="set">The composed set step, an <c>Action&lt;ContextTelemetry, T&gt;</c></param>
        internal override void ReadAndSet(ContextTelemetry telemetry, ReadOnlySpan<byte> utf8, in ReadingInformation information, Delegate set)
        {
            T value;

            if (utf8.IsEmpty)
            {
                value = default!;
            }
            else
            {
                try
                {
                    value = _converter.Convert(utf8);
                }
                catch (Exception exception) when (exception is FormatException or OverflowException)
                {
                    Throwing.WhenUnconvertible(this, telemetry.Telemetry.GetType(), information.Path.ToUtf8String(), exception);
                    return;
                }
            }

            ((Action<ContextTelemetry, T>)set)(telemetry, value);
        }

        internal override Delegate ComposeSet(IReadOnlyList<Policies.EnumerationPolicy> chain)
        {
            Action<ContextTelemetry, T> step = (telemetry, value) =>
            {
                _setter(telemetry.Telemetry, value);
                telemetry.MarkSet(Index);
            };

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                Policies.EnumerationPolicy policy = chain[i];
                Action<ContextTelemetry, T> next = step;
                step = (telemetry, value) => policy.Set(telemetry, this, value, telemetry.Context, next);
            }

            return step;
        }

        /// <summary>
        /// Set the member with the built-in setter, no policy (a policy setting it through
        /// <see cref="ContextTelemetry.Set{TValue}(string, TValue)"/>)
        /// </summary>
        /// <param name="telemetry"></param>
        /// <param name="value"></param>
        internal void SetValue(Telemetry telemetry, T value) => _setter(telemetry, value);

        /// <summary>
        /// Compiled once: assign the member of the telemetry's concrete type, property or field alike
        /// </summary>
        /// <param name="member"></param>
        /// <returns></returns>
        private static Action<Telemetry, T> CreateSetter(MemberInfo member)
        {
            ParameterExpression telemetry = Expression.Parameter(typeof(Telemetry), "telemetry");
            ParameterExpression value = Expression.Parameter(typeof(T), "value");
            Expression target = Expression.Convert(telemetry, member.DeclaringType!);

            MemberExpression access = member is PropertyInfo property
                ? Expression.Property(target, property)
                : Expression.Field(target, (FieldInfo)member);

            return Expression.Lambda<Action<Telemetry, T>>(Expression.Assign(access, value), telemetry, value).Compile();
        }
    }
}
