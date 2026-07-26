namespace Forestry.Turn
{
    /// <summary>
    /// Directive position in each pipe phase
    /// </summary>
    public enum PipelineDirectivePosition
    {
        /// <summary>
        /// Processed for each intention either zero or one times
        /// </summary>
        EachIntention,
        
        /// <summary>
        /// Processed during the retry phase either zero or multiple times
        /// </summary>
        EachRetry,

        /// <summary>
        /// Process before the turning phase either zero or multiple times
        /// </summary>
        BeforeTurning
    }
}
