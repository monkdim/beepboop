using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Monk;
using Xunit;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// The gauge line printed beside every recorded Monk cast.
/// <para>
/// Monk had none, and it cost two recorded pulls. Both showed Perfect Balance banking three
/// Opo-opo where the list should have been asking for one of each, and with nothing in the
/// log but form buffs there was no way to tell a Nadi that was read wrong from beast chakra
/// that were - so the first fix went to the wrong one. Everything the list reads is in here
/// now, which is what made the Red Mage combo bug findable in a single pull.
/// </para>
/// </summary>
public sealed class MonkGaugeReportTests
{
    private static string Describe(
        byte nadi,
        bool opo = false,
        bool raptor = false,
        bool coeurl = false,
        byte beastCount = 0,
        byte chakra = 0,
        byte opoFury = 0)
    {
        // Set through the snapshot: MonkGauge is a struct, so anything handed one by value
        // would mutate a copy and describe the wrong thing.
        var snapshot = new SnapshotBuilder()
            .Job(20).Level(100)
            .Gauge(s =>
            {
                s.Gauges.Monk.NadiFlags = nadi;
                s.Gauges.Monk.HasOpoChakra = opo;
                s.Gauges.Monk.HasRaptorChakra = raptor;
                s.Gauges.Monk.HasCoeurlChakra = coeurl;
                s.Gauges.Monk.BeastChakraCount = beastCount;
                s.Gauges.Monk.Chakra = chakra;
                s.Gauges.Monk.OpoOpoFury = opoFury;
            })
            .Build();

        return JobRotationBase.Create<MonkRotation>().DescribeGauge(snapshot) ?? string.Empty;
    }

    /// <summary>Which Nadi are lit is the whole Perfect Balance decision, so it is named.</summary>
    [Fact]
    public void TheNadiAreNamedRatherThanPrintedAsAFlagsByte()
    {
        Assert.Contains("nadi none", Describe(0));
        Assert.Contains("nadi lunar", Describe(1));
        Assert.Contains("nadi solar", Describe(2));
        Assert.Contains("nadi lunar+solar", Describe(3));
    }

    /// <summary>
    /// Which chakra are open, not how many. It is the missing one that decides what the
    /// window asks for next, and a count cannot say which that is.
    /// </summary>
    [Fact]
    public void TheOpenBeastChakraAreNamedIndividually()
    {
        Assert.Contains("beast none", Describe(0));
        Assert.Contains("beast opo", Describe(0, opo: true, beastCount: 1));
        Assert.Contains(
            "beast opo+raptor",
            Describe(0, opo: true, raptor: true, beastCount: 2));
    }

    /// <summary>The fury counters decide spend-or-build in every form.</summary>
    [Fact]
    public void TheFuryCountersAndChakraAreCarried()
    {
        var line = Describe(0, chakra: 4, opoFury: 1);

        Assert.Contains("chakra 4", line);
        Assert.Contains("fury opo 1 raptor 0 coeurl 0", line);
    }
}
