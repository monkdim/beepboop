using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Viper;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Viper.ViperActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// When the fifty-Offering Reawaken is spent.
/// <para>
/// It used to go the moment the gauge reached fifty, which the Intermediate guide names as
/// the basic priority system and says "comes at a significant loss of potency inside party
/// buffs". A recorded 31 minute raid shows the cost: fifteen burst windows, seven of them a
/// double Reawaken, eight a single with the second landing somewhere outside the buffs.
/// </para>
/// <para>
/// Now a paid Reawaken goes only when what is left will have grown back to fifty by the time
/// Serpent's Ire returns. Three things override that: the free one from Ire, a gauge about
/// to cap, and a level with no Ire to save for.
/// </para>
/// </summary>
public sealed class ViperReawakenHoldTests
{
    private static uint Suggest(
        byte offering, float ireCooldown, bool ready = false, bool ireLearned = true)
    {
        var builder = new SnapshotBuilder()
            .Job(41).Level(100).Gcd(0f).NoCombo().Enemies(1)
            .Gauge(s => s.Gauges.Viper.SerpentOffering = offering);

        if (ready)
            builder.Buff(A.ReawakenReady.Id, 30f);

        // Vicewinder sits below Reawaken but above the starters, and it is unconditional;
        // off, so what comes back when Reawaken is held is a plain starter.
        var actions = new FakeActionState()
            .OnCooldown(A.Vicewinder.Id, 30f)
            .OnCooldown(A.SerpentsIre.Id, ireCooldown);

        if (!ireLearned)
            actions.Locked(A.SerpentsIre.Id);

        return new RotationSession(JobRotationBase.Create<ViperRotation>(),
            new RotationSettings { UseOpener = false, SuggestionHoldSeconds = 0f })
            .Resolve(RotationMode.SingleTarget, builder.Build(), actions)
            .Action.Id;
    }

    /// <summary>The defect: fifty in hand, Ire half a minute out, and it was spent anyway.</summary>
    [Fact]
    public void FiftyIsHeldWhenItWouldNotGrowBackBeforeTheBurst()
    {
        Assert.NotEqual(A.Reawaken.Id, Suggest(offering: 50, ireCooldown: 30f));
    }

    /// <summary>With Ire a minute or more away, the gauge grows back in time - spend.</summary>
    [Fact]
    public void FiftyIsSpentWhenItWillGrowBackBeforeTheBurst()
    {
        Assert.Equal(A.Reawaken.Id, Suggest(offering: 50, ireCooldown: 90f));
    }

    /// <summary>
    /// The boundary the guide describes: one Reawaken between windows. At fifty that is
    /// exactly Ire sixty seconds out.
    /// </summary>
    [Fact]
    public void TheBoundaryIsAMinuteOfRegrowth()
    {
        Assert.Equal(A.Reawaken.Id, Suggest(offering: 50, ireCooldown: 61f));
        Assert.NotEqual(A.Reawaken.Id, Suggest(offering: 50, ireCooldown: 55f));
    }

    /// <summary>More in hand needs less time. Eighty with Ire twenty-five seconds out is fine.</summary>
    [Fact]
    public void AFullerGaugeNeedsLessTime()
    {
        Assert.Equal(A.Reawaken.Id, Suggest(offering: 80, ireCooldown: 25f));
        Assert.NotEqual(A.Reawaken.Id, Suggest(offering: 60, ireCooldown: 25f));
    }

    /// <summary>A gauge that would cap on the next finisher is spent, whatever Ire is doing.</summary>
    [Fact]
    public void AGaugeAboutToCapIsSpentRegardless()
    {
        Assert.Equal(A.Reawaken.Id, Suggest(offering: 95, ireCooldown: 5f));
    }

    /// <summary>The free one from Serpent's Ire is always taken.</summary>
    [Fact]
    public void ReadyToReawakenIsAlwaysSpent()
    {
        Assert.Equal(A.Reawaken.Id, Suggest(offering: 0, ireCooldown: 115f, ready: true));
    }

    /// <summary>
    /// With no Serpent's Ire there is no window to save for, and a rung that cannot be
    /// climbed must not hang the phase. Reawaken arrives at 90 and Ire at 86, so no level
    /// reaches this; an Ire the player has not learned does.
    /// </summary>
    [Fact]
    public void WithNoSerpentsIreFiftyIsSpentOnSight()
    {
        Assert.Equal(A.Reawaken.Id, Suggest(offering: 50, ireCooldown: 30f, ireLearned: false));
    }
}
