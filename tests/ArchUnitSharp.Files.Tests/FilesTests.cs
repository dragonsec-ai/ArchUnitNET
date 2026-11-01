using ArchUnitSharp.Common.Extraction;

namespace ArchUnitSharp.Files.Tests;

public class FilesTests
{
    [Fact]
    public void Select_without_selectors_returns_every_file()
    {
        var files = new Files(Graph(
            Self("src/App/Program.cs"),
            Self("src/Models/Car.cs")));

        Assert.Equal(new[] { "src/App/Program.cs", "src/Models/Car.cs" }, files.Select());
    }

    [Fact]
    public void WithName_selects_by_the_file_name_not_the_path()
    {
        var files = new Files(Graph(
            Self("src/Models/Car.cs"),
            Self("src/App/Car.cs"),
            Self("src/App/Program.cs"))).WithName("Car.cs");

        Assert.Equal(new[] { "src/App/Car.cs", "src/Models/Car.cs" }, files.Select());
    }

    [Fact]
    public void InFolder_selects_by_the_folder_not_the_file_name()
    {
        var files = new Files(Graph(
            Self("src/Models/Car.cs"),
            Self("src/Models/Truck.cs"),
            Self("src/App/Car.cs"))).InFolder("src/Models");

        Assert.Equal(new[] { "src/Models/Car.cs", "src/Models/Truck.cs" }, files.Select());
    }

    [Fact]
    public void Recursive_folder_rules_check_a_direct_child_even_when_a_nested_child_keeps_the_selection_non_empty()
    {
        var domain = new Files(Graph(
            Self("src/Domain/Order.cs"),
            Self("src/Domain/Nested/Value.cs"),
            Self("src/Infrastructure/Repository.cs"),
            Self("src/Infrastructure/Nested/Cache.cs"),
            new Edge("src/Domain/Order.cs", "src/Infrastructure/Repository.cs", external: false, ImportKind.Using)))
            .InFolder("src/Domain/**");

        IReadOnlyList<Violation> violations = domain.ShouldNot().DependOn().InFolder("src/Infrastructure/**").Check();

        Assert.Equal(
            new Violation[] { new DependencyViolation("src/Domain/Order.cs", "src/Infrastructure/Repository.cs") },
            violations);
        Assert.Equal(new[] { "src/Domain/Nested/Value.cs", "src/Domain/Order.cs" }, domain.Select());
    }

    [Fact]
    public void InPath_selects_by_the_whole_path()
    {
        var files = new Files(Graph(
            Self("src/Models/Car.cs"),
            Self("src/Models/Truck.cs"))).InPath("src/Models/Car.cs");

        Assert.Equal(new[] { "src/Models/Car.cs" }, files.Select());
    }

    [Fact]
    public void InFile_selects_by_the_class_style_name()
    {
        var files = new Files(Graph(
            Self("src/Models/Car.cs"),
            Self("src/App/Program.cs"))).InFile("src.Models.Car");

        Assert.Equal(new[] { "src/Models/Car.cs" }, files.Select());
    }

    [Fact]
    public void Selectors_combine_with_and()
    {
        var files = new Files(Graph(
            Self("src/Models/Car.cs"),
            Self("src/Models/Truck.cs"),
            Self("src/App/Car.cs")))
            .WithName("Car.cs")
            .InFolder("src/Models");

        Assert.Equal(new[] { "src/Models/Car.cs" }, files.Select());
    }

    [Fact]
    public void A_selector_leaves_the_parent_selection_unchanged()
    {
        var graph = Graph(
            Self("src/Models/Car.cs"),
            Self("src/App/Program.cs"));
        var parent = new Files(graph);

        var named = parent.WithName("Car.cs");

        Assert.Equal(new[] { "src/App/Program.cs", "src/Models/Car.cs" }, parent.Select());
        Assert.Equal(new[] { "src/Models/Car.cs" }, named.Select());
    }

    [Fact]
    public void Two_branches_off_one_parent_do_not_see_each_others_selectors()
    {
        var graph = Graph(
            Self("src/Models/Car.cs"),
            Self("src/Models/Truck.cs"),
            Self("src/App/Program.cs"));
        var parent = new Files(graph);

        var cars = parent.WithName("Car.cs");
        var inApp = parent.InFolder("src/App");

        Assert.Equal(new[] { "src/Models/Car.cs" }, cars.Select());
        Assert.Equal(new[] { "src/App/Program.cs" }, inApp.Select());
        Assert.Equal(
            new[] { "src/App/Program.cs", "src/Models/Car.cs", "src/Models/Truck.cs" },
            parent.Select());
    }

