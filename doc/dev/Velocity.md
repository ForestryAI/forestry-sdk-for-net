# Velocity

How task time is measured across this SDK, and why. Twofold purpose: reduce churn between
architecture and code, and keep AI use from quietly eroding the developer's own understanding of
what got built. Not anti-AI - the goal is that decisions stay part human, part AI, with enough
visibility into each to tell whether that split is working. Applies to any task authored with a
written architecture step (the model case is `Forestry.Deserialize.Xml`'s Feature #12 issues), not
just this package.

## Phases

Every task moves through some or all of these. Not every task needs Architecture (a small bug fix
might not), but the phase list and its order don't change.

| Phase | Covers | Normally done by | Churn? |
|---|---|---|---|
| Requirements Review | Gathering enough information about the problem to start authoring the task - happens in Backlog, before Architecture | developer* | yes |
| Architecture | Ambiguity → decisions → a state/assertion table the code can be written against | developer | no - own goal, see below |
| Test shell | Full, assertion-shaped test cases written from the architecture text alone, before code exists | Claude | n/a |
| Understanding | Reading the architecture and the shells, then demonstrating real comprehension of the design before coding starts | developer | yes |
| Test review | Developer turns the shells into real, verified test assertions, catching missing/extra cases against the architecture table | developer | yes |
| Coding | Implementation | developer | no - the yardstick churn is measured against |
| Test-bug fix | Fixing failures once both code and shells exist | developer | yes |

\* **Normally done by** assumes one person plays every role, matching how this SDK is developed
today. Requirements Review specifically may end up being a separate, more traditional architect
role once that's no longer true - consolidating requirements from other stakeholders (customers,
etc.) takes dedicated time a developer coding day-to-day may not have. Where that split exists, read
"developer" in this table as whichever role actually did that phase.

**Churn** is Requirements Review + Understanding + Test review + Test-bug fix. If a task is
specified clearly enough before Architecture starts, and the architecture itself is genuinely clear,
gathering requirements should be quick, understanding the design should be quick, the test shells
should closely match what gets built, and debugging real failures should be rare - coding itself
should be straightforward. Churn is the cost of the requirements and architecture *not* having done
that job; Coding is what a well-specified, well-architected task should mostly cost.

Each phase gets an **estimate** (written before the phase starts) and an **actual**, plus whether
the phase was delegated to AI. Test shell is always AI for now; the others are marked Y/N per task
so AI-assisted and unassisted phases can be compared later, not just averaged together.

**Test shell is actual-only, deliberately** - no estimate is logged for it, for now. If
architecture authoring is doing its job, any future problem in going from architecture to shells is
expected to show up as a Table defect (the architecture was unclear), not as AI shell-writing being
slow or unpredictable enough to need its own estimate. Revisit if that expectation turns out wrong -
i.e. if shell creation itself starts causing churn independent of architecture quality.

## Board stages

How the phases above map onto this SDK's task-tracking columns (Backlog → Ready → In Progress →
Review → Done):

| Transition / column | Phase(s) | Recorded |
|---|---|---|
| within Backlog | Requirements Review | Requirements Review est/act |
| Backlog → Ready | Architecture, then Claude writes the Test shells | Architecture est/act |
| Ready → In Progress | Understanding, using the shells already written as an aid | Understanding est/act, the checkpoint restatement |
| within In Progress | Test review (shells → real assertions), then Coding | Test review est/act, Coding est/act |
| In Progress → Review | Test-bug fix - the developer (and architect, if separate) triage real test failures | Test-bug fix est/act, defect classification |
| Review → Done | completeness gate | every column has a value → Churn ratio computed |

The Test shell step sits before Understanding on purpose - concrete cases can build understanding
faster than reading prose alone. The Understanding checkpoint below still requires explaining the
architecture's own decision, not describing what the shells do, so this doesn't turn into reading
Claude's code instead of the task.

## Goals

Current policy, not fixed forever - revisit once enough tasks are logged:

- **Architecture: max 4 hours per task.** An architect can't spend unlimited time on one task. #22
  is the first real data point and landed right at this ceiling, which is part of why it's the
  number chosen here rather than an arbitrary one.
