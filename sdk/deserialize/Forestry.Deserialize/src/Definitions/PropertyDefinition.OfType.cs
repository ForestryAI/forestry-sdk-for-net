using System.Linq.Expressions;
using System.Reflection;
using Forestry.Deserialize.Converters;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Definitions
{
    /// <summary>
    /// Definition of a member of type <typeparamref name="T"/>: its effective <see cref="Converter{T}"/>
    /// and a setter built once (research R14)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class PropertyDefinition<T> : PropertyDefinition
    {
        internal PropertyDefinition(MemberInfo member, Converter converter) : base(member, typeof(T), converter)
        {
            _converter = (Converter<T>)converter;
            _setter = CreateSetter(member);
        }

        private readonly Converter<T> _converter;

        private readonly Action<Row, T> _setter;

        /// <summary>
        /// Empty text sets null where the member allows it, else its type's default, without the
        /// converter; otherwise the converter's value. A failed conversion throws naming the member,
        /// its place and the class (FR-007)
        /// </summary>
        /// <param name="row"></param>
        /// <param name="utf8"></param>
        /// <param name="context"></param>
        internal override void ReadAndSet(Row row, ReadOnlySpan<byte> utf8, in FieldContext context)
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
                    Throwing.WhenUnconvertible(this, row.GetType(), context.Path.ToUtf8String(), exception);
                    return;
                }
            }

            _setter(row, value);
        }

        /// <summary>
        /// Compiled once: assign the member of the row's concrete type, property or field alike
        /// </summary>
        /// <param name="member"></param>
        /// <returns></returns>
        private static Action<Row, T> CreateSetter(MemberInfo member)
        {
            ParameterExpression row = Expression.Parameter(typeof(Row), "row");
            ParameterExpression value = Expression.Parameter(typeof(T), "value");
            Expression target = Expression.Convert(row, member.DeclaringType!);

            MemberExpression access = member is PropertyInfo property
                ? Expression.Property(target, property)
                : Expression.Field(target, (FieldInfo)member);

            return Expression.Lambda<Action<Row, T>>(Expression.Assign(access, value), row, value).Compile();
        }
    }
}
