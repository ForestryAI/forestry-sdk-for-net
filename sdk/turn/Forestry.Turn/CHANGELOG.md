# Release History

## 1.0.0-beta.1

### Breaking Changes
- Renamed `Question`/`QuestionType`/`QuestionContent`/`QuestionDimensions` to
  `Intention`/`IntentionType`/`IntentionContent`/`IntentionDimensions`, and
  `PipelineDirectivePosition.EachQuestion` to `EachIntention`.
- Renamed `Transition`/`TransitionDirective` to `Turning`/`TurningDirective`,
  `ClientOptions.Transition` to `ClientOptions.Turning`,
  `PipelineDirectivePosition.BeforeTransition` to `BeforeTurning`, and `Pipe.TransitionAsync` to
  `Pipe.TurnAsync` — the project is Turn, and an intention is turned into an answer, not
  transitioned.

### Other Changes
- Added `PipeTests` covering the end-to-end pipeline shape: a successful turning, retry until
  success, retry exhaustion, and directive position ordering (`EachIntention` once,
  `BeforeTurning` once per attempt). Two pending tests document known gaps: `Pipe` has no
  synchronous entry point, and `ConversationContext` isn't wired into `CreateAdjacencyPair` yet.
- Fixed `TestingTurning` (test helper, formerly `TestingTransition`) never invoking its turning
  delegate or setting `AdjacencyPair.Answer`, which made it unusable for anything beyond
  construction.
