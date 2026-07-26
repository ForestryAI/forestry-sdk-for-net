using System.Diagnostics.CodeAnalysis;

namespace Forestry.Turn
{
    /// <summary>
    /// An intention in the turn-taking model is an expectation of an answer making
    /// up the first half of an adjacency pair.  A turning is responsible for
    /// creating concrete intentions along side handling context and headers in a
    /// suitable format.
    /// </summary>
    public abstract class Intention: IDisposable
    {
        /// <summary>
        /// Intention type confers conversational expectations when turning
        /// </summary>
        public virtual IntentionType IntentionType { get; set; }

        /// <summary>
        /// Intention content
        /// </summary>
        public virtual IntentionContent? Content { get; set; }

        /// <summary>
        /// Mutable dimensions used when turning into an answer
        /// </summary>
        public IntentionDimensions Dimensions => new(this);

        /// <summary>
        /// Try get dimension
        /// </summary>
        /// <remarks>Dimensions with the same name are concatenated with a delimeter</remarks>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        protected internal abstract bool TryGetDimension(string name, [NotNullWhen(true)] out string? value);

        /// <summary>
        /// Try get dimension values by name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        protected internal abstract bool TryGetDimensionValues(string name, [NotNullWhen(true)] out IEnumerable<string>? values);

        /// <summary>
        /// Assert true when has dimension by name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        protected internal abstract bool ContainsDimension(string name);

        /// <summary>
        /// Add dimension to the dimensions collection
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        protected internal abstract void AddDimension(string name, string value);

        /// <summary>
        /// Remove dimension by name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        protected internal abstract bool RemoveDimension(string name);

        /// <summary>
        /// Set dimension value
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        protected internal virtual void SetDimension(string name, string value)
        {
            RemoveDimension(name);
            AddDimension(name, value);
        }

        /// <summary>
        /// Dimensions iterator
        /// </summary>
        /// <returns></returns>
        protected internal abstract IEnumerable<Dimension> EnumerateDimensions();

        /// <summary>
        /// Dispose primarily dimensions and content
        /// </summary
        public abstract void Dispose();
    }
}
