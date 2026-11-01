# Building and releasing packages

The packaging proposal keeps the root NuGet ID `ArchUnit`, with `ArchUnitSharp` assemblies
and namespaces and ten `ArchUnitSharp.*` supporting packages. All eleven share one version,
currently `3.0.0-beta.1`, and target .NET 10. The legacy `2.4.0-alpha.2` package is unchanged.
The original implementation is attributed to Robey Beswick in every package.

## Local verification

With .NET 10 and Python 3.9 or newer:

```sh
dotnet restore ArchUnitSharp.sln -p:Version=3.0.0-beta.1
dotnet build ArchUnitSharp.sln -c Release --no-restore -p:Version=3.0.0-beta.1
dotnet format ArchUnitSharp.sln --verify-no-changes --no-restore
dotnet test ArchUnitSharp.sln -c Release --no-build --no-restore -p:Version=3.0.0-beta.1
dotnet pack ArchUnitSharp.sln -c Release --no-build --no-restore -p:Version=3.0.0-beta.1 -o artifacts/packages
python eng/verify-packages.py --version 3.0.0-beta.1
```

Use an empty output directory when changing versions: the verifier rejects unexpected
or missing packages. It inspects IDs, versions, all internal dependencies, net10.0 DLLs,
XML documentation, repository metadata, README and attribution. It then copies the xUnit
consumer outside this checkout, uses a fresh NuGet package cache, maps all ArchUnit package
IDs exclusively to the local feed, and restores external dependencies from nuget.org.
No project references or source-built DLL paths are permitted in that consumer.

The consumer tests forbidden dependencies, cycles and LOC limits through failure and
repair, plus layer/slice/graph examples. CI repeats package consumption on Linux, Windows
and macOS. This is a prerequisite for release, not a claim of complete platform coverage.

## License and publication gate

The promoted source currently has no confirmed license declaration. Both the canonical
and former repositories lack a recognized license. The old package's Apache-2.0 metadata
does not establish the rewrite's license. **Publication remains blocked under issue #53.**

After resolving provenance with the implementation owner, commit the reviewed root
`LICENSE` text. Shared package metadata includes it as a NuGet file license automatically.
The publication job requires that license in every artifact; it does not infer an SPDX
identifier or silently reuse the old license. Local inspection artifacts are for evaluating
the packaging and are not a completed public release.

## Workflow

The `prepare or publish packages` workflow is manual. Its default `publish=false` runs
formatting, the full test suite, package inspection and the isolated consumer, and retains
the exact package artifacts. Review these before enabling publication.

For publication, run from `main` with the approved version and `publish=true`. Configure
the repository's `nuget` environment and its `NUGET_API_KEY` with publishing rights for
all eleven IDs. Confirm ownership of new IDs before release. The job downloads the
already-tested artifacts, enforces the license gate, pushes them and checks installation
from public NuGet with a fresh cache. Indexing retries are bounded; inspect failures rather
than declaring a successful publication merely from a push response.

Publish the complete graph: `ArchUnit`, `ArchUnitSharp.Common`, `ArchUnitSharp.Extraction`,
`ArchUnitSharp.Projection`, `ArchUnitSharp.Files`, `ArchUnitSharp.Graph`,
`ArchUnitSharp.Layers`, `ArchUnitSharp.Slices`, `ArchUnitSharp.Metrics`,
`ArchUnitSharp.Testing`, and `ArchUnitSharp.Testing.Xunit`.

After public installation is verified, update the README's unpublished status and the
website's package version, .NET 10 requirement, namespaces, synchronous examples, adapter
list, license and feature claims. Keep historical NuGet download totals labelled as
historical. Do not advertise the new implementation as published before this succeeds.