    [Fact]
    public void A_branch_extends_its_own_chain_without_touching_the_other_branch()
    {
        var graph = Graph(
            Self("src/Models/Car.cs"),
            Self("src/Models/Truck.cs"),
            Self("src/App/Car.cs"));
        var parent = new Files(graph);

        var carsInApp = parent.WithName("Car.cs").InFolder("src/App");
        var carsInModels = parent.WithName("Car.cs").InFolder("src/Models");

        Assert.Equal(new[] { "src/App/Car.cs" }, carsInApp.Select());
        Assert.Equal(new[] { "src/Models/Car.cs" }, carsInModels.Select());
    }

    [Fact]
    public void Select_returns_a_fresh_list_each_call()
    {
        var files = new Files(Graph(Self("a.cs"), Self("b.cs")));

        IReadOnlyList<string> first = files.Select();
        IReadOnlyList<string> second = files.Select();

        Assert.NotSame(first, second);
        Assert.Equal(second, first);
    }

    [Fact]
    public void The_constructor_rejects_a_null_graph()
    {
        Assert.Throws<ArgumentNullException>(() => new Files(null!));
    }

    [Fact]
    public void WithName_rejects_a_null_glob()
    {
        Assert.Throws<ArgumentNullException>(() => new Files(Graph(Self("a.cs"))).WithName(null!));
    }

    [Fact]
    public void WithName_rejects_an_empty_glob()
    {
        Assert.Throws<ArgumentException>(() => new Files(Graph(Self("a.cs"))).WithName(string.Empty));
    }

    [Fact]
    public void InFolder_rejects_a_null_glob()
    {
        Assert.Throws<ArgumentNullException>(() => new Files(Graph(Self("a.cs"))).InFolder(null!));
    }

    [Fact]
    public void InFolder_except_narrows_the_selection()
    {
        var files = new Files(Graph(
            Self("src/App/Program.cs"),
            Self("src/App/generated/Gen.cs"),
            Self("src/Models/Car.cs")))
            .InFolder("src/App**")
            .Except("src/App/generated");

        Assert.Equal(new[] { "src/App/Program.cs" }, files.Select());
    }

    [Fact]
    public void InFile_except_narrows_by_the_class_style_name()
    {
        var files = new Files(Graph(
            Self("src/Models/Car.cs"),
            Self("src/Models/Truck.cs"),
            Self("src/App/Car.cs")))
            .InFile("src.*")
            .Except("src.Models.Truck");

        Assert.Equal(new[] { "src/App/Car.cs", "src/Models/Car.cs" }, files.Select());
    }

    [Fact]
    public void Except_with_an_explicit_target_excludes_by_that_target()
    {
        var files = new Files(Graph(
            Self("src/Models/Car.cs"),
            Self("src/Models/Truck.cs"),
            Self("src/App/Car.cs")))
            .InFolder("src/Models")
            .Except(MatcherFactory.Filename("Truck.cs"));

        Assert.Equal(new[] { "src/Models/Car.cs" }, files.Select());
    }

    [Fact]
    public void Except_attaches_to_the_most_recent_selector()
    {
        var files = new Files(Graph(
            Self("src/Models/Car.cs"),
            Self("src/Models/Generated/Gen.cs"),
            Self("src/App/Car.cs")))
            .WithName("*.cs")
            .InFolder("src/Models**")
            .Except("src/Models/Generated");

        Assert.Equal(new[] { "src/Models/Car.cs" }, files.Select());
    }

    [Fact]
    public void Chained_except_calls_narrow_the_same_selector()
    {
        var files = new Files(Graph(
            Self("src/Models/Car.cs"),
            Self("src/Models/Truck.cs"),
            Self("src/Models/Generated/Gen.cs")))
            .InPath("src/Models/**")
            .Except("src/Models/Truck.cs")
            .Except("src/Models/Generated/**");

        Assert.Equal(new[] { "src/Models/Car.cs" }, files.Select());
    }

    [Fact]
    public void Except_leaves_the_parent_selection_unchanged()
    {
        var graph = Graph(
            Self("src/App/Program.cs"),
            Self("src/App/generated/Gen.cs"));
        var parent = new Files(graph).InFolder("src/App**");

        var narrowed = parent.Except("src/App/generated");

        Assert.Equal(new[] { "src/App/Program.cs", "src/App/generated/Gen.cs" }, parent.Select());
        Assert.Equal(new[] { "src/App/Program.cs" }, narrowed.Select());
    }

