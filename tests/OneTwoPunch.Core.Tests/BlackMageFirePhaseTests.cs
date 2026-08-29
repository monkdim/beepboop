using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.BlackMage.BlackMageActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// The two things a fight analysis put against a recorded twelve minute Black Mage pull, both
/// of them about the last globals of the Astral Fire phase.
/// <para>
/// "Once you can no longer cast another spell in Astral Fire and remain above 800 MP, you
/// should use your remaining MP by casting Despair - seven Astral Fire phases were missing at
/// least one Despair." And under it, a Flare Star missing from most windows, which is the same
/// bar running out one Fire IV early.
/// </para>
/// <para>
/// The arithmetic, taken off the recorded pull rather than assumed: Fire IV costs 1600 in
/// Astral Fire and 800 with an Umbral Heart to halve it; Fire III costs 2000, or 1000 with a
/// heart; Despair asks for 800 and then takes whatever is left; Paradox and Despair grant no
/// Astral Soul, so the six that Flare Star wants are six Fire IVs and nothing else.
/// </para>
/// </summary>
public sealed class BlackMageFirePhaseTests
{
    private static SnapshotBuilder Casting() =>
        new SnapshotBuilder().Job(25).Level(100).Gcd(0f).NoCombo().Enemies(1)
            .Debuff(A.HighThunderBuff.Id, 25f);

    private static SnapshotBuilder InAWeaveWindow() =>
        new SnapshotBuilder().Job(25).Level(100).Gcd(2.2f).NoCombo().Enemies(1)
            .Debuff(A.HighThunderBuff.Id, 25f);

    /// <summary>Astral Fire III, mid-phase, with the bar and the hearts set by the test.</summary>
    private static SnapshotBuilder Fire(uint mp, byte hearts, byte soul) =>
        Casting().Gauge(s =>
        {
            s.Gauges.BlackMage.AstralFire = 3;
            s.Gauges.BlackMage.UmbralHearts = hearts;
            s.Gauges.BlackMage.AstralSoulStacks = soul;
            s.Mp = mp;
        });

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

    // ---- Despair, and the bar it needs left for it ------------------------

    /// <summary>
    /// The exact state the recorded pull ends on: 1600 mana, no hearts, Astral Soul 4. Fire IV
    /// costs all of it, so taking it here is the Despair gone - which is what happened, twice
    /// in the last two globals of the log and seven phases across the fight.
    /// </summary>
    [Fact]
    public void DespairComesFirstWhenAFireFourWouldEmptyTheBar()
    {
        Assert.Equal(A.Despair.Id, Suggest(Fire(mp: 1600, hearts: 0, soul: 4)));
    }

    /// <summary>One Fire IV and a Despair after it is 2400, and 2400 is enough for both.</summary>
    [Fact]
    public void FireFourStaysWhileTheBarCanAffordBothOfThem()
    {
        Assert.Equal(A.Fire4.Id, Suggest(Fire(mp: 2400, hearts: 0, soul: 4)));
    }

    /// <summary>
    /// A heart halves the Fire IV to 800, so the same 1600 that could not pay for both a moment
    /// ago now pays for both. The gate is the cost of the next cast, not a flat number.
    /// </summary>
    [Fact]
    public void AnUmbralHeartPaysForTheFireFourAndLeavesTheDespair()
    {
        Assert.Equal(A.Fire4.Id, Suggest(Fire(mp: 1600, hearts: 1, soul: 4)));
    }

    /// <summary>
    /// Below 72 there is no Despair to hold mana back for, and holding it back anyway would end
    /// the fire phase a Fire IV early - the shape of every level-gating bug this list has had.
    /// </summary>
    [Fact]
    public void BelowSeventyTwoTheBarIsFireFoursAlone()
    {
        var packet = Fire(mp: 1600, hearts: 0, soul: 0).Level(70);

        Assert.Equal(A.Fire4.Id, Suggest(packet));
    }

    /// <summary>Six Astral Soul is Flare Star's, and Despair does not get to jump it.</summary>
    [Fact]
    public void AFullAstralSoulIsStillFlareStar()
    {
        Assert.Equal(A.FlareStar.Id, Suggest(Fire(mp: 1600, hearts: 0, soul: 6)));
    }

    /// <summary>
    /// And with the bar genuinely spent, Despair is what the phase ends on rather than a Fire
    /// IV that cannot cast. This is the rule's old behaviour and it still holds.
    /// </summary>
    [Fact]
    public void AnEmptyingBarStillEndsOnDespair()
    {
        Assert.Equal(A.Despair.Id, Suggest(Fire(mp: 800, hearts: 0, soul: 4)));
    }

    // ---- The Firestarter the crossing is supposed to carry ----------------

    /// <summary>Astral Fire spent: neither Fire IV nor Despair will cast.</summary>
    private static FakeActionState BarIsSpent() =>
        new FakeActionState().Unusable(A.Fire4.Id).Unusable(A.Despair.Id);

    /// <summary>
    /// The proc is worth 2800 mana on the other side - a 2000 mana Fire III and the Umbral
    /// Heart that would have halved it - and one global at Astral Fire III here. A held
    /// Firestarter used to block this Transpose outright, so the pull burned it fourteen times
    /// and paid for nine of its ten climbs.
    /// </summary>
    [Fact]
    public void AHeldFirestarterDoesNotBlockTheCrossing()
    {
        var packet = InAWeaveWindow().Buff(A.Firestarter.Id, 25f).Gauge(s =>
        {
            s.Gauges.BlackMage.AstralFire = 3;
            s.Gauges.BlackMage.AstralSoulStacks = 4;
            s.Gauges.BlackMage.ParadoxActive = false;
            s.Mp = 0;
        });

        Assert.Equal(A.Transpose.Id, Suggest(packet, BarIsSpent()));
    }

    /// <summary>
    /// Carried across, it is what makes the climb free - Astral Fire I with a Firestarter is a
    /// Fire III that costs nothing and no heart, and arrives at three.
    /// </summary>
    [Fact]
    public void TheCarriedFirestarterPaysForTheClimb()
    {
        var packet = Casting().Buff(A.Firestarter.Id, 20f).Gauge(s =>
        {
            s.Gauges.BlackMage.AstralFire = 1;
            s.Gauges.BlackMage.UmbralHearts = 3;
            s.Mp = 10000;
        });

        Assert.Equal(A.Fire3.Id, Suggest(packet));
    }

    /// <summary>
    /// The safety net stays. With the global up and the crossing missed, a free instant Fire
    /// III is still the best thing left to say - the point of the change is that the phase no
    /// longer has to arrive here, not that it may never.
    /// </summary>
    [Fact]
    public void AMissedCrossingStillSpendsTheProcRatherThanStalling()
    {
        var packet = Casting().Buff(A.Firestarter.Id, 25f).Gauge(s =>
        {
            s.Gauges.BlackMage.AstralFire = 3;
            s.Gauges.BlackMage.AstralSoulStacks = 4;
            s.Mp = 0;
        });

        Assert.Equal(A.Fire3.Id, Suggest(packet, BarIsSpent()));
    }
}
