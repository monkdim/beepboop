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
    /// <summary>
    /// The global the Balance quotes "one Reawaken per minute" at: 2.5s base under a 15%
    /// haste buff. Stated rather than left to the builder's default, because the offering
    /// rate is derived from it and these tests are about where the boundary sits.
    /// </summary>
    private const float ReferenceGcd = 2.12f;

    private static uint Suggest(
        byte offering,
        float ireCooldown,
        bool ready = false,
        bool ireLearned = true,
        float gcd = ReferenceGcd)
    {
        var builder = new SnapshotBuilder()
            .Job(41).Level(100).Gcd(0f, total: gcd).NoCombo().Enemies(1)
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

    /// <summary>
    /// Offering is earned per action, not per second, so a faster global earns it faster and
    /// the hold has to let go sooner. The rate used to be a constant quoted at a 2.12s
    /// global; at the 2.04s of a recorded pull the boundary moves from sixty seconds of
    /// Serpent's Ire cooldown to 57.7, so the hold sat on a full gauge 2.3s longer than it
    /// needed to. Fifty-nine seconds is inside that gap and outside both boundaries.
    /// </summary>
    [Fact]
    public void AFasterGlobalLetsGoSooner()
    {
        // Held at the reference global, because fifty needs the full minute to come back.
        Assert.NotEqual(A.Reawaken.Id, Suggest(offering: 50, ireCooldown: 59f));

        // Spent at 2.04s, where the same fifty is back with a second to spare.
        Assert.Equal(A.Reawaken.Id, Suggest(offering: 50, ireCooldown: 59f, gcd: 2.04f));
    }

    /// <summary>And the mirror: without Swiftscaled the global is slower, so it holds longer.</summary>
    [Fact]
    public void ASlowerGlobalHoldsLonger()
    {
        Assert.Equal(A.Reawaken.Id, Suggest(offering: 50, ireCooldown: 62f));
        Assert.NotEqual(A.Reawaken.Id, Suggest(offering: 50, ireCooldown: 62f, gcd: 2.5f));
    }

    /// <summary>
    /// A nonsense reading cannot produce a nonsense rate. Clamped, so the worst a bad global
    /// can do is behave like a plausible one.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(0.01f)]
    [InlineData(60f)]
    public void AnImplausibleGlobalIsClamped(float gcd)
    {
        // Ire two minutes out: affordable at any rate inside the clamp.
        Assert.Equal(A.Reawaken.Id, Suggest(offering: 50, ireCooldown: 120f, gcd: gcd));

        // Ire imminent: affordable at none of them.
        Assert.NotEqual(A.Reawaken.Id, Suggest(offering: 50, ireCooldown: 5f, gcd: gcd));
    }
}
