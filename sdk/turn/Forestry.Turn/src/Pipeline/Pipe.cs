using System.Buffers;
using System.Diagnostics;

namespace Forestry.Turn.Pipeline
{
    /// <summary>
    /// A pipe drives the pipeline of directives to turn the intention into an answer
    /// and culture with three distinct phases: intention creation, retry and transformation.
    /// </summary>
    public partial class Pipe
    {

        internal Pipe(
            Options options
        )
        {
            AnswerAnalyzer = options.AnswerAnalyzer ?? throw new ArgumentNullException(paramName: nameof(options.AnswerAnalyzer));

            _turning = options.Turning ?? throw new ArgumentNullException(paramName: nameof(options.Turning));
            _directives = options.Directives ?? throw new ArgumentNullException(paramName: nameof(options.Directives));

            Debug.Assert(options.Directives[^1] is TurningDirective);

            _lastIntentionDirectiveIndex = options.LastIntentionDirectiveIndex;
            _lastRetryDirectiveIndex = options.LastRetryDirectiveIndex;
        }

        /// <summary>
        /// Adjacency pair turning
        /// </summary>
        private protected readonly Turning _turning;

        /// <summary>
        /// Directives constituting the pipe
        /// </summary>
        private readonly ReadOnlyMemory<Directive> _directives;

        /// <summary>
        /// Index of last directive for each intention
        /// </summary>
        private readonly int _lastIntentionDirectiveIndex;

        /// <summary>
        /// Index of last directive for each retry
        /// </summary>
        private readonly int _lastRetryDirectiveIndex;

        /// <summary>
        /// Answer analyzer
        /// </summary>
        public AnswerAnalyzer AnswerAnalyzer { get; }

        /// <summary>
        /// Delegating the intention creation to the <see cref="Turning"/>
        /// </summary>
        public Intention CreateIntention() => _turning.CreateIntention();

        /// <summary>
        /// Delegating the adjancency pair creation to the <see cref="Turning"/> with this answer analyzer
        /// </summary>
        /// <returns></returns>
        public AdjacencyPair CreateAdjacencyPair() => new(CreateIntention(), AnswerAnalyzer);

        /// <summary>
        ///
        /// </summary>
        /// <param name="conversationState"></param>
        /// <returns></returns>s
        public AdjacencyPair CreateAdjacencyPair(ConversationContext conversationState)
        {
            AdjacencyPair adjacencyPair = new(CreateIntention(), AnswerAnalyzer);

            // TODO: dialog state

            return adjacencyPair;
        }

        /// <summary>
        /// Turn the adjacency pair
        /// </summary>
        /// <param name="adjacencyPair"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public ValueTask TurnAsync(
            AdjacencyPair adjacencyPair,
            CancellationToken cancellationToken
        ) {
            adjacencyPair.CancellationToken = cancellationToken;
            adjacencyPair.ProcessStartTime = DateTime.UtcNow;
            // TODO: dimensions from turn conversation (scoped)

            if (adjacencyPair.PositionedDirectives is null || adjacencyPair.PositionedDirectives.Count == 0)
            {
                return _directives.Span[0].ProcessAsync(adjacencyPair, _directives.Slice(1));
            }

            return TurnAsync(adjacencyPair);
        }

        /// <summary>
        /// Turn the adjacency pair appending contextual positioned target
        /// </summary>
        /// <param name="adjacencyPair"></param>
        /// <returns></returns>
        private async ValueTask TurnAsync(
            AdjacencyPair adjacencyPair
        )
        {
            // Renting space for target associated with this pipeline and the context of adjacency pair keeping the turns clean from target
            int count = _directives.Length + adjacencyPair.PositionedDirectives!.Count;
            Directive[] rentedDirectives = ArrayPool<Directive>.Shared.Rent(count);

            try
            {
                ReadOnlyMemory<Directive> accessor = AddTransientDirectives(rentedDirectives, adjacencyPair.PositionedDirectives);
                await accessor.Span[0].ProcessAsync(adjacencyPair, accessor.Slice(1));
            } finally
            {
                ArrayPool<Directive>.Shared.Return(rentedDirectives);
            }
        }

        /// <summary>
        /// Add transient directives (source) with the pipe directives to target
        /// </summary>
        /// <param name="target"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        private ReadOnlyMemory<Directive> AddTransientDirectives(
            Directive[] target,
            List<(PipelineDirectivePosition Position, Directive Directive)> source
        )
        {
            ReadOnlySpan<Directive> accessor = _directives.Span;  // no-cost and stack only
            int turningDirectiveIndex = accessor.Length - 1;

            // Each intention
            accessor[.._lastIntentionDirectiveIndex].CopyTo(target);

            int index = _lastIntentionDirectiveIndex;
            int count = MarkAddedTransientDirectives(target, source, PipelineDirectivePosition.EachIntention, index);

            // Each retry
            index += count;
            count = _lastRetryDirectiveIndex - _lastIntentionDirectiveIndex;
            accessor.Slice(_lastIntentionDirectiveIndex, count).CopyTo(target.AsSpan(index, count));

            index += count;
            count = MarkAddedTransientDirectives(target, source, PipelineDirectivePosition.EachRetry, index);

            // Before turning
            index += count;
            count = turningDirectiveIndex - _lastRetryDirectiveIndex;
            accessor.Slice(_lastRetryDirectiveIndex, count).CopyTo(target.AsSpan(index, count));

            index += count;
            count = MarkAddedTransientDirectives(target, source, PipelineDirectivePosition.BeforeTurning, index);

            // Turning
            index += count;
            target[index] = accessor[turningDirectiveIndex];

            return new ReadOnlyMemory<Directive>(target, 0, index + 1);
        }

        /// <summary>
        /// Add transient directives (source) filtered by position to target then mark last
        /// </summary>
        /// <param name="target"></param>
        /// <param name="source"></param>
        /// <param name="position"></param>
        /// <param name="mark"></param>
        /// <returns></returns>
        private static int MarkAddedTransientDirectives(
            Directive[] target,
            List<(PipelineDirectivePosition Position, Directive Directive)> source,
            PipelineDirectivePosition position,
            int mark
        )
        {
            int count = 0;

            if (source is not null)
            {
                foreach((PipelineDirectivePosition Position, Directive Directive) value in source)
                {
                    if (value.Position == position)
                    {
                        target[mark + count] = value.Directive;
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
