using Forestry.Turn.Pipeline;

namespace Forestry.Turn
{
    /// <summary>
    /// Paired discourses starting with an intention and ending with an answer
    /// </summary>
    public sealed class AdjacencyPair: IDisposable
    {
        /// <summary>
        /// Creates a new instanceof <see cref="AdjacencyPair"/>
        /// </summary>
        /// <param name="intention"></param>
        /// <param name="answerAnalyser"></param>
        public AdjacencyPair(
            Intention intention,
            AnswerAnalyzer answerAnalyser
        ) {
            ArgumentNullException.ThrowIfNull(intention, nameof(intention));

            Intention = intention;
            AnswerAnalyzer = answerAnalyser;
        }

        /// <summary>
        /// Get <see cref="Intention"/> turn
        /// </summary>
        public Intention Intention { get; }

        /// <summary>
        /// Answer analyzer used by the turn pipeline
        /// </summary>
        public AnswerAnalyzer AnswerAnalyzer { get; set; }

        /// <summary>
        /// Get <see cref="Answer"/> turn throwing an exception when not set
        /// </summary>
        public Answer Answer { 
            get
            {
                if (_answer is null)
                {
                    throw new InvalidOperationException("Turning never set answer");
                }

                return _answer;
            }
            set => _answer = value;
        }

        private Answer? _answer;

        /// <summary>
        /// Flagging true when answer turn exists
        /// </summary>
        public bool HasAnswer => _answer is not null;

        /// <summary>
        /// Cancellation token used when processing the turns
        /// </summary>
        public CancellationToken CancellationToken { get; internal set; }

        #region Adjacency pair context in a turn
        public AdjacencyPairContext AdjacencyPairContext => new(this);

        /// <summary>
        /// Process start time by directive pipe
        /// </summary>
        internal DateTimeOffset ProcessStartTime { get; set; }

        /// <summary>
        /// Retry count
        /// </summary>
        internal int RetryCount { get; set; }
        #endregion

        /// <summary>
        /// Positioned directives derived from intention context (TODO: explain cooralation=
        /// </summary>
        internal List<(PipelineDirectivePosition Position, Directive Directive)>? PositionedDirectives { get; set; }

        /// <summary>
        /// Disposes the intention and answer turns
        /// </summary>
        public void Dispose()
        {
            Intention.Dispose();

            var answer = Interlocked.Exchange(ref _answer, null);  // avoids multiple threads disposing at the same time with a local reference
            answer?.Dispose();
        }
    }
}
