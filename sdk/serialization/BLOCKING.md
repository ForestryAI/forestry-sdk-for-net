# BLOCKING.md — Deserialize solution layout

**Purpose:** reorganizing the `.slnx` files under `sdk/serialization/` is paused until it's decided where
`Forestry.StanForD` will live relative to `Forestry.Serialization` / `Forestry.Serialization.Xml`. This
file only tracks *that* the decision is open and what's already known — nothing here has been changed
in the repo yet.

**Why this exists:** VS Code (moving toward Visual Studio behavior) now prompts to add any project
that is `ProjectReference`d from outside its solution. Today that fires for
`Forestry.Serialization.Xml/src/Forestry.Serialization.Xml.csproj` → `..\..\Forestry.Serialization\src\Forestry.Serialization.csproj`,
because each package has its own `.slnx` listing only its own `src` and `test`. It's a warning, not a
build failure, but it will recur for every future cross-package reference.

## Current state

```
sdk/serialization/
  Forestry.Serialization/       Forestry.Serialization.slnx      (src, test)
  Forestry.Serialization.Xml/   Forestry.Serialization.Xml.slnx  (src, test)
sdk/stanford/
  Forestry.StanForD/          Forestry.StanForD.slnx         (src)
```

- The only cross-solution `ProjectReference` today is Xml → Deserialize.
- Nothing in CI, `eng/` or build scripts references the `.slnx` paths. Only `README.md` (build
  commands, lines ~25–35) and `Forestry.Serialization.Xml/ARCHITECTURE.md` (one mention of
  `Forestry.Serialization.Xml.slnx`) name them, so a restructure is cheap.

## Reference model

`Azure/azure-sdk-for-net` `sdk/storage/`: one solution per **service group**
(`Azure.Storage.sln`), sitting beside the package folders (`Azure.Storage.Blobs/{src,tests}`,
`Azure.Storage.Common/{src,tests}`, …), containing every package and test project in the group.
The folder tree here already matches that shape; only the solution placement differs.

## Options (not yet chosen)

1. **Group solution at `sdk/serialization/`** — add `Forestry.Serialization.slnx` listing all four
   projects; delete the two per-package `.slnx` files; update README + ARCHITECTURE.md mentions.
   Resolves the Xml → Deserialize warning. Does *not* by itself answer StanForD.
2. **Minimal** — add `..\Forestry.Serialization\src\Forestry.Serialization.csproj` to the Xml `.slnx`.
   Silences today's warning, keeps the structure, but the same prompt returns for each new
   cross-package reference (including StanForD).

## Open question that blocks the decision

`Forestry.StanForD` is expected to reference the Deserialize package(s) from its own solution later.
Azure treats the service group as the boundary, so either:

- **StanForD joins the same group** — move it under `sdk/serialization/`, or rename the group to
  something broader — and the group `.slnx` (option 1) covers it; or
- **StanForD stays in `sdk/stanford/`** and consumes Deserialize as a **NuGet package reference**, not
  a `ProjectReference` — then no cross-solution reference exists and option 1 alone is enough.

Not decided. Whichever is picked also implies whether `sdk/turn`, `sdk/raindrop` and `sdk/papinet`
(currently standalone; `papinet` is flat, with `src` directly under `sdk/papinet/`) get reshaped to
match for consistency.

## Not part of this pass

Reader/parser work in `Forestry.Serialization.Xml` (see its own `BLOCKING.md`) — unrelated to layout and
not gated by this file.
