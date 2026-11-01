using ArchUnitSharp.Common.Extraction;
using ArchUnitSharp.Extraction;
using ArchUnitSharp.Metrics;
using ArchUnitSharp.Testing;
using ArchUnitSharp.Testing.Xunit;
using Xunit.Sdk;

namespace ArchUnitSharp.Tests;

public class MetricAssertionIntegrationTests
{
    [Theory]
    [InlineData("lines", typeof(MetricViolation), "lines of code is 8; should be below 8.")]
    [InlineData("count predicate", typeof(MetricViolation), "method count is 2; should satisfy 'declare one method'.")]
    [InlineData("custom", typeof(CustomMetricViolation), "custom metric 'methods' (declared methods) is 2; should be below 2.")]
    [InlineData("custom predicate", typeof(CustomMetricViolation), "custom metric 'methods' (declared methods) is 2; should satisfy 'declare one method'.")]
    [InlineData("cohesion", typeof(LcomMetricViolation), "lcom4 is 2; should be 1.")]
    [InlineData("cohesion predicate", typeof(LcomMetricViolation), "lcom4 is 2; should satisfy 'form one component'.")]
    [InlineData("distance", typeof(DistanceMetricViolation), "abstractness is 0; should be above 0.")]
    [InlineData("distance predicate", typeof(DistanceMetricViolation), "abstractness is 0; should satisfy 'contain an abstraction'.")]
    [InlineData("zone", typeof(DistanceZoneViolation), "abstractness is 0 and instability is 0; must not be in zone of pain.")]
    public void Source_based_metrics_report_assertion_failures_and_support_both_asserted_outcomes(
        string family, Type violationType, string diagnostic)
    {
        using var project = new TempProject();
        project.WriteFile("App.sln", "");
        project.WriteFile("Sample.cs", """
            namespace Demo;
            public class Sample
            {
                private int _left;
                private int _right;
                public void Left() { _left = 1; }
                public void Right() { _right = 2; }
            }
            """);
        var location = ProjectLocator.Locate(project.Root);
        var metrics = Project.Metrics(location).WithName("Sample.cs");
        ICheckable failing = Rule(metrics, family, passing: false);

        Violation violation = Assert.Single(failing.Check());
        Assert.IsType(violationType, violation);
        RuleAssert.Fails(failing);
        failing.AssertFails();
        var generic = Assert.Throws<AssertionFailedException>(() => RuleAssert.Passes(failing));
        var native = Assert.Throws<TrueException>(() => failing.AssertPasses());

        Assert.Contains("Sample.cs", generic.Message);
        Assert.Contains(diagnostic, generic.Message);
        Assert.Equal(generic.Message, native.Message);
        if (family is "count predicate" or "custom" or "custom predicate" or "cohesion" or "cohesion predicate")
        {
            Assert.Contains("Demo.Sample", generic.Message);
        }

        ICheckable passing = Rule(metrics, family, passing: true);
        Assert.Empty(passing.Check());
        RuleAssert.Passes(passing);
        passing.AssertPasses();
        Assert.Throws<AssertionFailedException>(() => RuleAssert.Fails(passing));
        Assert.Throws<FalseException>(() => passing.AssertFails());
    }

    private static ICheckable Rule(ArchUnitSharp.Metrics.Metrics metrics, string family, bool passing) => family switch
    {
        "lines" => metrics.Count().LinesOfCode().ShouldBeBelow(passing ? 9 : 8),
        "count predicate" => metrics.Count().MethodCount()
            .ShouldSatisfy(value => value == (passing ? 2 : 1), "declare one method"),
        "custom" => metrics.CustomMetric("methods", "declared methods", info => info.Methods.Count)
            .ShouldBeBelow(passing ? 3 : 2),
        "custom predicate" => metrics.CustomMetric("methods", "declared methods", info => info.Methods.Count)
            .ShouldSatisfy((value, _) => value == (passing ? 2 : 1), "declare one method"),
        "cohesion" => metrics.Lcom().Lcom4().ShouldBe(passing ? 2 : 1),
        "cohesion predicate" => metrics.Lcom().Lcom4()
            .ShouldSatisfy(value => value == (passing ? 2 : 1), "form one component"),
        "distance" => passing
            ? metrics.Distance().Abstractness().ShouldBe(0)
            : metrics.Distance().Abstractness().ShouldBeAbove(0),
        "distance predicate" => metrics.Distance().Abstractness()
            .ShouldSatisfy(value => passing ? value == 0 : value > 0, "contain an abstraction"),
        "zone" => passing ? metrics.Distance().NotInZoneOfUselessness() : metrics.Distance().NotInZoneOfPain(),
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, "Unknown metric fixture."),
    };
}
