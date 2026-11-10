# Contract: packaging gate (constitution C2)

What a package in scope passes before `release.yml` publishes it. Every item is checked by the
pipeline on every pull request (`ci.yml`), so a release finds no surprises.

| Item | Checked by | Today (2026-11-07) |
|---|---|---|
| XML doc comment on every public member | `GenerateDocumentationFile=true` for libraries with `IsShippingLibrary`; CS1591 fails through warnings as errors | ✅ on for all three, 0 missing docs |
| Each public option tested for its default, an override and the values it rejects | Tests in the package's test project; listed below | ✅ table below complete (2026-11-10) |
| Tests pass on every supported target framework | `ci.yml` `build-test` | net10.0 (R8) |
| Performance gate passes | `ci.yml` `performance` | `Forestry.Xml` (allocations), `Forestry.Deserialize.Xml` (memory); `Forestry.Deserialize` has no performance test (P9) |
| Symbols and SourceLink | `.snupkg` produced; SourceLink from the SDK | ✅ on |
| Package metadata | `PackageId`, `Version`, `Description`, `Authors`, license, repository URL, README, release notes | ✅ all but the release notes (US3) |
| No breaking change against the last release, unless intended | SDK package validation with the last release as baseline (P5) | No release yet |

## Public options in scope

Completed from the code on 2026-11-09 (T022). ✅ names the existing test that covers a cell; **Shell**
is a new test shell (T023-T024), reviewed at T026 before any body is written. Test names are shortened.

### `Forestry.Xml`: `ReaderOptions`

| Option | Default | Override | Rejected |
|---|---|---|---|
| `MaxDepth` | 64 when never set. **Shell** `MaxDepth_Default`: 64 levels read, the 65th throws. (`Read_ForDefaultOptions` only reads 3 levels) | ✅ `Read_ForADepthAtTheLimit`, `Read_ForADepthBeyondTheLimit`, `MaxDepth_ForSixtyFour` | ✅ `MaxDepth_ForAValueOutOfRange` (0, -1, 65) |
| `MaxNameLength` | ✅ 128 when never set: `Read_ForAnOverriddenMaxNameLength` (reads 128), `Read_ForANameOneByteBeyondTheLimit` (129 throws) | ✅ `Read_ForAnOverriddenMaxNameLength` (16). **Shell** `MaxNameLength_Override`: the upper bound 2048 is accepted | ✅ `MaxNameLength_ForAValueOutOfRange` (0, -1, 2049) |

### `Forestry.Deserialize`: `DeserializeOptions`

| Option | Default | Override | Rejected |
|---|---|---|---|
| `Rows` | Empty. **Shell** `Rows_Default`; ✅ using it empty throws: `Deserialize_ForOptionsWithoutRowTypes` | **Shell** `Rows_Override`: order added, a repeat kept once, `Add` chains | ✅ not a `Row`: `RowsAdd_ForATypeNotExtendingRow`; ✅ after locking: `RowsAdd_AfterTheOptionsAreLocked`. **Shells** `Rows_Rejects`: an abstract row type, a null array, a null in the array |
| `Converters` | Empty. **Shell** `Converters_Default` | ✅ `ConverterResolutionTests`, `Deserialize_ForABooleanConverterInTheOptions` | ✅ after locking: `ConvertersAdd_AfterTheOptionsAreLocked`. **Shells** `Converters_Rejects`: null, `Clear` after locking |
| `Policies` | ✅ none apply: `Applying_WithoutPolicies` | ✅ `PolicyPrecedenceTests`, `WalkingSkeletonTests.US4` | ✅ `PoliciesAdd_AfterTheOptionsAreLocked` (`Add<TRow>(policy)`), `MakeReadOnly_ForAMemberPolicyOverridingConstruct`. **Shells** `Policies_Rejects`: a null policy on each of the three `Add`s, a null or empty member name, the other two `Add`s after locking |
| `IsReadOnly` / `MakeReadOnly()` | **Shell** `IsReadOnly_Default`: false on new options | **Shell** `MakeReadOnly_Override`: true after the call, a second call does nothing | ✅ `DefinitionValidationTests`: a row type that cannot be defined throws |

`DefinitionPolicy` and `EnumerationPolicy` have no values to set: they are extension points with
built-in steps, covered by `PolicyPrecedenceTests` (`IncludeMember`) and `WalkingSkeletonTests.US4`
(`Construct`, `Set`, `Enumerate`, `Clear`). No shells.

### `Forestry.Deserialize.Xml`: `DeserializeXmlOptions`

No public members beyond `DeserializeOptions` (its overrides are protected or internal). Its XML
default converter is covered by ✅ `Deserialize_ForABooleanConverterInTheOptions` (an override wins).
No shells, so T025 has nothing to add.

### Reviewed at the gate (T026, developer, 2026-11-10)

- `ReaderOptions`'s getters return 0 on a never-set struct, and the reader's constructors apply the
  default. Left as is on purpose: the two states (as given, and with defaults applied) may be useful
  later. The tests check what is read, not the getter.
- A failed `MakeReadOnly()` leaves the options unlocked. Added cell: adding the missing converter and
  calling it again must work (`MakeReadOnly_AfterAFailedBuildAndAddingTheMissingConverter`).
