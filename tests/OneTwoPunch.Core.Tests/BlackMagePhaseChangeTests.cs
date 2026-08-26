using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.BlackMage.BlackMageActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// The four findings a fight analysis put against a real Black Mage pull, each pinned to the
/// rule that was letting it happen.
/// <para>
/// Blizzard III cast in Astral Fire and Fire III cast in Umbral Ice are both damage-penalised,
/// and the pull had nine of each. Both came from the same omission: Transpose crosses the gap
/// between phases for free and off the global, and it was never suggested at all - the list
/// simply hard-cast the penalised spell instead.
/// </para>
/// </summary>
public sealed class BlackMagePhaseChangeTests
{
    private static RotationSession Session() =>
        new(JobRegistry.Create(25)!, new RotationSettings
        {
            UseOpener = false,
            SuggestionHoldSeconds = 0f,
        });

    /// <summary>A weave window, since Transpose and Manafont are both off-globals.</summary>
    private static SnapshotBuilder InAWeaveWindow() =>
        new SnapshotBuilder().Job(25).Gcd(2.2f).NoCombo().Enemies(1);

    /// <summary>
    /// Ley Lines, Amplifier and Manafont all sit above Transpose in the off-global list and
    /// would win every one of these, so all three are put on cooldown. Manafont especially:
    /// refilling the bar and staying in fire genuinely does beat leaving for ice, and that
    /// ordering is checked on its own below rather than allowed to mask these.
    /// </summary>
    private static uint Suggest(SnapshotBuilder builder, FakeActionState? actions = null)
    {
        var state = actions ?? new FakeActionState();
        state.OnCooldown(A.LeyLines.Id, 60f);
        state.OnCooldown(A.Amplifier.Id, 60f);
        state.OnCooldown(A.Manafont.Id, 60f);

        return Session().Resolve(RotationMode.SingleTarget, builder.Build(), state).Action.Id;
    }

    /// <summary>The same, with Manafont left available - only the two Manafont tests want it.</summary>
    private static uint SuggestWithManafont(SnapshotBuilder builder, FakeActionState actions)
    {
        actions.OnCooldown(A.LeyLines.Id, 60f);
        actions.OnCooldown(A.Amplifier.Id, 60f);

        return Session().Resolve(RotationMode.SingleTarget, builder.Build(), actions).Action.Id;
    }

    /// <summary>Astral Fire with the bar spent: no Fire IV, no Despair, nothing held.</summary>
    private static SnapshotBuilder FireIsSpent() =>
        InAWeaveWindow().Gauge(s =>
        {
            s.Gauges.BlackMage.AstralFire = 3;
            s.Gauges.BlackMage.ElementTimeRemaining = 12f;
            s.Gauges.BlackMage.AstralSoulStacks = 0;
            s.Gauges.BlackMage.PolyglotStacks = 2;
            s.Mp = 0;
        });

    /// <summary>Umbral Ice with hearts full, mana full, and Paradox already spent.</summary>
    private static SnapshotBuilder IceIsDone() =>
        InAWeaveWindow().Gauge(s =>
        {
            s.Gauges.BlackMage.UmbralIce = 3;
            s.Gauges.BlackMage.ElementTimeRemaining = 12f;
            s.Gauges.BlackMage.UmbralHearts = 3;
            s.Gauges.BlackMage.ParadoxActive = false;
            s.Gauges.BlackMage.PolyglotStacks = 2;
            s.Mp = 10000;
        });

    /// <summary>"9 Astral Fire phases began with a weakened Fire III."</summary>
    [Fact]
    public void LeavingIceIsATransposeRatherThanAWeakenedFireThree()
    {
        var suggestion = Suggest(IceIsDone());

        Assert.Equal(A.Transpose.Id, suggestion);
    }

    /// <summary>The Hot Blizzard IIIs: nine of them in one pull.</summary>
    [Fact]
    public void LeavingFireIsATransposeRatherThanAWeakenedBlizzardThree()
    {
        var suggestion = Suggest(FireIsSpent(), new FakeActionState().Unusable(A.Fire4.Id).Unusable(A.Despair.Id));

        Assert.Equal(A.Transpose.Id, suggestion);
    }

