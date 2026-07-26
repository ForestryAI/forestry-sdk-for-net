using System.Diagnostics;

namespace Forestry.Turn.Pipeline
{
    /// <summary>
    /// Last directive in the pipeline that delegates immediately to the turn pipeline's turning
    /// </summary>
    internal class TurningDirective : Directive
    {
        public TurningDirective(
            Turning turning
        ) {
            _turning = turning;
        }

        private readonly Turning _turning;

        /// <summary>
        /// Process by delegation where the directives are expected to be empty
        /// </summary>
        /// <param name="adjacencyPair"></param>
        /// <param name="directives"></param>
        /// <returns></returns>
        public override async ValueTask ProcessAsync(AdjacencyPair adjacencyPair, ReadOnlyMemory<Directive> directives)
        {
            Debug.Assert(directives.IsEmpty);

            await _turning.ProcessAsync(adjacencyPair).ConfigureAwait(false);

            adjacencyPair.Answer.HasErrors = adjacencyPair.AnswerAnalyzer.AssertHasErrors(adjacencyPair);
        }

        /// <summary>
        /// Process by delegation where the directives are expected to be empty
        /// </summary>
        /// <param name="adjacencyPair"></param>
        /// <param name="directives"></param>
        public override void Process(AdjacencyPair adjacencyPair, ReadOnlyMemory<Directive> directives)
        {
            Debug.Assert(directives.IsEmpty);

            _turning.Process(adjacencyPair);

            adjacencyPair.Answer.HasErrors = adjacencyPair.AnswerAnalyzer.AssertHasErrors(adjacencyPair);
        }
    }
}