    [Fact]
    public void Two_branches_off_one_selector_do_not_see_each_others_exclusions()
    {
        var graph = Graph(
            Self("src/App/Program.cs"),
            Self("src/App/generated/Gen.cs"),
            Self("src/App/legacy/Old.cs"));
        var parent = new Files(graph).InFolder("src/App**");

        var noGenerated = parent.Except("src/App/generated");
        var noLegacy = parent.Except("src/App/legacy");

        Assert.Equal(new[] { "src/App/Program.cs", "src/App/legacy/Old.cs" }, noGenerated.Select());
        Assert.Equal(new[] { "src/App/Program.cs", "src/App/generated/Gen.cs" }, noLegacy.Select());
        Assert.Equal(
            new[] { "src/App/Program.cs", "src/App/generated/Gen.cs", "src/App/legacy/Old.cs" },
            parent.Select());
    }

    [Fact]
    public void Except_without_a_selector_raises_a_user_error()
    {
        var files = new Files(Graph(Self("a.cs")));

        Assert.Throws<UserError>(() => files.Except("Car.cs"));
        Assert.Throws<UserError>(() => files.Except(MatcherFactory.Filename("Car.cs")));
    }

    [Fact]
    public void Except_rejects_a_null_glob()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Files(Graph(Self("a.cs"))).WithName("a.cs").Except((string)null!));
    }

    [Fact]
    public void Except_rejects_an_empty_glob()
    {
        Assert.Throws<ArgumentException>(() =>
            new Files(Graph(Self("a.cs"))).WithName("a.cs").Except(string.Empty));
    }

    [Fact]
    public void Except_rejects_a_null_filter()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Files(Graph(Self("a.cs"))).WithName("a.cs").Except((Filter)null!));
    }

    [Fact]
    public void An_except_that_excludes_everything_is_guarded_as_an_empty_test()
    {
        var files = new Files(Graph(Self("src/App/Program.cs"), Self("src/Models/Car.cs")))
            .InFolder("src/App")
            .Except("**");

        IReadOnlyList<Violation> violations = files.Should().Exist().Check();

        Assert.Equal(
            new Violation[] { new EmptyTestViolation("project files in folder 'src/App' except in folder '**' should exist") },
            violations);
    }

    [Fact]
    public void InPath_rejects_a_null_glob()
    {
        Assert.Throws<ArgumentNullException>(() => new Files(Graph(Self("a.cs"))).InPath(null!));
    }

    [Fact]
    public void InFile_rejects_a_null_glob()
    {
        Assert.Throws<ArgumentNullException>(() => new Files(Graph(Self("a.cs"))).InFile(null!));
    }

    [Fact]
    public void FileDetailOf_reads_through_the_provider()
    {
        var files = new Files(Graph(Self("src/Models/Car.cs")), identifier => $"content of {identifier}");

        FileDetail detail = files.FileDetailOf("src/Models/Car.cs");

        Assert.Equal("content of src/Models/Car.cs", detail.SourceText);
        Assert.Equal("src/Models/Car.cs", detail.Path);
    }

    [Fact]
    public void A_branch_keeps_the_source_provider_of_its_parent()
    {
        var parent = new Files(Graph(Self("src/Models/Car.cs"), Self("src/App/Program.cs")), _ => "source");

        var named = parent.WithName("Car.cs");

        Assert.Equal("source", named.FileDetailOf("src/Models/Car.cs").SourceText);
        Assert.Equal("source", parent.FileDetailOf("src/Models/Car.cs").SourceText);
    }

    [Fact]
    public void Two_branches_off_one_parent_share_the_source_provider()
    {
        var parent = new Files(
            Graph(Self("src/Models/Car.cs"), Self("src/App/Program.cs")),
            _ => "source");

        var cars = parent.WithName("Car.cs");
        var inApp = parent.InFolder("src/App");

        Assert.Equal("source", cars.FileDetailOf("src/Models/Car.cs").SourceText);
        Assert.Equal("source", inApp.FileDetailOf("src/App/Program.cs").SourceText);
        Assert.Equal("source", parent.FileDetailOf("src/App/Program.cs").SourceText);
    }

    [Fact]
    public void FileDetailOf_without_a_provider_raises_a_user_error()
    {
        var files = new Files(Graph(Self("a.cs")));

        Assert.Throws<UserError>(() => files.FileDetailOf("a.cs"));
    }

    [Fact]
    public void The_internal_constructor_rejects_a_null_provider()
    {
        Assert.Throws<ArgumentNullException>(() => new Files(Graph(Self("a.cs")), null!));
    }

    private static Graph Graph(params Edge[] edges) => new(edges);

    private static Edge Self(string file) => new(file, file, external: false, ImportKind.None);
}
