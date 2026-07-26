using Forestry.Turn.Pipeline;
using Xunit;

namespace Forestry.Turn.Tests
{
    /// <summary>
    /// Shows the shape a concrete client is expected to ride on: build a
    /// <see cref="Pipe"/> from <see cref="ClientOptions"/> and a <see cref="Turning"/>,
    /// create an adjacency pair for each intention, then drive it through the pipeline
    /// (EachIntention directives once, EachRetry/BeforeTurning directives once per
    /// attempt, finally the turning itself) to get an answer.
    /// </summary>
    public class PipeTests
    {
        [Fact]
        public async Task TurnAsync_WhenTurningSucceeds_ItShould_SetAnswerOnAdjacencyPair()
        {
            // Arrange
            TestingTurning turning = new(intention => new TestingAnswer().WithContent("ok"));
            TestingClientOptions options = new(turning);
            Pipe pipe = Pipe.Create(options, answerAnalyzer: null);

            AdjacencyPair pair = pipe.CreateAdjacencyPair();

            // Act
            await pipe.TurnAsync(pair, CancellationToken.None);

            // Assert
            Assert.True(pair.HasAnswer);
            Assert.False(pair.Answer.HasErrors);
        }

        [Fact]
        public async Task TurnAsync_WhenTurningFailsThenSucceeds_ItShould_RetryUntilItSucceeds()
        {
            // Arrange
            int attempts = 0;
            TestingTurning turning = new(intention =>
            {
                attempts++;
                if (attempts <= 2)
                {
                    throw new IOException("transient failure");
                }

                return new TestingAnswer().WithContent("ok");
            });

            TestingClientOptions options = new(turning);
            options.AddDirective(
                new RetryDirective(maximumRetries: 3, delayPolicy: Delaying.CreateFixed(TimeSpan.Zero)),
                PipelineDirectivePosition.EachRetry);

            Pipe pipe = Pipe.Create(options, answerAnalyzer: null);
            AdjacencyPair pair = pipe.CreateAdjacencyPair();

            // Act
            await pipe.TurnAsync(pair, CancellationToken.None);

            // Assert
            Assert.Equal(3, attempts);
            Assert.True(pair.HasAnswer);
            Assert.Equal(2, pair.AdjacencyPairContext.RetryCount);
        }

        [Fact]
        public async Task TurnAsync_WhenTurningKeepsFailing_ItShould_ThrowAfterMaximumRetries()
        {
            // Arrange
            TestingTurning turning = new(intention => throw new IOException("always fails"));

            TestingClientOptions options = new(turning);
            options.AddDirective(
                new RetryDirective(maximumRetries: 2, delayPolicy: Delaying.CreateFixed(TimeSpan.Zero)),
                PipelineDirectivePosition.EachRetry);

            Pipe pipe = Pipe.Create(options, answerAnalyzer: null);
            AdjacencyPair pair = pipe.CreateAdjacencyPair();

            // Act
            AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
                () => pipe.TurnAsync(pair, CancellationToken.None).AsTask());

            // Assert: initial attempt + 2 retries == 3 captured failures
            Assert.Equal(3, exception.InnerExceptions.Count);
            Assert.All(exception.InnerExceptions, inner => Assert.IsType<IOException>(inner));
        }

        [Fact]
        public async Task TurnAsync_ItShould_RunEachIntentionDirectiveOnceAndBeforeTurningDirectiveOncePerAttempt()
        {
            // Arrange
            int eachIntentionCount = 0;
            int beforeTurningCount = 0;
            int attempts = 0;

            TestingTurning turning = new(intention =>
            {
                attempts++;
                if (attempts <= 2)
                {
                    throw new IOException("transient failure");
                }

                return new TestingAnswer().WithContent("ok");
            });

            TestingClientOptions options = new(turning);
            options.AddDirective(new TestingDirective(_ => eachIntentionCount++), PipelineDirectivePosition.EachIntention);
            options.AddDirective(
                new RetryDirective(maximumRetries: 3, delayPolicy: Delaying.CreateFixed(TimeSpan.Zero)),
                PipelineDirectivePosition.EachRetry);
            options.AddDirective(new TestingDirective(_ => beforeTurningCount++), PipelineDirectivePosition.BeforeTurning);

            Pipe pipe = Pipe.Create(options, answerAnalyzer: null);
            AdjacencyPair pair = pipe.CreateAdjacencyPair();

            // Act
            await pipe.TurnAsync(pair, CancellationToken.None);

            // Assert: EachIntention directives run once no matter how many retries happen;
            // BeforeTurning directives sit inside the retry loop and run once per attempt.
            Assert.Equal(1, eachIntentionCount);
            Assert.Equal(3, beforeTurningCount);
        }

        [Fact(Skip =
            "Pipe only exposes TurnAsync today. Directive and Turning both have a " +
            "synchronous Process path, but Pipe never drives it end-to-end - see Pipe.cs.")]
        public void TurnAsync_SynchronousEntryPoint_IsNotYetImplemented()
        {
        }

        [Fact(Skip =
            "ConversationContext is an empty placeholder (see ConversationContext.cs). " +
            "Pipe.CreateAdjacencyPair(ConversationContext) has a // TODO: dialog state and " +
            "does not use the context it is given - see Pipe.cs.")]
        public void CreateAdjacencyPair_WithConversationContext_ShouldCarryDialogStateAcrossTurns()
        {
        }
    }
}
