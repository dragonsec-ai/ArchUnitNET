using System.Globalization;
using ArchUnitSharp.Common.Extraction;
using ArchUnitSharp.Files;
using ArchUnitSharp.Metrics;

namespace ArchUnitSharp.Testing.Tests;

public class MetricViolationFormattingTests
{
    [Theory]
    [InlineData(MetricComparison.Below, "be below")]
    [InlineData(MetricComparison.Above, "be above")]
    [InlineData(MetricComparison.Equal, "be")]
    [InlineData(MetricComparison.BelowOrEqual, "be below or equal to")]
    [InlineData(MetricComparison.AboveOrEqual, "be above or equal to")]
    public void A_file_metric_preserves_the_measured_value_and_comparison(MetricComparison comparison, string words)
    {
        var violation = new MetricViolation("src/App.cs", null, CountMetricKind.LinesOfCode, 42, comparison, 40);

        Assert.Equal(
            $"File 'src/App.cs': lines of code is 42; should {words} 40.",
            ViolationFactory.Format(violation));
    }

    [Fact]
    public void A_class_metric_names_the_class_and_preserves_the_predicate_message()
    {
        var violation = new MetricViolation(
            "src/App.cs", "App.Service", CountMetricKind.MethodCount, 3, "have an even number of methods");

        Assert.Equal(
            "Class 'App.Service' in file 'src/App.cs': method count is 3; should satisfy 'have an even number of methods'.",
            ViolationFactory.Format(violation));
    }

    [Fact]
    public void A_custom_metric_preserves_its_name_description_and_threshold()
    {
        var violation = new CustomMetricViolation(
            "src/App.cs", "App.Service", "member count", "methods plus fields", 6, MetricComparison.Below, 5);

        Assert.Equal(
            "Class 'App.Service' in file 'src/App.cs': custom metric 'member count' (methods plus fields) is 6; should be below 5.",
            ViolationFactory.Format(violation));
    }

    [Fact]
    public void A_custom_metric_preserves_its_predicate_message()
    {
        var violation = new CustomMetricViolation(
            "src/App.cs", "App.Service", "member count", "methods plus fields", 3, "keep members paired");

        Assert.Equal(
            "Class 'App.Service' in file 'src/App.cs': custom metric 'member count' (methods plus fields) is 3; should satisfy 'keep members paired'.",
            ViolationFactory.Format(violation));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    public void A_cohesion_metric_formats_fractional_values_independently_of_culture(string culture)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var violation = new LcomMetricViolation(
                "src/App.cs", "App.Service", LcomMetricKind.LcomStar, 0.75, MetricComparison.BelowOrEqual, 0.5);

            Assert.Equal(
                "Class 'App.Service' in file 'src/App.cs': lcom* is 0.75; should be below or equal to 0.5.",
                ViolationFactory.Format(violation));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void A_cohesion_metric_preserves_its_predicate_message()
    {
        var violation = new LcomMetricViolation(
            "src/App.cs", "App.Service", LcomMetricKind.Lcom4, 2, "form one component");

        Assert.Equal(
            "Class 'App.Service' in file 'src/App.cs': lcom4 is 2; should satisfy 'form one component'.",
            ViolationFactory.Format(violation));
    }

    [Fact]
    public void A_distance_metric_preserves_its_file_value_and_threshold()
    {
        var violation = new DistanceMetricViolation(
            "src/App.cs", DistanceMetricKind.DistanceFromMainSequence, 0.75, MetricComparison.Below, 0.5);

        Assert.Equal(
            "File 'src/App.cs': distance from main sequence is 0.75; should be below 0.5.",
            ViolationFactory.Format(violation));
    }

    [Fact]
    public void A_distance_metric_preserves_its_predicate_message()
    {
        var violation = new DistanceMetricViolation(
            "src/App.cs", DistanceMetricKind.Instability, 0.75, "remain stable");

        Assert.Equal(
            "File 'src/App.cs': instability is 0.75; should satisfy 'remain stable'.",
            ViolationFactory.Format(violation));
    }

    [Theory]
    [InlineData(DistanceZone.Pain, 0.1, 0.2, "pain")]
    [InlineData(DistanceZone.Uselessness, 0.8, 0.9, "uselessness")]
    public void A_zone_violation_names_the_zone_and_both_coordinates(
        DistanceZone zone, double abstractness, double instability, string name)
    {
        var violation = new DistanceZoneViolation("src/App.cs", zone, abstractness, instability);

        string message = ViolationFactory.Format(violation);

        Assert.Equal(
            FormattableString.Invariant($"File 'src/App.cs': abstractness is {abstractness} and instability is {instability}; must not be in zone of {name}."),
            message);
    }

    [Fact]
    public void A_metric_result_keeps_all_diagnostics_in_order()
    {
        Violation[] violations =
        {
            new MetricViolation("src/App.cs", null, CountMetricKind.LinesOfCode, 42, MetricComparison.Below, 40),
            new DependencyViolation("src/App.cs", "src/Legacy.cs"),
        };

        CheckResult result = ResultFactory.Create(violations);

        Assert.False(result.Passed);
        Assert.Equal(
            "File 'src/App.cs': lines of code is 42; should be below 40.\n"
            + "File 'src/App.cs' must not depend on 'src/Legacy.cs'.",
            result.Message);
    }
}
