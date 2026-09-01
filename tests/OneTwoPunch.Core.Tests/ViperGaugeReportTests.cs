using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Viper;
using OneTwoPunch.Core.Model;
using Xunit;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// The gauge line printed beside every recorded Viper cast.
/// <para>
/// Viper had none, and it is the job that needs one most: five separate things drive its
/// rules and not one of them reached the log. A recorded four and a half minute pull is
/// structurally perfect - seven Reawaken chains, every follow-up matched, Swiftscaled at
/// ninety-nine percent - and it still cannot answer the only question asked of it, which
/// was whether the resources were being held or spent, because Serpent Offering is not in it.
/// </para>
/// </summary>
public sealed class ViperGaugeReportTests
{
    private static string Describe(
        byte coils = 0,
        byte offering = 0,
        byte tribute = 0,
        DreadCombo dread = DreadCombo.None,
        SerpentCombo tail = SerpentCombo.None)
    {
        // Set through the snapshot: ViperGauge is a struct, so anything handed one by value
        // would mutate a copy and describe the wrong thing.
        var snapshot = new SnapshotBuilder()
            .Job(41).Level(100)
            .Gauge(s =>
            {
                s.Gauges.Viper.RattlingCoils = coils;
                s.Gauges.Viper.SerpentOffering = offering;
                s.Gauges.Viper.AnguineTribute = tribute;
                s.Gauges.Viper.DreadCombo = dread;
                s.Gauges.Viper.SerpentCombo = tail;
            })
            .Build();

        return JobRotationBase.Create<ViperRotation>().DescribeGauge(snapshot) ?? string.Empty;
    }

    /// <summary>
    /// The two the question was actually about: what is banked, and how close to spending.
    /// Both are always printed, including at zero, because "none left" is the answer as
    /// often as "capped" is.
    /// </summary>
    [Fact]
    public void TheCoilsAndTheOfferingAreAlwaysCarried()
    {
        Assert.Equal("coils 0 | offering 0", Describe());
        Assert.Equal("coils 3 | offering 90", Describe(coils: 3, offering: 90));
    }

    /// <summary>Anguine Tribute only while a Reawaken chain is actually running.</summary>
    [Fact]
    public void TheReawakenCounterAppearsOnlyWhileItIsRunning()
    {
        Assert.DoesNotContain("tribute", Describe(offering: 50));
        Assert.Contains("tribute 4", Describe(tribute: 4));
    }

    /// <summary>
    /// Both chain trackers by name. Neither is the ordinary combo state - Vicewinder leaves
    /// Steel Fangs' combo running underneath it - and both have already been the cause of a
    /// rule that fired for nobody.
    /// </summary>
    [Fact]
    public void BothChainTrackersAreNamedRatherThanPrintedAsNumbers()
    {
        Assert.Contains("dread Vicewinder", Describe(dread: DreadCombo.Vicewinder));
        Assert.Contains("tail DeathRattle", Describe(tail: SerpentCombo.DeathRattle));
        Assert.DoesNotContain("dread", Describe());
        Assert.DoesNotContain("tail", Describe());
    }
}
