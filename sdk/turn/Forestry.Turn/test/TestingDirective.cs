using Forestry.Turn.Pipeline;

namespace Forestry.Turn.Tests
{
    /// <summary>
    /// Directive that records when the pipeline calls it, then hands off to the rest
    /// of the chain. Used to observe how often/when a directive position runs.
    /// </summary>
    public class TestingDirective : Directive
    {
        private readonly Action<AdjacencyPair> _onProcess;

        public TestingDirective(Action<AdjacencyPair> onProcess)
        {
            _onProcess = onProcess;
        }

        public override void Process(AdjacencyPair adjacencyPair, ReadOnlyMemory<Directive> directives)
        {
            _onProcess(adjacencyPair);
            ProcessNext(adjacencyPair, directives);
        }

        public override async ValueTask ProcessAsync(AdjacencyPair adjacencyPair, ReadOnlyMemory<Directive> directives)
        {
            _onProcess(adjacencyPair);
            await ProcessNextAsync(adjacencyPair, directives);
        }
    }
}
