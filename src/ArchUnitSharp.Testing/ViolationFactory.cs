namespace ArchUnitSharp.Testing;

using System.Globalization;
using ArchUnitSharp.Common.Extraction;
using ArchUnitSharp.Files;
using ArchUnitSharp.Layers;
using ArchUnitSharp.Metrics;
using ArchUnitSharp.Slices;

/// <summary>
/// The single place a <see cref="Violation"/> becomes a human-readable message: given a violation's
/// structured data — the offending file, dependency, cycle, value or rule — it renders the report line
/// a consumer shows. All message formatting lives here and only here; no adapter formats its own prose.
/// </summary>
/// <remarks>
/// <para>
/// A violation carries data, not prose, so the exact wording of a report is this factory's decision:
/// the template for each concrete violation subtype is fixed here, and a consumer who wants different
/// wording supplies it over the data, not by editing the module that produced the violation. The rule
/// text a module hands the empty-test guard arrives as the violation's <em>data</em>; this factory
/// turns it into the report sentence. The concrete subtypes the library defines today are each
/// handled — <see cref="EmptyTestViolation"/>, <see cref="FileViolation"/>,
/// <see cref="AdhereToViolation"/>, <see cref="DependencyViolation"/>,
/// <see cref="CycleViolation"/>, <see cref="LayerViolation"/>,
/// <see cref="ForbiddenDependencyViolation"/>, <see cref="MissingDependencyViolation"/> and
/// <see cref="DiagramAdherenceViolation"/>, together with the count, custom, cohesion and distance
/// metric violations — and a violation subtype this factory does not know is a
/// defect, so it throws rather than fabricate a message.
/// </para>
/// <para>
/// This type is stateless and safe for concurrent use; the strings it returns are freshly built on
/// every call.
/// </para>
/// </remarks>
public static class ViolationFactory
{
    /// <summary>
    /// Renders <paramref name="violation"/> as the message a report prints.
    /// </summary>
    /// <param name="violation">The violation to render. Must not be <see langword="null"/>.</param>
    /// <returns>The violation's report message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="violation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="violation"/> is a subtype this factory does not know how to render.</exception>
    public static string Format(Violation violation)
    {
        ArgumentNullException.ThrowIfNull(violation);
        return violation switch
        {
            EmptyTestViolation v => $"The rule matched nothing: {v.RuleDescription}.",
            FileViolation v => $"File '{v.File}' violates the rule.",
            AdhereToViolation v => $"File '{v.File}' violates the rule: {v.Message}",
            DependencyViolation v => $"File '{v.Source}' must not depend on '{v.Target}'.",
            CycleViolation v => $"Cycle: {v.Path}",
            LayerViolation v =>
                $"Layer '{v.SourceLayer}' must not depend on layer '{v.TargetLayer}': '{v.Source}' depends on '{v.Target}'.",
            ForbiddenDependencyViolation v =>
                $"Slice '{v.Slice}' must not contain a dependency from '{v.Source}' to '{v.Target}'.",
            MissingDependencyViolation v =>
                $"Slice '{v.Slice}' must contain a dependency from a file matching '{v.From}' to a file matching '{v.To}'.",
            DiagramAdherenceViolation v =>
                $"Slice '{v.SourceSlice}' must not depend on '{v.TargetSlice}' (not in the diagram).",
            MetricViolation v => FormatMetric(
                v.File, v.Class, CountMetricName(v.MetricKind), v.Value, v.Comparison, v.Threshold, v.Message),
            CustomMetricViolation v => FormatMetric(
                v.File, v.Class, $"custom metric '{v.MetricName}' ({v.Description})",
                v.Value, v.Comparison, v.Threshold, v.Message),
            LcomMetricViolation v => FormatMetric(
                v.File, v.Class, LcomMetricName(v.MetricKind), v.Value, v.Comparison, v.Threshold, v.Message),
            DistanceMetricViolation v => FormatMetric(
                v.File, null, DistanceMetricName(v.MetricKind), v.Value, v.Comparison, v.Threshold, v.Message),
            DistanceZoneViolation v =>
                $"File '{v.File}': abstractness is {Number(v.Abstractness)} and instability is {Number(v.Instability)}; "
                + $"must not be in zone of {ZoneName(v.Zone)}.",
            _ => throw new ArgumentOutOfRangeException(
                nameof(violation),
                violation,
                "Violation is not a defined Violation subtype."),
        };
    }

    private static string FormatMetric(
        string file,
        string? className,
        string metric,
        IFormattable value,
        MetricComparison? comparison,
        IFormattable? threshold,
        string? message)
    {
        string subject = className is null ? $"File '{file}'" : $"Class '{className}' in file '{file}'";
        string requirement = comparison is { } kind && threshold is { } limit
            ? $"{ComparisonWords(kind)} {Number(limit)}"
            : $"satisfy '{message}'";
        return $"{subject}: {metric} is {Number(value)}; should {requirement}.";
    }

    private static string Number(IFormattable value) => value.ToString(null, CultureInfo.InvariantCulture);

    private static string ComparisonWords(MetricComparison comparison) => comparison switch
    {
        MetricComparison.Below => "be below",
        MetricComparison.Above => "be above",
        MetricComparison.Equal => "be",
        MetricComparison.BelowOrEqual => "be below or equal to",
        MetricComparison.AboveOrEqual => "be above or equal to",
        _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Unknown metric comparison."),
    };

    private static string CountMetricName(CountMetricKind kind) => kind switch
    {
        CountMetricKind.MethodCount => "method count",
        CountMetricKind.FieldCount => "field count",
        CountMetricKind.LinesOfCode => "lines of code",
        CountMetricKind.Statements => "statements",
        CountMetricKind.Imports => "imports",
        CountMetricKind.Classes => "classes",
        CountMetricKind.Interfaces => "interfaces",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown count metric."),
    };

    private static string LcomMetricName(LcomMetricKind kind) => kind switch
    {
        LcomMetricKind.Lcom96a => "lcom96a",
        LcomMetricKind.Lcom96b => "lcom96b",
        LcomMetricKind.Lcom1 => "lcom1",
        LcomMetricKind.Lcom2 => "lcom2",
        LcomMetricKind.Lcom3 => "lcom3",
        LcomMetricKind.Lcom4 => "lcom4",
        LcomMetricKind.Lcom5 => "lcom5",
        LcomMetricKind.LcomStar => "lcom*",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown cohesion metric."),
    };

    private static string DistanceMetricName(DistanceMetricKind kind) => kind switch
    {
        DistanceMetricKind.Abstractness => "abstractness",
        DistanceMetricKind.Instability => "instability",
        DistanceMetricKind.DistanceFromMainSequence => "distance from main sequence",
        DistanceMetricKind.CouplingFactor => "coupling factor",
        DistanceMetricKind.NormalisedDistance => "normalised distance",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown distance metric."),
    };

    private static string ZoneName(DistanceZone zone) => zone switch
    {
        DistanceZone.Pain => "pain",
        DistanceZone.Uselessness => "uselessness",
        _ => throw new ArgumentOutOfRangeException(nameof(zone), zone, "Unknown distance zone."),
    };
}
