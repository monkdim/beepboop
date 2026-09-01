using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Viper;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Viper.ViperActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// How long Rattling Coils are held before they are spent.
/// <para>
/// The list used to spend only at three, which is the cap - so a coil earned while full was
/// simply lost. The Balance's basic priority list says the opposite in as many words:
/// "Spend Rattling Coils as you get them. Save one at all times to cover potential
/// disengages, but spend them before using Serpent's Ire as it will grant another. Avoid
/// overcapping Coils."
/// </para>
/// </summary>
public sealed class ViperCoilTests
{
    private static uint Suggest(byte coils, bool moving = false)
    {
        var builder = new SnapshotBuilder()
            .Job(41).Level(100).Gcd(0f).NoCombo().Enemies(1)
            .Gauge(s => s.Gauges.Viper.RattlingCoils = coils);

        if (moving)
            builder.Moving();

        // Vicewinder is the global above this one and it is unconditional, which is right -
        // the Balance puts the twinblade combo at priority five and the coils at six. Turning
        // it means these tests are about the coil rule rather than about that ordering.
        //
        // Serpent's Ire a minute away, because an Ire that is off cooldown reads as a burst
        // about to start - and that spends the reserve coil, which is its own rule with its
        // own test in ViperBurstSetupTests.
        var actions = new FakeActionState()
            .OnCooldown(A.Vicewinder.Id, 30f)
            .OnCooldown(A.SerpentsIre.Id, 60f);

        return new RotationSession(JobRotationBase.Create<ViperRotation>(),
            new RotationSettings { UseOpener = false, SuggestionHoldSeconds = 0f })
            .Resolve(RotationMode.SingleTarget, builder.Build(), actions)
            .Action.Id;
    }

    /// <summary>
    /// And the ordering itself, so turning Vicewinder off above cannot quietly become the
    /// thing being tested. Priority five is the twinblade combo; six is the coils.
    /// </summary>
    [Fact]
    public void VicewinderStillOutranksTheCoils()
    {
        var full = new SnapshotBuilder()
            .Job(41).Level(100).Gcd(0f).NoCombo().Enemies(1)
            .Gauge(s => s.Gauges.Viper.RattlingCoils = 3)
            .Build();

        var suggestion = new RotationSession(JobRotationBase.Create<ViperRotation>(),
            new RotationSettings { UseOpener = false, SuggestionHoldSeconds = 0f })
            .Resolve(RotationMode.SingleTarget, full,
                new FakeActionState().OnCooldown(A.SerpentsIre.Id, 60f)).Action.Id;

        Assert.Equal(A.Vicewinder.Id, suggestion);
    }

    /// <summary>The defect: a coil earned at the cap was lost, because nothing spent below it.</summary>
    [Fact]
    public void TheSecondCoilIsSpentRatherThanBankedTowardsTheCap()
    {
        Assert.Equal(A.UncoiledFury.Id, Suggest(coils: 2));
    }

    /// <summary>And at the cap, obviously.</summary>
    [Fact]
    public void AFullGaugeIsSpent()
    {
        Assert.Equal(A.UncoiledFury.Id, Suggest(coils: 3));
    }

    /// <summary>
    /// One is kept back. It is what a disengage costs, and Uncoiled Fury is the only ranged
    /// global either list has - so the reserve is the thing that makes the next one free.
    /// </summary>
    [Fact]
    public void TheLastCoilIsHeldInReserve()
    {
        Assert.NotEqual(A.UncoiledFury.Id, Suggest(coils: 1));
    }

    /// <summary>Unless the reserve is being used for the thing it is reserved for.</summary>
    [Fact]
    public void TheReserveIsSpentWhenYouAreMoving()
    {
        Assert.Equal(A.UncoiledFury.Id, Suggest(coils: 1, moving: true));
    }

    /// <summary>An empty gauge suggests something else entirely, rather than a dud press.</summary>
    [Fact]
    public void AnEmptyGaugeIsNotOffered()
    {
        Assert.NotEqual(A.UncoiledFury.Id, Suggest(coils: 0, moving: true));
    }
}
