
namespace Forestry.Turn.Tests
{
    public class TestingTurning : Turning
    {
        private readonly Func<AdjacencyPair, TestingAnswer> _turning;

        private readonly object _answering = new();

        /// <summary>
        /// Turn each answer blindly
        /// </summary>
        /// <param name="answers"></param>
        public TestingTurning(params TestingAnswer[] answers)
        {
            int index = 0;
            _turning = _ =>
            {
                lock (_answering)
                {
                    return answers[index++];
                }
            };
        }

        /// <summary>
        /// Turn intention into an answer
        /// </summary>
        /// <param name="turning"></param>
        public TestingTurning(Func<TestingIntention, TestingAnswer> turning): this(turn => turning((TestingIntention)turn.Intention)) { }

        /// <summary>
        /// Turn adjacency pair into an answer
        /// </summary>
        /// <param name="turning"></param>
        private TestingTurning(Func<AdjacencyPair, TestingAnswer> turning)
        {
            _turning = turning;
        }

        /// <summary>
        /// Set asynchronous processing
        /// </summary>
        public bool? IsAsynchronously { get; set; }

        public override void Process(AdjacencyPair adjacencyPair)
        {
            if (IsAsynchronously == true)
            {
                throw new InvalidOperationException("Expecting sychronous processing");
            }

            InternalProccessAsync(adjacencyPair).GetAwaiter().GetResult();
        }

        public override async ValueTask ProcessAsync(AdjacencyPair adjacencyPair)
        {
            if (IsAsynchronously == false)
            {
                throw new InvalidOperationException("Expecting asychronous processing");
            }

            await InternalProccessAsync(adjacencyPair);
        }

        protected override Intention CreateIntention()
        {
            return new TestingIntention();
        }

        private Task InternalProccessAsync(AdjacencyPair adjacencyPair)
        {
            adjacencyPair.Answer = _turning(adjacencyPair);
            return Task.CompletedTask;
        }
    }
}
