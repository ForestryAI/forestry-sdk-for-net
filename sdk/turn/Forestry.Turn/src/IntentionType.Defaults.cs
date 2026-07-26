namespace Forestry.Turn
{
    /// <summary>
    /// Intention type defaults helpful for interaction
    /// </summary>
    public readonly partial struct IntentionType : IEquatable<IntentionType>
    {
        /// <summary>
        /// Single-turn interactions yield an answer solely based on the intention
        /// </summary>
        public static readonly IntentionType SingleTurn = new IntentionType("single-turn");

        /// <summary>
        /// Multi-turn signales ongoing dialogues with multiple exchanges
        /// </summary>
        public static readonly IntentionType MultiTurn = new IntentionType("multi-turn");
    }
}
