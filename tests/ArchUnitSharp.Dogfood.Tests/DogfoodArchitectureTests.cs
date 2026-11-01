namespace ArchUnitSharp.Dogfood.Tests;

using ArchUnitSharp.Common.Extraction;

/// <summary>
/// The dogfood suite: runs the library against its own source. The repository is located once and
/// every rule checks the <c>src</c> tree, so the suite enforces the module architecture of
/// <c>AGENTS.md</c> — the kernel stays isolated from the other modules and from every external
/// module outside the standard library and the analysis toolchain, the domain modules stay apart,
/// nothing depends on the public surface, and the module dependency graph is acyclic.
/// </summary>
/// <remarks>
/// Every rule routes through the public surface (<see cref="Project"/> entry points) and the shared
/// <see cref="EmptyTestGuard"/>, so a rule whose subject or object matched nothing — a mislocated
/// repository, a renamed module, a typo — is a violation, never a silent pass. The rules are scoped
/// to <c>src/**</c>: the <c>tests</c> tree depends on the public surface by design and is not part
/// of the shipped library's architecture. The fixture test anchors the graph with a known
/// dependency edge as well as known files, so the suite still guards against an extractor that
/// produces every file but no dependency edges.
/// </remarks>
public class DogfoodArchitectureTests
{
    [Fact]
    public void The_dogfood_suite_sees_the_librarys_own_source()
    {
        IReadOnlyList<string> files = Project.ProjectFiles(Repository.Location)
            .InPath("src/**")
            .Select();

        Assert.Contains("src/ArchUnitSharp.Common/Extraction/Edge.cs", files);
        Assert.Contains("src/ArchUnitSharp/Project.cs", files);
        Assert.Contains("src/ArchUnitSharp.Files/Files.cs", files);
        Assert.Contains("src/ArchUnitSharp.Metrics/Metrics.cs", files);

        IReadOnlyList<Violation> dependency = Project.ProjectFiles(Repository.Location)
            .InPath("src/ArchUnitSharp/Project.cs")
            .Should()
            .DependOn()
            .InPath("src/ArchUnitSharp.Common/**")
            .Check();

        Assert.Empty(dependency);
    }

    [Fact]
    public void Common_depends_on_no_other_module_in_the_library()
    {
        IReadOnlyList<Violation> violations = Project.ProjectFiles(Repository.Location)
            .InFolder("src/ArchUnitSharp.Common/**")
            .ShouldNot()
            .DependOn()
            .InPath("src/**")
            .Except("src/ArchUnitSharp.Common/**")
            .Check();

        Assert.Empty(violations);
    }

    [Fact]
    public void Common_depends_on_no_external_module_outside_the_standard_library_and_the_analysis_toolchain()
    {
        IReadOnlyList<Violation> violations = Project.ProjectFiles(Repository.Location)
            .InFolder("src/ArchUnitSharp.Common/**")
            .ShouldNot()
            .DependOnExternalModules()
            .Matching("*")
            .Except("System.*")
            .Except("Microsoft.CodeAnalysis.*")
            .Check();

        Assert.Empty(violations);
    }

    [Fact]
    public void Domain_modules_do_not_depend_on_each_other()
    {
        IReadOnlyList<Violation> violations = Project.Layers(Repository.Location)
            .Layer("Files").DefinedBy("src/ArchUnitSharp.Files/**")
            .Layer("Layers").DefinedBy("src/ArchUnitSharp.Layers/**")
            .Layer("Slices").DefinedBy("src/ArchUnitSharp.Slices/**")
            .Layer("Graph").DefinedBy("src/ArchUnitSharp.Graph/**")
            .Layer("Metrics").DefinedBy("src/ArchUnitSharp.Metrics/**")
            .WhereLayer("Files").MayNotDependOnLayers("Layers", "Slices", "Graph", "Metrics")
            .WhereLayer("Layers").MayNotDependOnLayers("Files", "Slices", "Graph", "Metrics")
            .WhereLayer("Slices").MayNotDependOnLayers("Files", "Layers", "Graph", "Metrics")
            .WhereLayer("Graph").MayNotDependOnLayers("Files", "Layers", "Slices", "Metrics")
            .WhereLayer("Metrics").MayNotDependOnLayers("Files", "Layers", "Slices", "Graph")
            .Check();

        Assert.Empty(violations);
    }

    [Fact]
    public void Nothing_depends_on_the_public_surface()
    {
        IReadOnlyList<Violation> violations = Project.ProjectFiles(Repository.Location)
            .InPath("src/**")
            .Except("src/ArchUnitSharp/**")
            .ShouldNot()
            .DependOn()
            .InPath("src/ArchUnitSharp/**")
            .Check();

        Assert.Empty(violations);
    }

    [Fact]
    public void The_module_dependency_graph_is_cycle_free()
    {
        IReadOnlyList<Violation> violations = Project.Layers(Repository.Location)
            .Layer("Common").DefinedBy("src/ArchUnitSharp.Common/**")
            .Layer("Projection").DefinedBy("src/ArchUnitSharp.Projection/**")
            .Layer("Extraction").DefinedBy("src/ArchUnitSharp.Extraction/**")
            .Layer("Files").DefinedBy("src/ArchUnitSharp.Files/**")
            .Layer("Graph").DefinedBy("src/ArchUnitSharp.Graph/**")
            .Layer("Layers").DefinedBy("src/ArchUnitSharp.Layers/**")
            .Layer("Metrics").DefinedBy("src/ArchUnitSharp.Metrics/**")
            .Layer("Slices").DefinedBy("src/ArchUnitSharp.Slices/**")
            .Layer("Testing").DefinedBy("src/ArchUnitSharp.Testing/**")
            .Layer("Testing.Xunit").DefinedBy("src/ArchUnitSharp.Testing.Xunit/**")
            .Layer("Surface").DefinedBy("src/ArchUnitSharp/**")
            .WhereLayer("Common").MayOnlyDependOnLayers()
            .WhereLayer("Projection").MayOnlyDependOnLayers("Common")
            .WhereLayer("Extraction").MayOnlyDependOnLayers("Common")
            .WhereLayer("Files").MayOnlyDependOnLayers("Common", "Projection")
            .WhereLayer("Graph").MayOnlyDependOnLayers("Common", "Projection")
            .WhereLayer("Layers").MayOnlyDependOnLayers("Common")
            .WhereLayer("Metrics").MayOnlyDependOnLayers("Common")
            .WhereLayer("Slices").MayOnlyDependOnLayers("Common", "Projection")
            .WhereLayer("Testing").MayOnlyDependOnLayers("Common", "Files", "Layers", "Metrics", "Slices")
            .WhereLayer("Testing.Xunit").MayOnlyDependOnLayers("Common", "Testing")
            .WhereLayer("Surface").MayOnlyDependOnLayers(
                "Common", "Projection", "Extraction", "Files", "Graph", "Layers", "Metrics", "Slices", "Testing", "Testing.Xunit")
            .Check();

        Assert.Empty(violations);
    }
}
