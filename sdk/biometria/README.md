# Forestry Biometria

The [Biometria](https://www.biometria.se/) knowledge of the Forestry SDK, built on
[Forestry.StanForD.Harvesting](../stanford/Forestry.StanForD.Harvesting/README.md). Nothing is built
yet; this README states the goals. The SDK's [README](../../README.md#big-data-clients) explains the
whole path, from a device document to telemetries that feed a client's pipeline and on to big data.

## Sender's Extension

Most production machines in Sweden have a program called Sender installed, which moves the machine's
StanForD documents to Biometria. On the way, Sender adds an `Extension` element with variables of its
own. They fall into four groups:

- **The production planner**: the user ID of the actor who planned the production.
- **Sender**: e.g. its version.
- **The operating system**: e.g. Windows.
- **The file**: e.g. when it was created.

The production planner is the most important. That actor reference is the primary access to the
data: who may read and write it, and who is notified when something goes wrong.

## Extending Harvesting telemetries

Forestry.Biometria will have its own `AddHarvesting`, on top of Forestry.StanForD.Harvesting's:

- **Telemetries**: Stem and Log telemetries extended with the Extension's variables, copied onto every telemetry so each
  stays self-contained, as the dimensions are.
- **Policies** that validate the variables, the production planner above all.
- **Directives** for what is particular to Biometria, such as checking production planners.

## A closed system

Biometria is closed: harvesting contracts, actors and the other entities have no REST API to look up.
Instead, Biometria offers subscriptions to entity changes, PapiNet entities alongside its own, and
each forestry company keeps what it wants of them.

So a policy cannot ask Biometria whether a harvesting contract is real while a document is being
ingested. What it can do is model how Biometria performs such checks. Those policies are optional,
for clients that want them, and never stand in for Biometria's own.

Whether this package also helps with subscribing to entity changes is open. If it does, it will
likely build on [Forestry.PapiNet](../papinet/README.md) for the shared entity model.

## Status

Not started: no source and no `.csproj` yet. It comes after Forestry.StanForD.Harvesting; see
[where the POC stands](../../README.md#where-the-poc-stands).
