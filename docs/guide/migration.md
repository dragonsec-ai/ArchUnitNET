# Migrating from ArchUnit 2.4.0-alpha.2

The promoted implementation is a breaking replacement, developed by Robey Beswick as
ArchUnitSharp and adopted on 5 October 2026. Historical packages and tags are unchanged;
the previous source is preserved in [Deprecated-ArchUnitNET](https://github.com/LukasNiessen/Deprecated-ArchUnitNET).

## Distribution and runtime

The proposed first release is `3.0.0-beta.1`. It is **not yet published**. The source and
packages require .NET 10; a .NET 8 or 9 test project cannot reference them. The application
being inspected is a C# source tree and need not itself target .NET 10.

The composition-root package keeps the `ArchUnit` install identity. Its assembly and
namespaces are `ArchUnitSharp`. Ten supporting `ArchUnitSharp.*` packages carry the same
version. For xUnit, install `ArchUnit` and `ArchUnitSharp.Testing.Xunit`; for a generic
assertion helper, install `ArchUnit` and `ArchUnitSharp.Testing`. All other module
dependencies restore transitively. There are no current native NUnit or MSTest adapters.

To try the locally built packages before publication, follow [the release guide](release.md).
Once the exact version is publicly available, the equivalent xUnit installation is:

```sh
dotnet add package ArchUnit --version 3.0.0-beta.1
dotnet add package ArchUnitSharp.Testing.Xunit --version 3.0.0-beta.1
```

## API changes

| Previous implementation | Promoted implementation |
| --- | --- |
| `using ArchUnitNet;` | `using ArchUnitSharp;` |
| `ArchUnit.ProjectFiles(path)` | `Project.ProjectFiles(ProjectLocator.Locate(path))` |
| `DependOnFiles()` | `DependOn()` |
| `await rule.CheckAsync()` | `rule.Check()` |
| `await rule.PassesAsync()` | `rule.AssertPasses()` from `ArchUnitSharp.Testing.Xunit` |
| Framework-specific old adapters | xUnit adapter or `RuleAssert.Passes(rule)` |
| `Metrics<T>()` | Source selection with `Project.Metrics(location)`; type-scoped entry is unsupported |
| `{Layer}` / `{Slice}` grouping | Explicit named layer definitions / slice glob captures |

The following examples belong inside tests with these imports and a location pointing to
the source project. `eng/package-consumer/ArchitectureTests.cs` compiles and exercises the
new files, layers, slices, metrics and assertion surfaces using packages alone.

```csharp
using ArchUnitSharp;
using ArchUnitSharp.Extraction;
using ArchUnitSharp.Testing;
using ArchUnitSharp.Testing.Xunit;

var location = ProjectLocator.Locate("/path/to/source-project");
```

### Files and assertions

Replace `ArchUnit.ProjectFiles(path).InFolder("src/App").ShouldNot().DependOnFiles()`
and its asynchronous assertion with:

```csharp
var rule = Project.ProjectFiles(location)
    .InFolder("src/App/**").ShouldNot().DependOn().InFolder("src/Storage/**");
rule.AssertPasses();
// Alternative without a framework-native adapter:
RuleAssert.Passes(rule);
// Or inspect structured violations directly:
var violations = rule.Check();
```

`InFolder("src/App/**")` includes the base folder and its descendants. Dependencies
still follow local `using` imports, not every semantic type reference (issue #47).
Repeated checks cache extraction within a process. If a tool edits the source between
checks, use the documented `CheckOptions.ClearCache` option or `GraphCache.Clear()`.

### Layers

The previous `{Layer}` extraction and `Where(Layer.Defined(...)).MayOnlyDependOn(...)`
becomes explicit declarations and a named constraint:

```csharp
Project.Layers(location)
    .Layer("App").DefinedByFolder("src/App")
    .Layer("Models").DefinedByFolder("src/Models")
    .WhereLayer("App").MayOnlyDependOnLayers("Models")
    .AssertPasses();
```

### Slices and cycles

`DefinedBy("src/{Slice}/**").Should().BeAcyclic()` has no direct slice-cycle equivalent
in this implementation. Use slice dependency constraints where appropriate:

```csharp
Project.Slices(location).DefinedBy("src/(**)/*.cs")
    .ShouldNot().ContainDependency("src/Models/**", "src/App/**")
    .AssertPasses();
```

File cycles are supported separately; they are not the same as cycles in a collapsed
slice graph:

```csharp
Project.ProjectFiles(location).Should().HaveNoCycles().AssertPasses();
```

### Metrics

Replace reflection/type-scoped metrics and old threshold method names with a source
selection and the new threshold grammar. For example:

```csharp
Project.Metrics(location).Count().LinesOfCode().ShouldBeBelowOrEqual(300).AssertPasses();
Project.Metrics(location).Count().MethodCount().ShouldBeBelow(20).AssertPasses();
```

Lines of code counts non-blank source lines. `ShouldBeBelow` is strict; use
`ShouldBeBelowOrEqual` for an inclusive maximum. Failed metric assertions now format
the measured value and expected condition through both generic and xUnit helpers.

## Legacy feature status

| Legacy feature | Current status |
| --- | --- |
| File boundaries, naming, file cycles | Replaced by the current files DSL |
| Named layers | Replaced by explicit declarations and layer constraints |
| Slice grouping and dependency rules | Replaced by capture globs and current slice rules |
| Slice-level cycle API | Deferred; file-cycle checks are not equivalent |
| Count/cohesion metrics | Replaced by source-based count/LCOM rules; review metric definitions |
| Architecture presets | Deferred; express policies explicitly |
| JSON configuration | Deferred; declare rules in C# |
| Baselines/snapshots | Deferred; no migrated baseline API |
| SARIF/JSON/text/HTML violation exports | Deferred; graph exports and metric HTML reports are separate features |
| Graph reports | Current DOT/Mermaid/D2/CSV/JSON/HTML exporters |
| Profiling API | Deferred |
| xUnit integration | Replaced by synchronous `AssertPasses` / `AssertFails` |
| Native NUnit/MSTest adapters | Deferred; use framework-neutral `RuleAssert` |

Empty selections fail by default. Do not use `AllowEmptyTests` to conceal a migration
that selects the wrong files. Known selector differences beyond trailing `/**` remain
tracked in issue #54; this release does not claim glob compatibility with every sibling.

## Remaining release work

Issue #53 stays open until the promoted implementation's license/provenance is confirmed,
the package identity/version choices are reviewed, the complete package graph is published
and publicly verified, and the website accurately reflects this API and runtime boundary.
The owner of the former `RobeyBeswick/ArchUnitSharp` repository must also coordinate its
redirect or retirement; this change does not claim that owner action is complete.
