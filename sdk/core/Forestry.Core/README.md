# Forestry Core

The core for the Forestry SDK's clients, in the spirit of Azure.Core in the Azure SDK for .NET. It
holds client options and the pipeline that every client rides on, and it is where other shared
pieces, such as monitoring, are expected to land. The SDK's
[README](../../../README.md#big-data-clients) explains the whole path, from a device document to rows
that feed a client's pipeline and on to a big data resource.

Formerly Forestry.Turn: "Turn" named one mechanism, the pipeline; "Core" names what the package is
becoming. Type names did not change.

## The pipeline

The pipeline uses a turn-taking model borrowed from conversation analysis: an **intention** is raised,
goes through a chain of **directives**, and comes back paired with its **answer**. It is the .NET
counterpart to the pipeline in the private Forestry AI SDK for Python.

Unlike Azure.Core, a client here is not mainly an HTTP client. In a big data client, the rows from
deserialization are gathered into chunks and carried through the pipeline to the resource: the
**turning** is the call to the resource (writing a file, an upload), and the answer is the
resource's response, which directives such as retry inspect. What an ingestion as a whole reports is
**monitoring**, not an answer.

## Concepts

- **`Intention`**: what is carried through the pipeline: a type, a content payload, and dimensions
  used to route and annotate it.
- **`Answer`**: the result a turning produces for an intention.
- **`AdjacencyPair`**: one intention paired with its eventual answer, plus the bookkeeping the
  pipeline needs on the way (retry count, timing, cancellation).
- **`Turning`**: turns an intention into an answer. This is the extension point where a client
  makes the actual call to its resource.
- **`AnswerAnalyzer`**: decides whether an answer, or an exception raised while producing one,
  should be retried.
- **`Directive`** and **`Pipe`**: the pipeline itself. A `Pipe` runs an ordered chain of directives
  that wrap the turning and end in it.

## Pipeline shape

Directives run in four positions, in this order:

1. **`EachIntention`**: once per intention, before anything else (e.g. enrichment).
2. **`EachRetry`**: around every attempt, retries included. The built-in `RetryDirective` lives
   here: it runs the rest of the pipeline, asks the `AnswerAnalyzer` whether a failure or an answer
   flagged as an error can be retried, and if so waits out a `Delaying` policy (exponential backoff
   by default) and tries again, up to `RetryOptions.DefaultMaximumRetries`.
3. **`BeforeTurning`**: immediately before the turning runs.
4. **Turning**: the last step, not supplied as a directive but added from `ClientOptions.Turning`.

```csharp
using Forestry.Core;

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

Directives for a single call can also be attached to one `AdjacencyPair` (`PositionedDirectives`)
without changing the shared pipe.

## Where it is heading

Not a complete list: an idea of what the core will hold for clients.

- **Client options shared by every client**: how a client takes in documents, and how it chunks,
  flushes and sends rows. Each library (e.g. Forestry.StanForD.Harvesting) plugs in its own policies
  and directives with defaults the client can adjust.
- **Writing as a directive** that owns a format writer: JSON Lines first, Parquet later
  ([Forestry.Parquet](../../parquet/Forestry.Parquet/README.md)).
- **Monitoring**: exceptions, metrics and events, with the client deciding where they go, likely
  through a directive. It may land here or in a Forestry.Monitoring package of its own.
- **Turnings per resource**, in the clients that use the core, such as the first local client and
  later `Forestry.StanForD.Azure.BlobStorage`.

## Status

The pipeline mechanics (directive ordering, retry and backoff, the adjacency pair's lifecycle) are the
most developed part of this package, and [`PipeTests`](test/PipeTests.cs) exercises them end to end:
a successful turning, retrying past transient failures, retry exhaustion, and directive ordering
across attempts. Use it as the reference for how a client rides on the pipeline. Still open:

- No concrete `Turning` ships yet; a client wires one up to reach its resource.
- `ConversationContext` is an empty placeholder, and `AdjacencyPairContext` and dialog state are not
  wired up yet: `Pipe.CreateAdjacencyPair(ConversationContext)` does not use the context it is given,
  which a pending test in `PipeTests` flags.
- `Pipe` only exposes `TurnAsync`; `Directive` and `Turning` both support a synchronous `Process`
  path that `Pipe` never drives, also flagged by a pending test.

Not packed or released yet. Earlier renames (`Question` to `Intention`, `Transition` to `Turning`)
are in the [CHANGELOG](CHANGELOG.md).

## Tests

```bash
dotnet test sdk/core/Forestry.Core/Forestry.Core.slnx
```
