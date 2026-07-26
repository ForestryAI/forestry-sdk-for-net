# Forestry Turn

Forestry Turn drives a single request through to a resolved answer using a turn-taking model
borrowed from conversation analysis: something is raised, it goes through a pipeline of
directives, and it comes back paired with its answer. It's the .NET counterpart to the pipeline
in the private Forestry AI SDK for Python.

> **Naming notes:**
> - This used to be called `Question` (`QuestionType`, `QuestionContent`, `QuestionDimensions`)
>   in code as well as docs. It's now `Intention` throughout — "question" implied something
>   posed to a person, where what's actually flowing through the pipeline is closer to
>   "here's what I want to happen."
> - The extension point used to be called `Transition`. It's now `Turning` — the project itself
>   is called Turn, and an intention is *turned* into an answer, not transitioned. (`Turn` itself
>   was avoided for the class name because "turn" is already used as a noun elsewhere for one
>   half of an adjacency pair, e.g. an intention or answer "turn" — `Turning` keeps that clear.)

## Concepts

- **`Intention`** — the thing being carried through the pipeline: a type, a content payload, and
  a set of dimensions used to route and annotate it.
- **Answer** — the result a `Turning` produces for an intention.
- **`AdjacencyPair`** — pairs one intention with its eventual answer, plus the bookkeeping
  (retry count, process timing, cancellation) the pipeline needs while getting there.
- **`Turning`** — turns an intention into an answer. This is the extension point where a
  concrete client implements the actual call out to Forestry AI.
- **`AnswerAnalyzer`** — decides whether an answer (or an exception raised while producing one)
  should trigger a retry.
- **`Directive`** / **`Pipe`** — the pipeline itself. A `Pipe` runs an ordered chain of
  directives that wrap the `Turning`, terminating in it.

## Pipeline shape

Directives run in four positions, in this order:

1. **`EachIntention`** — once per intention, before anything else (e.g. enrichment).
2. **`EachRetry`** — around every attempt, including retries. The built-in `RetryDirective` lives
   here: it runs the rest of the pipeline, asks the `AnswerAnalyzer` whether a failure or
   error-flagged answer is retryable, and if so waits out a `Delaying` policy (exponential
   backoff by default) and tries again — up to `RetryOptions.DefaultMaximumRetries`.
3. **`BeforeTurning`** — immediately before the turning runs.
4. **Turning** — the terminal step; not user-supplied, added automatically from
   `ClientOptions.Turning`.

```csharp
public class MyClientOptions : ClientOptions
{
    public MyClientOptions(Turning turning) : base(turning) { }
}

var options = new MyClientOptions(new MyTurning());
options.AddDirective(new RetryDirective(maximumRetries: 3), PipelineDirectivePosition.EachRetry);

Pipe pipe = Pipe.Create(options, answerAnalyzer: null); // falls back to AnswerAnalyzer.Shared

AdjacencyPair pair = pipe.CreateAdjacencyPair();
await pipe.TurnAsync(pair, cancellationToken);

Answer answer = pair.Answer;
```

Per-call directives can also be attached to an individual `AdjacencyPair`
(`PositionedDirectives`) without changing the shared pipe.

## Status

The pipeline mechanics — directive ordering, retry/backoff, adjacency pair lifecycle — are the
most developed part of this SDK, and [`PipeTests`](test/PipeTests.cs) exercises that shape
end-to-end: a successful turning, retrying past transient failures, retry exhaustion, and
directive ordering across attempts. Use it as the reference for how a client is meant to ride
on the pipeline. What's still open:

- No concrete `Turning` ships yet; a client wires this up to actually reach Forestry AI.
- `ConversationContext` is an empty placeholder, and `AdjacencyPairContext` and dialog state are
  not wired up yet (`Pipe.CreateAdjacencyPair(ConversationContext)` has a `// TODO: dialog state`
  and does not use the context it's given) — flagged by a pending test in `PipeTests`.
- `Pipe` only exposes `TurnAsync`; `Directive`/`Turning` both support a synchronous `Process`
  path that `Pipe` never drives — also flagged by a pending test in `PipeTests`.
