# BLOCKING.md — Deserialize solution layout

**Purpose:** reorganizing the `.slnx` files under `sdk/deserialize/` is paused until it's decided where
`Forestry.StanForD` will live relative to `Forestry.Deserialize` / `Forestry.Deserialize.Xml`. This
file only tracks *that* the decision is open and what's already known — nothing here has been changed
in the repo yet.

**Why this exists:** VS Code (moving toward Visual Studio behavior) now prompts to add any project
that is `ProjectReference`d from outside its solution. Today that fires for
`Forestry.Deserialize.Xml/src/Forestry.Deserialize.Xml.csproj` → `..\..\Forestry.Deserialize\src\Forestry.Deserialize.csproj`,
because each package has its own `.slnx` listing only its own `src` and `test`. It's a warning, not a
build failure, but it will recur for every future cross-package reference.

## Current state

```
sdk/deserialize/
  Forestry.Deserialize/       Forestry.Deserialize.slnx      (src, test)
  Forestry.Deserialize.Xml/   Forestry.Deserialize.Xml.slnx  (src, test)
sdk/stanford/
  Forestry.StanForD/          Forestry.StanForD.slnx         (src)
```

- The only cross-solution `ProjectReference` today is Xml → Deserialize.
- Nothing in CI, `eng/` or build scripts references the `.slnx` paths. Only `README.md` (build
  commands, lines ~25–35) and `Forestry.Deserialize.Xml/ARCHITECTURE.md` (one mention of
  `Forestry.Deserialize.Xml.slnx`) name them, so a restructure is cheap.

## Reference model

`Azure/azure-sdk-for-net` `sdk/storage/`: one solution per **service group**
(`Azure.Storage.sln`), sitting beside the package folders (`Azure.Storage.Blobs/{src,tests}`,
`Azure.Storage.Common/{src,tests}`, …), containing every package and test project in the group.
The folder tree here already matches that shape; only the solution placement differs.

## Options (not yet chosen)

1. **Group solution at `sdk/deserialize/`** — add `Forestry.Deserialize.slnx` listing all four
   projects; delete the two per-package `.slnx` files; update README + ARCHITECTURE.md mentions.
   Resolves the Xml → Deserialize warning. Does *not* by itself answer StanForD.
2. **Minimal** — add `..\Forestry.Deserialize\src\Forestry.Deserialize.csproj` to the Xml `.slnx`.
   Silences today's warning, keeps the structure, but the same prompt returns for each new
   cross-package reference (including StanForD).

## Open question that blocks the decision

`Forestry.StanForD` is expected to reference the Deserialize package(s) from its own solution later.
Azure treats the service group as the boundary, so either:

- **StanForD joins the same group** — move it under `sdk/deserialize/`, or rename the group to
  something broader — and the group `.slnx` (option 1) covers it; or
- **StanForD stays in `sdk/stanford/`** and consumes Deserialize as a **NuGet package reference**, not
  a `ProjectReference` — then no cross-solution reference exists and option 1 alone is enough.

Not decided. Whichever is picked also implies whether `sdk/turn`, `sdk/raindrop` and `sdk/papinet`
(currently standalone; `papinet` is flat, with `src` directly under `sdk/papinet/`) get reshaped to
match for consistency.

## Not part of this pass

Reader/parser work in `Forestry.Deserialize.Xml` (see its own `BLOCKING.md`) — unrelated to layout and
not gated by this file.
