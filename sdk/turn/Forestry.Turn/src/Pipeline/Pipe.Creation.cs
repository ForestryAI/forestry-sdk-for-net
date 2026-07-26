namespace Forestry.Turn.Pipeline
{
    /// <summary>
    /// Pipe creation
    /// </summary>
    public partial class Pipe
    {
        /// <summary>
        /// Default count of pipe derectives
        /// </summary>
        private static readonly int _defaultDirectivesCount = 8;

        /// <summary>
        /// Internal pipe options
        /// </summary>
        internal struct Options
        {
            /// <summary>
            /// Answer analyzer
            /// </summary>
            internal AnswerAnalyzer AnswerAnalyzer;

            /// <summary>
            /// Adjacency pair turning
            /// </summary>
            internal Turning Turning;

            /// <summary>
            /// Index of last directive for each intention
            /// </summary>
            internal int LastIntentionDirectiveIndex;

            /// <summary>
            /// Index of last directive for each retry
            /// </summary>
            internal int LastRetryDirectiveIndex;

            /// <summary>
            /// Directives constituting the pipe
            /// </summary>
            internal Directive[] Directives;
        }

        /// <summary>
        /// Create pipe from client options and an answer analyzer
        /// </summary>
        /// <param name="clientOptions"></param>
        /// <param name="answerAnalyzer"></param>
        /// <returns></returns>
        public static Pipe Create(
            ClientOptions clientOptions,
            AnswerAnalyzer? answerAnalyzer
        ) {
            Options options = ToInteralOptions(clientOptions, answerAnalyzer);
            return new Pipe(options);
        }

        /// <summary>
        /// Create internal pipe options
        /// </summary>
        /// <param name="clientOptions"></param>
        /// <param name="answerAnalyzer"></param>
        /// <returns></returns>
        private static Options ToInteralOptions(
            ClientOptions clientOptions,
            AnswerAnalyzer? answerAnalyzer
        )
        {
            Options options;

            options.AnswerAnalyzer = answerAnalyzer ?? clientOptions.AnswerAnalyzer;
            options.Turning = clientOptions.Turning;

            List<Directive> directives = new(_defaultDirectivesCount + (clientOptions.PositionedDirectives?.Count ?? 0));

            // Add directives from client options for each position
            void Add(PipelineDirectivePosition position)
            {
                if (clientOptions.PositionedDirectives is not null)
                {
                    foreach(var (Position, Directive) in clientOptions.PositionedDirectives)
                    {
                        if (Position == position && Directive is not null)
                        {
                            directives.Add(Directive);
                        }
                    }
                }
            }

            // Each Intention
            Add(PipelineDirectivePosition.EachIntention);

            options.LastIntentionDirectiveIndex = directives.Count;

            // Each Retry
            // TODO: Retry defaults

            Add(PipelineDirectivePosition.EachRetry);

            options.LastRetryDirectiveIndex = directives.Count;

            // Before turning
            Add(PipelineDirectivePosition.BeforeTurning);

            // Turning
            directives.Add(new TurningDirective(options.Turning));

            options.Directives = [.. directives];

            return options;
        }
    }
}
