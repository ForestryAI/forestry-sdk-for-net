namespace Forestry.Turn
{
    /// <summary>
    /// Turning in the turn pipeline sets an answer by processing the intention made of content plus dimensions
    /// </summary>
    public abstract class Turning
    {
        /// <summary>
        /// Processes the intention in the <paramref name="adjacencyPair"/> into an answer
        /// </summary>
        /// <param name="adjacencyPair"></param>
        public abstract void Process(AdjacencyPair adjacencyPair);

        /// <summary>
        /// Processes the intention in the <paramref name="adjacencyPair"/> into an answer
        /// </summary>
        /// <param name="adjacencyPair"></param>
        /// <returns></returns>
        public abstract ValueTask ProcessAsync(AdjacencyPair adjacencyPair);

        /// <summary>
        /// Creates an intention that this turning can process
        /// </summary>
        /// <returns></returns>
        protected internal abstract Intention CreateIntention();
    }
}
