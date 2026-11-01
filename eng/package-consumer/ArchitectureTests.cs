using ArchUnitSharp;
using ArchUnitSharp.Common.Extraction;
using ArchUnitSharp.Extraction;
using ArchUnitSharp.Metrics;
using ArchUnitSharp.Testing;
using ArchUnitSharp.Testing.Xunit;
using Xunit;

public sealed class ArchitectureTests
{
    [Fact]
    public void Forbidden_dependency_fails_then_passes_after_removing_the_import()
    {
        using var fixture = new SourceProject();
        fixture.Write("src/Domain/Order.cs", "using Demo.Storage; namespace Demo.Domain; public class Order { }");
        fixture.Write("src/Storage/Store.cs", "namespace Demo.Storage; public class Store { }");

        ICheckable Rule() => Project.ProjectFiles(fixture.Location)
            .InFolder("src/Domain/**").ShouldNot().DependOn().InFolder("src/Storage/**");

        Assert.Single(Rule().Check());
        Rule().AssertFails();
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => Rule().AssertPasses());
        RuleAssert.Fails(Rule());
        Assert.Throws<AssertionFailedException>(() => RuleAssert.Passes(Rule()));

        fixture.Write("src/Domain/Order.cs", "namespace Demo.Domain; public class Order { }");
        Assert.Empty(Rule().Check());
        Rule().AssertPasses();
        RuleAssert.Passes(Rule());
    }

    [Fact]
    public void Cycle_fails_then_passes_after_removing_one_edge()
    {
        using var fixture = new SourceProject();
        fixture.Write("src/Orders/Order.cs", "using Demo.Receipts; namespace Demo.Orders; public class Order { }");
        fixture.Write("src/Receipts/Receipt.cs", "using Demo.Orders; namespace Demo.Receipts; public class Receipt { }");

        ICheckable Rule() => Project.ProjectFiles(fixture.Location).Should().HaveNoCycles();

        Assert.Single(Rule().Check());
        Rule().AssertFails();
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => Rule().AssertPasses());
        fixture.Write("src/Receipts/Receipt.cs", "namespace Demo.Receipts; public class Receipt { }");
        Assert.Empty(Rule().Check());
        Rule().AssertPasses();
    }

    [Fact]
    public void Lines_of_code_failure_has_a_diagnostic_and_passes_after_shortening_the_file()
    {
        using var fixture = new SourceProject();
        fixture.Write("src/Orders/Order.cs", "namespace Demo.Orders;\npublic class Order\n{\n    public int Id;\n}\n");

        ICheckable Rule() => Project.Metrics(fixture.Location).Count().LinesOfCode().ShouldBeBelowOrEqual(3);

        var violation = Assert.IsType<MetricViolation>(Assert.Single(Rule().Check()));
        Assert.Equal(5, violation.Value);
        Rule().AssertFails();
        RuleAssert.Fails(Rule());
        var error = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => Rule().AssertPasses());
        Assert.Contains("src/Orders/Order.cs", error.Message);
        Assert.Contains("5", error.Message);
        Assert.Contains("3", error.Message);
        Assert.Throws<AssertionFailedException>(() => RuleAssert.Passes(Rule()));

        fixture.Write("src/Orders/Order.cs", "namespace Demo.Orders;\npublic class Order { public int Id; }\n");
        Assert.Empty(Rule().Check());
        Rule().AssertPasses();
        RuleAssert.Passes(Rule());
    }

    [Fact]
    public void Migration_examples_for_layers_slices_and_graph_work_from_packages()
    {
        using var fixture = new SourceProject();
        fixture.Write("src/App/Order.cs", "using Demo.Models; namespace Demo.App; public class Order { }");
        fixture.Write("src/Models/Model.cs", "namespace Demo.Models; public class Model { }");
        var location = fixture.Location;

        Project.Layers(location)
            .Layer("App").DefinedByFolder("src/App")
            .Layer("Models").DefinedByFolder("src/Models")
            .WhereLayer("App").MayOnlyDependOnLayers("Models")
            .AssertPasses();
        Project.Slices(location).DefinedBy("src/(**)/*.cs")
            .ShouldNot().ContainDependency("src/Models/**", "src/App/**")
            .AssertPasses();
        Assert.Contains("Order.cs", Project.Graph(location).ToMermaid());
    }

    private sealed class SourceProject : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "archunit-consumer-" + Guid.NewGuid().ToString("N"));

        public SourceProject() => Write("Fixture.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

        public ProjectLocation Location => ProjectLocator.Locate(_root);

        public void Write(string relativePath, string content)
        {
            var path = Path.Combine(_root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            // These tests edit source within one process, unlike separate dotnet test runs.
            GraphCache.Clear();
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