- **Coding: target ~1/4 of that task's actual architecture time**, not a flat number - a task with
  2h of architecture should budget ~30 min of coding, not always the same figure. If a task's own
  coding estimate runs meaningfully above this fraction, that's worth a sentence of justification in
  the issue, the same way #22 justified its 30-minute estimate from the shape of the method itself.
  **This fraction is a hypothesis, not a calibrated constant.** The assumption is that architecture
  time is a proxy for the task's underlying size, and coding scales with size - plausible, and
  related prior art sizes work from design-stage output for the same reason, but normally at a much
  coarser grain (a whole feature, not a few hours of one task's architecture). Whether it holds at
  this granularity is unconfirmed; revisit once several tasks are logged. See Prior art below.

## Understanding checkpoint

The developer reads the architecture together with the test shells Claude already wrote from it,
then writes a short (3-5 sentence) restatement of the architecture's decision, timed as part of
Understanding. The restatement must explain the decision and why it resolves the ambiguity - not
describe what the shells do. Reading Claude's test names back isn't evidence of understanding the
architecture, it's evidence of reading Claude's work. A mismatch against the architecture table
isn't scored - it's a signal that either the task text was unclear (an architecture problem) or the
step got rushed (a process problem), logged as a one-line note, not a grade.

**Preferred concrete form, when the task has a state table (the model case): a short doc comment on
each test shell, one per row, stating which state it represents and why that state produces its
outcome** - instead of, or alongside, the free-form restatement above. This checks understanding row
by row against the table rather than once for the whole design, and it becomes permanent
documentation on the test itself rather than a logged-and-discarded note - a reviewer can mechanically
confirm every row got one. First real use (#22's `ContentReady` shells) caught a genuine
misunderstanding - and notably, it caught one of *Claude's*, not just the developer's: an original
shell comment described one row as character data inside already-open content, but that row's own
precondition (content-ready flag false) rules that out - it was actually the empty element's own
`/>` completing. The developer's independently-written per-row summary was correct where Claude's
inline comment wasn't, caught only because both sides put their reasoning in writing against the
same table rather than one trusting the other's read of it. Use this form by default when a state
table exists; the general restatement above is the fallback when it doesn't.

## Defect / change taxonomy

Every test failure or test-shell edit gets exactly one of these, decided during Test-bug fix:

- **Table defect** - the architecture itself is wrong or silent about the case. Counts against the
  Architecture phase, not Coding - the thing this process exists to drive toward zero.
- **Code defect** - the implementation doesn't match the architecture table. Ordinary coding cost,
  not churn.
- **Test defect** - Claude misread the architecture while writing the shell. Claude's error, logged
  separately from the developer's own time.
- **Shell-to-assertion drift** - a test shell's assertion hardcoded a state change that actually
  belongs to an external (out-of-scope) step, and that assumption broke once the external step's
  own behavior was pinned down - not any assertion needing a rewrite. #22's example: a shell
  asserted a specific content-ready-flag value that depended on `ElementStack.Pop`'s semantics,
  which are explicitly out of #22's own scope; the assumption broke once `Pop` changed elsewhere,
  not because `ContentReady()` itself was wrong. Distinct from a plain test defect (a misread of
  the architecture under test) and from a code defect (the implementation under test being wrong) -
  this is specifically the shell reaching outside its own step's scope.

## Metrics

Two groups, computed from the same per-task numbers above, sliced differently for two different
audiences.

### Group A - project management (is architecture producing codeable work?)

- **Architecture overrun** = Architecture actual − 4h goal. Consistently positive means the 4h
  ceiling itself needs revisiting, or tasks need splitting smaller.
- **Coding ratio** = Coding actual ÷ Architecture actual. Target ~1/4, trended across tasks.
- **Understanding ratio** = Understanding actual ÷ Architecture actual. The same question as Coding
  ratio, asked of the Understanding phase instead: how much of the architecture investment converts
  into fast comprehension. #22: 15m ÷ 4h ≈ 0.06 - first data point, no target yet.
- **Churn ratio** = (Requirements Review + Understanding + Test review + Test-bug fix) actual ÷
  Coding actual. Coding is the "should be straightforward" yardstick; this says how much friction
  sits around it. No target yet - the point of logging is to find out what normal looks like before
  setting one.

**Understanding ratio's confound is sharper than the usual task-size one, right now**: whoever does
Understanding today is usually the same person who wrote Architecture, picking it straight back up -
fast comprehension there partly just means remembering a decision made hours earlier, not that the
text would read clearly to someone encountering it cold. The ratio becomes a real test of
architecture clarity under either of two conditions: two separate people (a developer reading
someone else's finished architecture for the first time), or the same person returning to their own
task after enough time has passed that recall genuinely isn't doing the work anymore - the
architecture has to be understood again, not remembered. Outside those two cases, treat it as
measuring recall speed as much as text clarity.

### Group B - development team (where does architecture-to-code friction come from?)

- **Requirements share** = Table defects ÷ all defects.
- **Testing share** = (Test defects + Shell-to-assertion drift) ÷ all defects.
- **Learning signal** = Understanding-checkpoint mismatches ÷ tasks.
- **AI effectiveness** = the Test defect rate specifically within Claude-written shells (Test
  defects ÷ total shell cases), which isolates Claude's own error rate from the architecture's
  clarity. The actual÷estimate half of this originally proposed - comparing AI-delegated phases
  against developer-run ones - is deferred: Test shell doesn't carry an estimate (see Phases
  above), so there's nothing to compute that ratio against right now.

Group A is coarse and rolls up across tasks - it's the number that says the process is or isn't
working. Group B is diagnostic - it says *why*, so a task's authoring or the AI workflow itself can
be adjusted. Neither is a per-developer scorecard; see Prior art below on why that distinction
matters once this stops being solo work.

**Candidate signal, not yet a metric: Understanding actual > Coding actual, paired with a very low
Coding ratio.** #22 (Understanding 15m vs Coding 10m; Coding ratio ~0.04, well under the
already-small ~1/4 hypothesis) is the first data point suggesting this combination indicates real
engagement with the architecture rather than reflexively accepting AI output. The two facts aren't
independent evidence for the same conclusion, though: a low Coding ratio *by itself* is ambiguous -
rubber-stamping the shells (typing whatever makes them pass without real thought) would also
produce fast coding, so speed alone can't distinguish "fast because deeply understood" from "fast
because barely engaged." What actually lowers rubber-stamping probability is the *pairing* - real,
logged, non-trivial understanding time sitting right before that fast coding, which is what makes
the low ratio something other than suspicious. Two caveats before this becomes more than an
observation: it's a proxy for *time spent*, not the *quality* of understanding - the per-row
checkpoint's actual content (see Understanding checkpoint above) is the direct signal, this is at
best a cheap secondary one; and it's confounded by task size - a task as small as #22 makes a high
Understanding-to-Coding ratio and a low Coding ratio both easy to hit regardless of engagement,
while a large architecture with a genuinely simple implementation could show either pattern without
meaning anything bad. Watch it, don't target it - the usual Goodhart risk applies the moment it
becomes something to hit.

## Recording

**The developer fills in the log** - actual hours, defect classification, the understanding-match
note - not Claude, and not automated from commit trailers. The act of recording and classifying is
itself part of the learning goal this file exists for; delegating it away would defeat that, the
same reason Test shell writing is the one phase that's explicitly not developer time. Claude may
total the log, compute the ratios above, and cross-check recorded hours against commit `Effort:`
trailers as a sanity check, but doesn't originate the judgment calls.

**This file is not exempt from [Goodhart's Law](https://en.wikipedia.org/wiki/Goodhart%27s_law):**
the moment filling it in becomes the goal rather than a byproduct of actually understanding a task,
it's producing exactly the noise it exists to avoid - the same reason task estimates in GitHub/Azure
DevOps tend to get ignored or gamed once "get code running and tested" is the only thing actually
rewarded. Never tie this log to performance review or compensation. One concrete tell to watch for:
if the Understanding-checkpoint restatement goes boilerplate or one-line for several tasks running,
that's the field being satisfied rather than used - worth a look back at the log, not just the next
task's number.

Raw per-task numbers should live in the repo, not GitHub Issues/Project fields alone - reading a
plain-text table back is faster than paging through issue history or an API. **Open, not yet
decided**: whether the log stays a table appended to this file, or moves to a separate
`Velocity-Log.md`/CSV once enough tasks exist that one file gets unwieldy. Starting with a table
here; split out later if it does.

## Future

Aspiration, not built: a CI/CD check reading this file's log at each board transition from the
Board stages table above - e.g. blocking Ready until Architecture's row is filled in, or blocking a
Done transition on an empty row or an unexplained Architecture overrun or high Churn ratio. Keeping
the log as plain, stable-column, git-tracked text (rather than only in GitHub's own fields) is what
would make this possible later without redesigning the format first.

### Log

| Task | Requirements Review (est/act) | Architecture (est/act) | Test shell (act) | Understanding (est/act) | Test review (est/act) | Coding (est/act) | Test-bug fix (est/act) | Table | Code | Test | Drift | Understanding match |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| [#22](https://github.com/ForestryAI/forestry-sdk-for-net/issues/22) Content Ready | 0/0m | 4/4h | 10m | 30/15m | 15/5m | 30/10m | 15/5m | 0 | 3 | 1 | 1 | yes |

A short retrospective on how the numbers actually felt - what was better or worse than previous
tasks, any pattern worth watching - belongs as a closing comment on the task itself when it moves
to Done, not duplicated here. This table is the numeric ledger; the issue is where the narrative
goes.

## Prior art

Not a new problem - related work exists, with caveats on where it transfers to a single task at
this small a grain:

- **[Boehm's cost-of-change curve](https://reworkcost.com/boehm-cost-of-change-curve)** (*Software
  Engineering Economics*, 1981) - the origin of "a defect costs more to fix the later it's caught,"
  directly the reasoning behind Table defect being worse than Code defect above. The original
  1x/5x/10x/20x figures are from 1970s waterfall projects at TRW/IBM; Boehm and Basili's 2001
  revision found a meaningfully flatter curve for small, agile, CI/CD-enabled projects - take the
  ordering seriously, not the multipliers.
- **[McConnell's Cone of Uncertainty](https://en.wikipedia.org/wiki/Cone_of_uncertainty)**
  (*Software Project Survival Guide*, 1997) - estimate accuracy narrows as a project moves through
  concept → requirements → design → code, the general case for "architecture time reduces
  downstream uncertainty." Normally applied at whole-project scale, not a single task's multi-hour
  architecture step - one reason the Goals section marks the 1/4 fraction unverified rather than
  settled.
- **[Function Point Analysis](https://www.pmi.org/learning/library/software-measuring-function-point-methodology-6201)**
  (Albrecht) - sizes effort from design/requirements output rather than code volume, the same reason
  this file wants Architecture time to be a size proxy for Coding. Same caveat as the cone: normally
  a whole-application measure, not a per-task one.
- **[Joel Spolsky's Evidence-Based Scheduling](https://www.joelonsoftware.com/2007/10/26/evidence-based-scheduling/)** -
  tracks each estimator's actual÷estimate ratio across many tasks rather than trusting any single
  estimate, because people "get the scale wrong but the relative estimates right." Directly the
  reasoning behind trending the Coding ratio across tasks instead of judging #22 alone.
- **[Specification by Example / Acceptance Test-Driven Development](https://en.wikipedia.org/wiki/Acceptance_test-driven_development)**
  (Adzic) - writing executable tests from a specification before code exists, as a named, established
  practice - what the Test shell phase already does.
- **[DORA / *Accelerate*](https://dora.dev/guides/dora-metrics/)** (Forsgren, Humble, Kim) - the
  precedent for splitting an organizational/delivery-level metric (Group A here) from team-level
  diagnostics (Group B) - and an explicit warning that applying delivery metrics at the individual
  level creates perverse incentives. Worth remembering if this file ever tracks more than one
  developer: Group B is for finding where friction comes from, not for scoring a person.
- **[Goodhart's Law](https://en.wikipedia.org/wiki/Goodhart%27s_law)** - "when a measure becomes a
  target, it ceases to be a good measure." The reason GitHub/Azure DevOps task estimates tend to get
  ignored or gamed once shipping code is the only thing actually rewarded, and an explicit risk this
  file itself isn't exempt from - see Recording above.
- **Shihab, Hundhausen et al., "The Effects of GitHub Copilot on Computing Students' Programming
  Effectiveness, Efficiency, and Processes in Brownfield Programming Tasks,"** ACM ICER 2025, DOI
  [10.1145/3702652.3744219](https://dl.acm.org/doi/10.1145/3702652.3744219) - peer-reviewed evidence
  that AI's effect on a developer's actual understanding, not just task speed, is an active research
  question, not a settled one. Cited for existing, not for any specific figure - its numbers weren't
  independently confirmed here.
