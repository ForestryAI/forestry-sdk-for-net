using System.Diagnostics.CodeAnalysis;

namespace Forestry.Turn
{
    /// <summary>
    /// Intention type confers conversational expectations when turning
    /// </summary>
    public readonly partial struct IntentionType : IEquatable<IntentionType>
    {
        public IntentionType(
            string type
        ) {
            Type = type;
        }

        public string Type { get; }

        /// <summary>
        /// Equivalent <see cref="Type"/> string as bytes to another
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(IntentionType other)
        {
            return string.Equals(Type, other.Type, StringComparison.Ordinal);
        }

        /// <summary>
        /// Object is an equivalent <see cref="IntentionType"/>
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public override bool Equals([NotNullWhen(true)] object? other)
        {
            return other is IntentionType type && Equals(type);
        }

        public static bool operator ==(IntentionType left, IntentionType right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(IntentionType left, IntentionType right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Hash of <see cref="Type"/> string as bytes
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {            
            return Type is null ? 0 : StringComparer.Ordinal.GetHashCode(Type);
        }

        public override string ToString()
        {
            return Type ?? "<null>";
        }
    }
}
