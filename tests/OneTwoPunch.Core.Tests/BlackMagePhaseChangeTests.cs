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
            s.Gauges.BlackMage.AstralSoulStacks = 0;
            s.Gauges.BlackMage.PolyglotStacks = 2;
            s.Mp = 0;
        });

    /// <summary>Umbral Ice with hearts full, mana full, and Paradox already spent.</summary>
    private static SnapshotBuilder IceIsDone() =>
        InAWeaveWindow().Gauge(s =>
        {
            s.Gauges.BlackMage.UmbralIce = 3;
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

    /// <summary>
    /// But a held Firestarter is not a reason to stay, and this test used to say it was.
    /// <para>
    /// The reasoning was "that one is full damage and costs nothing", which is true of the
    /// global and false of the phase. Firestarter lasts thirty seconds - longer than the ice
    /// phase - and carried across it pays for the climb from Astral Fire I to III on the other
    /// side. That climb taken paid is Fire III's 2000 mana plus an Umbral Heart to halve it,
    /// and the heart is another 800. So spending the proc here buys one Fire III at Astral
    /// Fire III and costs the next phase 2800 mana, which is most of two Fire IVs.
    /// </para>
    /// <para>
    /// A recorded twelve minute pull is the whole argument: the proc was burned here fourteen
    /// times, every one at mana 0 and Astral Fire III, eleven of them at Astral Soul 5 - one
    /// Fire IV short of a Flare Star - and nine of its ten climbs were paid for.
    /// </para>
    /// </summary>
    [Fact]
    public void FireIsAbandonedWithTheFirestarterStillHeld()
    {
        var suggestion = Suggest(
            FireIsSpent().Buff(A.Firestarter.Id, 20f),
            new FakeActionState().Unusable(A.Fire4.Id).Unusable(A.Despair.Id));

        Assert.Equal(A.Transpose.Id, suggestion);
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

    /// <summary>
    /// The rung Transpose left out. Crossing into ice lands on Umbral Ice *one*, and mana
    /// only comes back at a useful rate at three - a recorded pull has every ice phase after
    /// the opener stuck at ice 1 with mana peaking at 3500, against a full 10000 in the
    /// opener, which is a third of a fire phase lost every cycle.
    /// </summary>
    [Fact]
    public void UmbralIceOneClimbsToThreeRatherThanCrossingStraightBack()
    {
        var suggestion = Suggest(
            new SnapshotBuilder().Job(25).Gcd(0f).NoCombo().Enemies(1)
                .Debuff(A.HighThunderBuff.Id, 25f)
                .Gauge(s =>
                {
                    s.Gauges.BlackMage.UmbralIce = 1;
                    s.Gauges.BlackMage.UmbralHearts = 3;
                    s.Mp = 3300;
                }));

        Assert.Equal(A.Blizzard3.Id, suggestion);
    }

    /// <summary>Hearts alone are not the signal - they fill at any ice level.</summary>
    [Fact]
    public void FullHeartsAtIceOneAreNotAReasonToGoBackToFire()
    {
        var suggestion = Suggest(
            new SnapshotBuilder().Job(25).Gcd(0f).NoCombo().Enemies(1)
                .Debuff(A.HighThunderBuff.Id, 25f)
                .Gauge(s =>
                {
                    s.Gauges.BlackMage.UmbralIce = 1;
                    s.Gauges.BlackMage.UmbralHearts = 3;
                    s.Mp = 3300;
                }));

        Assert.NotEqual(A.Fire3.Id, suggestion);
    }

    /// <summary>Nor is a full bar, while the ice phase still owes its hearts.</summary>
    [Fact]
    public void AFullBarAtIceThreeStillFillsTheHeartsFirst()
    {
        var suggestion = Suggest(
            new SnapshotBuilder().Job(25).Gcd(0f).NoCombo().Enemies(1)
                .Debuff(A.HighThunderBuff.Id, 25f)
                .Gauge(s =>
                {
                    s.Gauges.BlackMage.UmbralIce = 3;
                    s.Gauges.BlackMage.UmbralHearts = 0;
                    s.Mp = 10000;
                }));

        Assert.Equal(A.Blizzard4.Id, suggestion);
    }

    /// <summary>
    /// And with everything full but the last mana tick outstanding, the answer is still an
    /// ice global rather than the Fire I the list used to fall through to.
    /// </summary>
    [Fact]
    public void WaitingOnTheLastManaTickDoesNotFallThroughToFireOne()
    {
        var suggestion = Suggest(
            new SnapshotBuilder().Job(25).Gcd(0f).NoCombo().Enemies(1)
                .Debuff(A.HighThunderBuff.Id, 25f)
                .Gauge(s =>
                {
                    s.Gauges.BlackMage.UmbralIce = 3;
                    s.Gauges.BlackMage.UmbralHearts = 3;
                    s.Mp = 7000;
                }));

        Assert.NotEqual(A.Fire1.Id, suggestion);
        Assert.NotEqual(A.Fire3.Id, suggestion);
    }

    /// <summary>
    /// The mirror of the ice rung, missing for the same reason. Transpose crosses into Astral
    /// Fire *one*, and Fire IV there is a fraction of what it is at three - a recorded level
    /// 90 pull has twenty-one globals at "fire 1" because both ice exits Transposed and
    /// nothing climbed.
    /// </summary>
    [Fact]
    public void AstralFireOneClimbsToThreeRatherThanCastingFireFourAtTheBottomRung()
    {
        var suggestion = Suggest(
            new SnapshotBuilder().Job(25).Gcd(0f).NoCombo().Enemies(1)
                .Debuff(A.HighThunderBuff.Id, 25f)
                .Gauge(s =>
                {
                    s.Gauges.BlackMage.AstralFire = 1;
                    s.Mp = 10000;
                }));

        Assert.Equal(A.Fire3.Id, suggestion);
    }

    /// <summary>And at the top rung it is Fire IV again, not an endless Fire III.</summary>
    [Fact]
    public void AstralFireThreeCastsFireFour()
    {
        var suggestion = Suggest(
            new SnapshotBuilder().Job(25).Gcd(0f).NoCombo().Enemies(1)
                .Debuff(A.HighThunderBuff.Id, 25f)
                .Gauge(s =>
                {
                    s.Gauges.BlackMage.AstralFire = 3;
                    s.Mp = 10000;
                }));

        Assert.Equal(A.Fire4.Id, suggestion);
    }

    /// <summary>
    /// The crossing in full, corrected against The Balance's own chart. At Astral Fire one the
    /// global that belongs there is the climb, and the fire phase's own Paradox waits for its
    /// eighth global.
    /// <para>
    /// This used to name the ice phase's Paradox as what makes the climb free. It is not -
    /// only Paradox cast in Astral Fire leaves a Firestarter behind. The proc comes from the
    /// previous fire phase's Paradox and survives the crossing, which is why a held one is no
    /// longer a reason to stay in fire.
    /// </para>
    /// </summary>
    [Fact]
    public void TheClimbComesBeforeTheParadox()
    {
        var suggestion = Suggest(
            new SnapshotBuilder().Job(25).Gcd(0f).NoCombo().Enemies(1)
                .Debuff(A.HighThunderBuff.Id, 25f)
                .Gauge(s =>
                {
                    s.Gauges.BlackMage.AstralFire = 1;
                    s.Gauges.BlackMage.ParadoxActive = true;
                    s.Mp = 10000;
                }));

        Assert.Equal(A.Fire3.Id, suggestion);
    }

    /// <summary>"Manafont was used before Despair 6 times."</summary>
    [Fact]
    public void ManafontWaitsUntilDespairIsNoLongerCastable()
    {
        var suggestion = SuggestWithManafont(
            InAWeaveWindow().Gauge(s =>
            {
                s.Gauges.BlackMage.AstralFire = 3;
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
                        s.Gauges.BlackMage.ParadoxActive = true;
                        s.Gauges.BlackMage.AstralSoulStacks = 3;
                        s.Mp = 10000;
                    })
                    .Build(),
                new FakeActionState())
            .Action.Id;

        Assert.Equal(A.Paradox.Id, suggestion);
    }
}
