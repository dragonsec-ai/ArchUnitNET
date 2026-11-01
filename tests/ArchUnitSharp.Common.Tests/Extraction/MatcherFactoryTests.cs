using ArchUnitSharp.Common.Extraction;

namespace ArchUnitSharp.Common.Tests.Extraction;

public class MatcherFactoryTests
{
    [Fact]
    public void Filename_binds_the_filename_target()
    {
        var filter = MatcherFactory.Filename("*.cs");

        Assert.Equal(MatchTarget.Filename, filter.Target);
        Assert.True(filter.Matches("src/Models/Car.cs"));
        Assert.True(filter.Matches("Car.cs"));
        Assert.False(filter.Matches("src/Models/Car.txt"));
    }

    [Fact]
    public void Folder_binds_the_directory_target()
    {
        var models = MatcherFactory.Folder("src/Models");
        var any = MatcherFactory.Folder("**");

        Assert.Equal(MatchTarget.PathWithoutFilename, models.Target);
        Assert.True(models.Matches("src/Models/Car.cs"));
        Assert.False(models.Matches("src/Other/Car.cs"));
        Assert.True(any.Matches("Car.cs"));
        Assert.True(any.Matches("src/Models/Car.cs"));
    }

    [Theory]
    [InlineData("src/Models/**")]
    [InlineData(@"src\Models\**")]
    public void Recursive_folder_includes_direct_and_nested_children_but_not_sibling_folders(string glob)
    {
        var filter = MatcherFactory.Folder(glob);

        Assert.True(filter.Matches("src/Models/Car.cs"));
        Assert.True(filter.Matches("src/Models/Nested/Car.cs"));
        Assert.True(filter.Matches(@"src\Models\Car.cs"));
        Assert.False(filter.Matches("src/ModelsExtra/Car.cs"));
        Assert.False(filter.Matches("src/Car.cs"));
    }

    [Fact]
    public void Path_binds_the_whole_identifier_target()
    {
        var filter = MatcherFactory.Path("**/*.cs");

        Assert.Equal(MatchTarget.Path, filter.Target);
        Assert.True(filter.Matches("Car.cs"));
        Assert.True(filter.Matches("src/Models/Car.cs"));
        Assert.False(filter.Matches("src/Models/Car.txt"));
    }

    [Fact]
    public void Classname_binds_the_class_name_target()
    {
        var controllers = MatcherFactory.Classname("**/*Controller");

        Assert.Equal(MatchTarget.Classname, controllers.Target);
        Assert.True(controllers.Matches("src/Controllers/HomeController.cs"));
        Assert.False(controllers.Matches("src/Models/Car.cs"));
    }

    [Fact]
    public void Exact_file_matches_the_whole_identifier_literally()
    {
        var filter = MatcherFactory.ExactFile("src/Models/Car.cs");

        Assert.True(filter.Matches("src/Models/Car.cs"));
        Assert.False(filter.Matches("src/Models/Other.cs"));
        Assert.False(filter.Matches("Car.cs"));
        Assert.False(filter.Matches("src/Models/Car.cs/Extra"));
    }

    [Fact]
    public void Exact_file_treats_glob_characters_as_literal()
    {
        var star = MatcherFactory.ExactFile("a*.cs");
        var question = MatcherFactory.ExactFile("a?b");
        var bracket = MatcherFactory.ExactFile("a[b]c");

        Assert.True(star.Matches("a*.cs"));
        Assert.False(star.Matches("aa.cs"));
        Assert.True(question.Matches("a?b"));
        Assert.False(question.Matches("aXb"));
        Assert.True(bracket.Matches("a[b]c"));
        Assert.False(bracket.Matches("abc"));
    }

    [Fact]
    public void Exact_file_normalises_backslash_separators()
    {
        var filter = MatcherFactory.ExactFile(@"src\Models\Car.cs");

        Assert.True(filter.Matches("src/Models/Car.cs"));
        Assert.True(filter.Matches(@"src\Models\Car.cs"));
        Assert.False(filter.Matches("src/Other/Car.cs"));
    }

    [Fact]
    public void A_factory_method_equals_the_equivalent_hand_built_filter()
    {
        Assert.Equal(new Filter(new Pattern("*.cs"), MatchTarget.Filename), MatcherFactory.Filename("*.cs"));
        Assert.Equal(new Filter(new Pattern("src/**"), MatchTarget.Path), MatcherFactory.Path("src/**"));
    }

    [Theory]
    [InlineData("Filename")]
    [InlineData("Folder")]
    [InlineData("Path")]
    [InlineData("Classname")]
    public void Null_glob_is_rejected(string method)
    {
        switch (method)
        {
            case "Filename": Assert.Throws<ArgumentNullException>(() => MatcherFactory.Filename(null!)); break;
            case "Folder": Assert.Throws<ArgumentNullException>(() => MatcherFactory.Folder(null!)); break;
            case "Path": Assert.Throws<ArgumentNullException>(() => MatcherFactory.Path(null!)); break;
            case "Classname": Assert.Throws<ArgumentNullException>(() => MatcherFactory.Classname(null!)); break;
        }
    }

    [Fact]
    public void Empty_glob_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => MatcherFactory.Filename(string.Empty));
    }

    [Fact]
    public void Null_exact_file_identifier_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => MatcherFactory.ExactFile(null!));
    }

    [Fact]
    public void Empty_exact_file_identifier_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => MatcherFactory.ExactFile(string.Empty));
    }
}