    /// <summary>But never while there is still something worth casting in the phase.</summary>
    [Fact]
    public void FireIsNotAbandonedWhileDespairIsStillCastable()
    {
        var suggestion = Suggest(FireIsSpent().Gauge(s => s.Mp = 2000),
            new FakeActionState().Unusable(A.Fire4.Id));

        Assert.NotEqual(A.Transpose.Id, suggestion);
    }

    /// <summary>Nor with a free Fire III in hand - that one is full damage and costs nothing.</summary>
    [Fact]
    public void FireIsNotAbandonedWithFirestarterHeld()
    {
        var suggestion = Suggest(
            FireIsSpent().Buff(A.Firestarter.Id, 20f),
            new FakeActionState().Unusable(A.Fire4.Id).Unusable(A.Despair.Id));

        Assert.NotEqual(A.Transpose.Id, suggestion);
    }

    /// <summary>Nor holding a Paradox marker, which is instant and about to be worth a Fire III.</summary>
    [Fact]
    public void IceIsNotLeftWithAParadoxStillHeld()
    {
        var suggestion = Suggest(
            IceIsDone().Gauge(s => s.Gauges.BlackMage.ParadoxActive = true));

        Assert.NotEqual(A.Transpose.Id, suggestion);
    }

    /// <summary>And not before ice has actually refilled the bar.</summary>
    [Fact]
    public void IceIsNotLeftOnAHalfEmptyBar()
    {
        var suggestion = Suggest(
            IceIsDone().Gauge(s => s.Mp = 5000));

        Assert.NotEqual(A.Transpose.Id, suggestion);
    }

    /// <summary>"Manafont was used before Despair 6 times."</summary>
    [Fact]
    public void ManafontWaitsUntilDespairIsNoLongerCastable()
    {
        var suggestion = SuggestWithManafont(
            InAWeaveWindow().Gauge(s =>
            {
                s.Gauges.BlackMage.AstralFire = 3;
                s.Gauges.BlackMage.ElementTimeRemaining = 12f;
                s.Gauges.BlackMage.PolyglotStacks = 2;
                s.Mp = 4000;
            }),
            new FakeActionState().Unusable(A.Fire4.Id));

        Assert.NotEqual(A.Manafont.Id, suggestion);
    }

    /// <summary>And takes it the moment the bar really is spent.</summary>
    [Fact]
    public void ManafontIsTakenOnceTheBarIsSpent()
    {
        var suggestion = SuggestWithManafont(
            InAWeaveWindow().Gauge(s =>
            {
                s.Gauges.BlackMage.AstralFire = 3;
                s.Gauges.BlackMage.ElementTimeRemaining = 12f;
                s.Gauges.BlackMage.AstralSoulStacks = 3;
                s.Gauges.BlackMage.PolyglotStacks = 2;
                s.Mp = 0;
            }),
            new FakeActionState().Unusable(A.Fire4.Id).Unusable(A.Despair.Id));

        Assert.Equal(A.Manafont.Id, suggestion);
    }

    /// <summary>"Paradox got overwritten 9 times" - nothing in fire could ever spend one.</summary>
    [Fact]
    public void AParadoxHeldInAstralFireIsSpentBeforeFireFour()
    {
        var suggestion = Session()
            .Resolve(
                RotationMode.SingleTarget,
                new SnapshotBuilder().Job(25).Gcd(0f).NoCombo().Enemies(1)
                    .Debuff(A.HighThunderBuff.Id, 25f)
                    .Gauge(s =>
                    {
                        s.Gauges.BlackMage.AstralFire = 3;
                        s.Gauges.BlackMage.ElementTimeRemaining = 12f;
                        s.Gauges.BlackMage.ParadoxActive = true;
                        s.Gauges.BlackMage.AstralSoulStacks = 2;
                        s.Mp = 10000;
                    })
                    .Build(),
                new FakeActionState())
            .Action.Id;

        Assert.Equal(A.Paradox.Id, suggestion);
    }
}
