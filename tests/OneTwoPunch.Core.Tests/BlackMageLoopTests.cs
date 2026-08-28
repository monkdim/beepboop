using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.BlackMage.BlackMageActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// The steady-state loop, read off The Balance's Single Target Rotation chart for Black Mage
/// level 100, Dawntrail patch 7.2:
/// <code>
///   [Transpose, Swiftcast]
///   1 Blizzard III   2 Blizzard IV   3 Paradox   [Transpose]
///   4 Fire III (Firestarter)   5-7 Fire IV   8 Paradox   9-11 Fire IV
///   12 Flare Star   13 Despair   [Transpose]
/// </code>
/// <para>
/// The priority list was putting both Paradoxes first in their phase rather than where the
/// chart draws them, which costs the free climb on the way into fire.
/// </para>
/// </summary>
public sealed class BlackMageLoopTests
{
    private static SnapshotBuilder Casting() =>
        new SnapshotBuilder().Job(25).Gcd(0f).NoCombo().Enemies(1)
            .Debuff(A.HighThunderBuff.Id, 25f);

    private static uint Suggest(SnapshotBuilder builder, FakeActionState? actions = null)
    {
        var state = actions ?? new FakeActionState();
        state.OnCooldown(A.LeyLines.Id, 60f);
        state.OnCooldown(A.Amplifier.Id, 60f);
        state.OnCooldown(A.Manafont.Id, 60f);

        return new RotationSession(JobRegistry.Create(25)!, new RotationSettings
        {
            UseOpener = false,
            SuggestionHoldSeconds = 0f,
        }).Resolve(RotationMode.SingleTarget, builder.Build(), state).Action.Id;
    }

    // ---- The ice phase: Blizzard III, Blizzard IV, Paradox ----------------

    /// <summary>Global 1. A held Paradox is not a reason to skip the climb.</summary>
    [Fact]
    public void IceOpensOnBlizzardThreeEvenHoldingAParadox()
    {
        var suggestion = Suggest(Casting().Gauge(s =>
        {
            s.Gauges.BlackMage.UmbralIce = 1;
            s.Gauges.BlackMage.ParadoxActive = true;
            s.Mp = 400;
        }));

        Assert.Equal(A.Blizzard3.Id, suggestion);
    }

    /// <summary>Global 2. Nor to skip the hearts.</summary>
    [Fact]
    public void TheHeartsComeBeforeTheParadox()
    {
        var suggestion = Suggest(Casting().Gauge(s =>
        {
            s.Gauges.BlackMage.UmbralIce = 3;
            s.Gauges.BlackMage.UmbralHearts = 0;
            s.Gauges.BlackMage.ParadoxActive = true;
            s.Mp = 3300;
        }));

        Assert.Equal(A.Blizzard4.Id, suggestion);
    }

    /// <summary>Global 3, and the bridge out - it is what leaves the Firestarter.</summary>
    [Fact]
    public void TheParadoxIsTheLastGlobalOfTheIcePhase()
    {
        var suggestion = Suggest(Casting().Gauge(s =>
        {
            s.Gauges.BlackMage.UmbralIce = 3;
            s.Gauges.BlackMage.UmbralHearts = 3;
            s.Gauges.BlackMage.ParadoxActive = true;
            s.Mp = 8000;
        }));

        Assert.Equal(A.Paradox.Id, suggestion);
    }

    // ---- The fire phase: climb, three Fire IVs, Paradox, three more -------

    /// <summary>Global 4. Astral Fire one is the climb's global, not the Paradox's.</summary>
    [Fact]
    public void FireOpensOnTheClimb()
    {
        var suggestion = Suggest(Casting()
            .Buff(A.Firestarter.Id, 20f)
            .Gauge(s =>
            {
                s.Gauges.BlackMage.AstralFire = 1;
                s.Gauges.BlackMage.ParadoxActive = true;
                s.Mp = 10000;
            }));

        Assert.Equal(A.Fire3.Id, suggestion);
    }

    /// <summary>Globals 5 to 7. The Paradox waits.</summary>
    [Fact]
    public void TheFirstThreeFireFoursComeBeforeTheParadox()
    {
        var suggestion = Suggest(Casting().Gauge(s =>
        {
            s.Gauges.BlackMage.AstralFire = 3;
            s.Gauges.BlackMage.AstralSoulStacks = 1;
            s.Gauges.BlackMage.ParadoxActive = true;
            s.Mp = 10000;
        }));

        Assert.Equal(A.Fire4.Id, suggestion);
    }

    /// <summary>Global 8. Three Astral Soul is three Fire IVs, which is where it is drawn.</summary>
    [Fact]
    public void TheParadoxIsTheEighthGlobalOfTheFirePhase()
    {
        var suggestion = Suggest(Casting().Gauge(s =>
        {
            s.Gauges.BlackMage.AstralFire = 3;
            s.Gauges.BlackMage.AstralSoulStacks = 3;
            s.Gauges.BlackMage.ParadoxActive = true;
            s.Mp = 10000;
        }));

        Assert.Equal(A.Paradox.Id, suggestion);
    }

    /// <summary>
    /// But a marker that cannot be spent is one that gets overwritten, so a bar with nothing
    /// left in it spends the Paradox rather than losing it.
    /// <para>
    /// Truly nothing left: Despair is the chart's own last global of the phase and outranks
    /// this, so a bar that can still pay its 800 is not dry yet.
    /// </para>
    /// </summary>
    [Fact]
    public void ABarWithNothingLeftSpendsTheParadoxRatherThanLosingIt()
    {
        var suggestion = Suggest(
            Casting().Gauge(s =>
            {
                s.Gauges.BlackMage.AstralFire = 3;
                s.Gauges.BlackMage.AstralSoulStacks = 1;
                s.Gauges.BlackMage.ParadoxActive = true;
                s.Mp = 0;
            }),
            new FakeActionState().Unusable(A.Fire4.Id).Unusable(A.Despair.Id));

        Assert.Equal(A.Paradox.Id, suggestion);
    }

    /// <summary>And Despair keeps its place while the bar can still pay for it.</summary>
    [Fact]
    public void DespairStillComesBeforeAHeldParadox()
    {
        var suggestion = Suggest(
            Casting().Gauge(s =>
            {
                s.Gauges.BlackMage.AstralFire = 3;
                s.Gauges.BlackMage.AstralSoulStacks = 1;
                s.Gauges.BlackMage.ParadoxActive = true;
                s.Mp = 800;
            }),
            new FakeActionState().Unusable(A.Fire4.Id));

        Assert.Equal(A.Despair.Id, suggestion);
    }
}
