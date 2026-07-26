namespace Forestry.Deserialize
{
    /// <summary>
    /// Typed value
    /// </summary>
    public abstract class Value<T>: NullableValue<T>
    {
        public override bool HasValue => true;

        public abstract override T Deserialized { get; }

        public static implicit operator T(Value<T> value)
        {
            ArgumentNullException.ThrowIfNull(value);

            return value.Deserialized!;
        }
    }
}