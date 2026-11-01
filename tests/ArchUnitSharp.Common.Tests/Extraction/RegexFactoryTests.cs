using System.Text.RegularExpressions;
using ArchUnitSharp.Common.Extraction;

namespace ArchUnitSharp.Common.Tests.Extraction;

public class RegexFactoryTests
{
    [Fact]
    public void Star_matches_any_run_of_characters_within_a_segment()
    {
        var regex = RegexFactory.CompileGlob("*.cs");

        Assert.Matches(regex, "Car.cs");
        Assert.Matches(regex, "a.cs");
        Assert.DoesNotMatch(regex, "Car.txt");
        Assert.DoesNotMatch(regex, "Models/Car.cs");
    }

    [Fact]
    public void Double_star_matches_across_segments()
    {
        var regex = RegexFactory.CompileGlob("**/*.cs");

        Assert.Matches(regex, "Car.cs");
        Assert.Matches(regex, "Models/Car.cs");
        Assert.Matches(regex, "src/Models/Car.cs");
        Assert.DoesNotMatch(regex, "src/Models/Car.txt");
    }

    [Fact]
    public void Double_star_after_a_leading_path_matches_zero_or_more_segments()
    {
        var regex = RegexFactory.CompileGlob("src/**/Car.cs");

        Assert.Matches(regex, "src/Car.cs");
        Assert.Matches(regex, "src/Models/Car.cs");
        Assert.Matches(regex, "src/Models/Sub/Car.cs");
        Assert.DoesNotMatch(regex, "Other/Car.cs");
        Assert.DoesNotMatch(regex, "src/ACar.cs");
    }

    [Theory]
    [InlineData("src/Models/**")]
    [InlineData(@"src\Models\**")]
    public void Trailing_double_star_includes_the_base_path_and_descendants(string glob)
    {
        var regex = RegexFactory.CompileGlob(glob);

        Assert.Matches(regex, "src/Models");
        Assert.Matches(regex, "src/Models/");
        Assert.Matches(regex, "src/Models/Car.cs");
        Assert.Matches(regex, "src/Models/Nested/Car.cs");
        Assert.DoesNotMatch(regex, "src");
        Assert.DoesNotMatch(regex, "src/ModelsExtra");
        Assert.DoesNotMatch(regex, "src/ModelsExtra/Car.cs");
        Assert.DoesNotMatch(regex, "src/models");
    }

    [Fact]
    public void Parenthesized_double_star_captures_the_segments_between_the_literals()
    {
        var regex = RegexFactory.CompileGlob("src/features/(**)/*.cs");

        Assert.Matches(regex, "src/features/billing/order.cs");
        Assert.Matches(regex, "src/features/a/b/order.cs");
        Assert.DoesNotMatch(regex, "src/features/billing/order.txt");

        Match billing = regex.Match("src/features/billing/order.cs");
        Assert.True(billing.Groups[1].Success);
        Assert.Equal("billing/", billing.Groups[1].Value);

        Match nested = regex.Match("src/features/a/b/order.cs");
        Assert.Equal("a/b/", nested.Groups[1].Value);
    }

    [Fact]
    public void Parenthesized_double_star_at_the_end_captures_the_remainder()
    {
        var regex = RegexFactory.CompileGlob("src/features/(**)");

        Match match = regex.Match("src/features/billing/order.cs");

        Assert.True(match.Success);
        Assert.Equal("billing/order.cs", match.Groups[1].Value);
    }

    [Fact]
    public void Parenthesized_double_star_before_a_separator_matches_zero_segments()
    {
        var regex = RegexFactory.CompileGlob("src/features/(**)/order.cs");

        Assert.Matches(regex, "src/features/order.cs");
        Assert.Matches(regex, "src/features/sub/order.cs");

        Match flat = regex.Match("src/features/order.cs");
        Assert.Equal(string.Empty, flat.Groups[1].Value);

        Match nested = regex.Match("src/features/sub/order.cs");
        Assert.Equal("sub/", nested.Groups[1].Value);
    }

    [Fact]
    public void A_parenthesis_that_is_not_a_capture_is_a_literal_character()
    {
        var regex = RegexFactory.CompileGlob("(x)");

        Assert.Matches(regex, "(x)");
        Assert.DoesNotMatch(regex, "x");
    }

    [Fact]
    public void Star_does_not_cross_a_segment_boundary()
    {
        var regex = RegexFactory.CompileGlob("src/*/Car.cs");

        Assert.Matches(regex, "src/Models/Car.cs");
        Assert.DoesNotMatch(regex, "src/Models/Sub/Car.cs");
    }

    [Fact]
    public void Question_mark_matches_exactly_one_character()
    {
        var regex = RegexFactory.CompileGlob("C?r.cs");

        Assert.Matches(regex, "Car.cs");
        Assert.DoesNotMatch(regex, "Cr.cs");
        Assert.DoesNotMatch(regex, "Caar.cs");
        Assert.DoesNotMatch(regex, "C/r.cs");
    }

    [Fact]
    public void Character_class_matches_any_listed_character()
    {
        var regex = RegexFactory.CompileGlob("[Cc]ar.cs");

        Assert.Matches(regex, "Car.cs");
        Assert.Matches(regex, "car.cs");
        Assert.DoesNotMatch(regex, "Bar.cs");
    }

    [Fact]
    public void Negated_character_class_matches_any_character_but_the_listed_ones()
    {
        var regex = RegexFactory.CompileGlob("[!Cc]ar.cs");

        Assert.Matches(regex, "Bar.cs");
        Assert.DoesNotMatch(regex, "Car.cs");
        Assert.DoesNotMatch(regex, "car.cs");
    }

    [Fact]
    public void Character_class_supports_ranges()
    {
        var regex = RegexFactory.CompileGlob("file[0-9].txt");

        Assert.Matches(regex, "file1.txt");
        Assert.Matches(regex, "file9.txt");
        Assert.DoesNotMatch(regex, "filea.txt");
    }

    [Fact]
    public void Matching_is_case_sensitive_by_default()
    {
        var regex = RegexFactory.CompileGlob("*.cs");

        Assert.DoesNotMatch(regex, "CAR.CS");
        Assert.Matches(regex, "car.cs");
    }

    [Fact]
    public void Unterminated_character_class_is_treated_as_a_literal()
    {
        var regex = RegexFactory.CompileGlob("a[bc");

        Assert.Matches(regex, "a[bc");
        Assert.DoesNotMatch(regex, "ab");
    }

    [Fact]
    public void Backslash_separators_are_normalised_to_forward_slashes()
    {
        var regex = RegexFactory.CompileGlob(@"src\**\Car.cs");

        Assert.Matches(regex, "src/Models/Car.cs");
        Assert.Matches(regex, "src/Car.cs");
    }

    [Fact]
    public void The_dot_is_a_literal_character()
    {
        var regex = RegexFactory.CompileGlob("*.cs");

        Assert.DoesNotMatch(regex, "aXcs");
    }

    [Fact]
    public void The_whole_candidate_is_matched_not_a_substring()
    {
        var regex = RegexFactory.CompileGlob("Car");

        Assert.Matches(regex, "Car");
        Assert.DoesNotMatch(regex, "Car.cs");
    }

    [Fact]
    public void Null_glob_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => RegexFactory.CompileGlob(null!));
    }
}
